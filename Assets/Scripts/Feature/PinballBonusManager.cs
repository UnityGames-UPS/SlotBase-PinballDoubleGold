using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

// Owns the entire "Pinball" bonus event: the base->bonus scroll transition, the per-press shot loop
// (each Shoot Ball press = one "BONUS" socket request), the ring/pocket light animation, the Shots /
// Bonus-Win / Total-Bet displays, and the return to the base game.
//
// Data contract (see pdg-backend-clarifications):
//  - Triggered from SlotBehaviour via BeginBonus() with the starting shot count.
//  - Each BONUS response: payload.bonusWin (this shot), payload.isOver (end signal),
//    payload.selectedIndex (+ payload.isSpecial) = the PRIZE the ball landed on (indexes
//    prizes[]/specialPrizes[], NOT a ring circle), payload.bonusState { shotsRemaining,
//    totalBonusWin, extraShots }. Balance is credited by the backend only on the isOver shot.
public class PinballBonusManager : MonoBehaviour
{
  [Header("References")]
  [SerializeField] private SocketIOManager socketManager;
  [SerializeField] private SlotBehaviour slotBehaviour;

  [Header("Transition (scroll base machine out, bonus UI in)")]
  // Sibling movers: gameContentRoot (the base machine, GameContent) scrolls down/out while
  // pinballSpecialUI scrolls in from one screen above. Two synced tweens, same duration + ease.
  [SerializeField] private RectTransform gameContentRoot;   // = GameContent; scrolls down (machine visible, faded base UI rides along)
  [SerializeField] private RectTransform pinballSpecialUI;  // = PinballSpecialUI root; scrolls in from the top
  [SerializeField] private CanvasGroup baseGameUI;          // wrapper around the surrounding base UI that fades out (not the machine)
  [SerializeField] private CanvasGroup bonusDecorations;    // ring/ships/marbles/counters that fade in after scroll
  [SerializeField] private float scrollDistance = 1080f;
  [SerializeField] private float scrollDuration = 1f;
  [SerializeField] private Ease scrollEase = Ease.InOutCubic;
  [SerializeField] private float fadeDuration = 0.4f;

  [Header("Controls & Displays")]
  [SerializeField] private Button shootButton;
  [SerializeField] private TMP_Text shotsAmount;
  [SerializeField] private TMP_Text bonusWinAmount;
  [SerializeField] private TMP_Text totalBetAmount;

  [Header("Ring — Circles")]
  // All circles (outer loop + inner route circles) share the same two sprites, swapped to light up.
  [SerializeField] private Sprite circleLitSprite;
  [SerializeField] private Sprite circleUnlitSprite;
  // The outer loop, lit straight through every shot before the inner routing begins.
  [SerializeField] private List<Image> outerPath;

  [Header("Ring — Prizes")]
  // UFOs: land-beside prizes reached via curated routes, matched by isSpecial + selectedIndex.
  // Marbles: the chute accumulator — filled bottom->top, so ordered here in FILL order
  // (marbles[0] = first/bottom = chutePrizes[0]; marbles[last] = jackpot = top). A chute shot lands
  // on marbles[selectedIndex] (== chuteHits-1) and that marble stays lit for the rest of the bonus.
  [SerializeField] private List<Ufo> ufos = new List<Ufo>();
  [SerializeField] private List<Marble> marbles = new List<Marble>();
  [SerializeField] private List<BallRoute> marbleApproachRoutes = new List<BallRoute>();
  // Shared marble collected/uncollected sprites (all marbles use the same pair; the jackpot marble's
  // distinct look is its own child graphic on top). A collected marble's Image sits on marbleLitSprite.
  [SerializeField] private Sprite marbleLitSprite;
  [SerializeField] private Sprite marbleUnlitSprite;

  [Header("Ring Animation Timing")]
  [SerializeField] private float perCircleLightDuration = 0.05f;   // time per circle as the light travels
  [SerializeField] private float marbleLandHold = 0.5f;            // hold after a marble is collected
  [SerializeField] private int prizeFlashCount = 4;                // flash pulses on the landed prize
  [SerializeField] private float prizeFlashHalfCycle = 0.15f;
  [SerializeField] private float prizeFlashLowAlpha = 0.2f;        // dim end of each flash pulse
  [SerializeField] private float endHoldDuration = 1.5f;

  // Runtime state
  private int _betIndex;
  private double _totalBet;
  private int _shotsRemaining;
  private bool _featureActive;
  private bool _shotInFlight;
  private bool _homesCaptured;
  private float _gameContentHomeY;
  private float _specialHomeY;
  private Image _litCircle;   // the single circle currently lit (the travelling light)

  private void Awake()
  {
    if (shootButton)
    {
      shootButton.onClick.RemoveListener(OnShootPressed);
      shootButton.onClick.AddListener(OnShootPressed);
    }
  }

  #region Entry / Exit
  // Called by SlotBehaviour once the trigger flash has played. Runs the transition, then hands
  // control to the player's shots.
  internal void BeginBonus(int startShots, int betIndex, double totalBet)
  {
    if (_featureActive) return;
    _betIndex = betIndex;
    _totalBet = totalBet;
    _shotsRemaining = startShots;
    _featureActive = true;
    _shotInFlight = false;
    StartCoroutine(BeginBonusRoutine());
  }

  private IEnumerator BeginBonusRoutine()
  {
    UpdateShotsAmount(_shotsRemaining);
    UpdateBonusWinAmount(0);
    if (totalBetAmount) totalBetAmount.text = _totalBet.ToString("F2");
    RefreshPrizeLabels();
    ClearAllLights();
    SetShootInteractable(false);

    yield return StartCoroutine(TransitionToBonus());

    SetShootInteractable(_shotsRemaining > 0);
  }

  private IEnumerator EndBonus()
  {
    SetShootInteractable(false);
    // TODO (with team): optional final bonus-win flourish before we leave. Balance is already
    // credited by the backend on the isOver shot, so the display resync happens in OnBonusComplete.
    yield return new WaitForSeconds(endHoldDuration);
    yield return StartCoroutine(TransitionFromBonus());
    _featureActive = false;
    if (slotBehaviour) slotBehaviour.OnBonusComplete();
  }
  #endregion

  #region Shots
  private void OnShootPressed()
  {
    if (!_featureActive || _shotInFlight || _shotsRemaining <= 0) return;
    StartCoroutine(ShootRoutine());
  }

  private IEnumerator ShootRoutine()
  {
    if (socketManager == null)
    {
      Debug.LogWarning("[PinballBonus] socketManager not assigned — cannot fire a shot.");
      yield break;
    }

    _shotInFlight = true;
    SetShootInteractable(false);

    socketManager.AccumulateBonusResult(_betIndex);
    yield return new WaitUntil(() => socketManager.isResultdone);

    Payload p = socketManager.ResultData.payload;
    yield return StartCoroutine(AnimateShot(p.isChute, p.isSpecial, p.selectedIndex));

    // Trust the backend's post-shot state for the displays and the next enable/disable decision.
    _shotsRemaining = p.bonusState.shotsRemaining;
    UpdateShotsAmount(_shotsRemaining);
    UpdateBonusWinAmount(p.bonusState.totalBonusWin);

    _shotInFlight = false;

    if (p.isOver)
      yield return StartCoroutine(EndBonus());
    else
      SetShootInteractable(_shotsRemaining > 0);
  }
  #endregion

  #region Ring animation
  // Plays the ball's journey for one shot: the outer loop, then to the backend-chosen destination.
  private IEnumerator AnimateShot(bool isChute, bool isSpecial, int selectedIndex)
  {
    // 1. Outer loop, always the same, straight through.
    yield return LightSequence(outerPath);

    // 2a. Chute — approach the marble entry, then collect marbles[selectedIndex] (it stays lit).
    if (isChute)
    {
      yield return LightSequence(PickRoute(marbleApproachRoutes)?.circles);
      ClearLight();
      yield return CollectMarble(selectedIndex);
      yield break;
    }

    // 2b. UFO (regular or special) — travel a curated route to a circle beside it, then flash it.
    Ufo ufo = FindUfo(isSpecial, selectedIndex);
    if (ufo != null)
    {
      BallRoute route = PickRoute(ufo.routes);
      if (route != null) yield return LightSequence(route.circles);
      ClearLight();
      yield return FlashPrize(ufo.image);
      ClearLight();
      yield break;
    }

    ClearLight();
    Debug.LogWarning($"[PinballBonus] No UFO wired for isSpecial={isSpecial}, selectedIndex={selectedIndex}.");
  }

  // Moves the single travelling light through the given circles in order. Continuous across calls,
  // so outer loop -> route reads as one moving light (each step turns off the previous circle).
  private IEnumerator LightSequence(List<Image> circles)
  {
    if (circles == null) yield break;
    foreach (Image circle in circles)
    {
      if (!circle) continue;
      MoveLightTo(circle);
      yield return new WaitForSeconds(perCircleLightDuration);
    }
  }

  // The chute is a bottom-up accumulator: the newly-reached marble (marbles[index], index == chuteHits-1)
  // lights and STAYS lit for the rest of the bonus. Prior marbles are already lit from earlier hits,
  // so only the new one is touched here. marbles[last] is the jackpot.
  private IEnumerator CollectMarble(int index)
  {
    if (marbles == null || index < 0 || index >= marbles.Count)
    {
      Debug.LogWarning($"[PinballBonus] Chute shot for marble index {index}, but no such marble is wired.");
      yield break;
    }
    Marble m = marbles[index];
    if (m != null && m.image && marbleLitSprite) m.image.sprite = marbleLitSprite;
    yield return new WaitForSeconds(marbleLandHold);
  }

  private Ufo FindUfo(bool isSpecial, int selectedIndex)
  {
    if (ufos != null)
      foreach (Ufo u in ufos)
        if (u != null && u.isSpecial == isSpecial && u.prizeIndex == selectedIndex)
          return u;
    return null;
  }

  private BallRoute PickRoute(List<BallRoute> routes)
  {
    if (routes == null || routes.Count == 0) return null;
    return routes[Random.Range(0, routes.Count)];
  }

  // Circle lighting is a single travelling light: only one circle is lit at a time, so lighting the
  // next turns off the previous. That also means clearing is just clearing that one circle.
  private void MoveLightTo(Image circle)
  {
    if (_litCircle && _litCircle != circle) _litCircle.sprite = circleUnlitSprite;
    if (circle) circle.sprite = circleLitSprite;
    _litCircle = circle;
  }

  private void ClearLight()
  {
    if (_litCircle) _litCircle.sprite = circleUnlitSprite;
    _litCircle = null;
  }

  // A flashing pulse on the landed prize (UFO or the final marble): alpha yoyo, restored to full.
  private IEnumerator FlashPrize(Image img)
  {
    if (!img) yield break;
    img.DOKill();
    img.DOFade(prizeFlashLowAlpha, prizeFlashHalfCycle).SetLoops(prizeFlashCount * 2, LoopType.Yoyo);
    yield return new WaitForSeconds(prizeFlashCount * 2 * prizeFlashHalfCycle);
    RestoreAlpha(img);
  }

  private static void RestoreAlpha(Image img)
  {
    Color c = img.color;
    c.a = 1f;
    img.color = c;
  }

  private void ClearAllLights()
  {
    ClearLight();
    if (outerPath != null)
      foreach (Image img in outerPath)
        if (img) img.sprite = circleUnlitSprite;
    if (ufos != null)
      foreach (Ufo u in ufos)
        if (u != null && u.image) RestoreAlpha(u.image);
    // Reset the chute: every marble back to uncollected (unlit). Called at bonus start, so the
    // accumulator starts empty; during a bonus, collected marbles are intentionally left lit.
    if (marbles != null)
      foreach (Marble m in marbles)
        if (m != null && m.image && marbleUnlitSprite) m.image.sprite = marbleUnlitSprite;
  }

  // Prize point values shown on the UFOs/marbles are bet-dependent and set at runtime here.
  // Base values are in socketManager.GameFeatures.pinball: prizes[]/specialPrizes[] for UFOs (by their
  // prizeIndex), chutePrizes[] for marbles (by their POSITION in the marbles list).
  // TODO(pinball): apply the bet-scaling factor once known, e.g.:
  //   PinballConfig cfg = socketManager?.GameFeatures?.pinball;
  //   foreach ufo:    ufo.prizeAmount.text = ((ufo.isSpecial ? cfg.specialPrizes[ufo.prizeIndex].prize
  //                                                           : cfg.prizes[ufo.prizeIndex]) * BetScaleFactor(_betIndex)).ToString();
  //   for (int i = 0; i < marbles.Count; i++)  // jackpot marble (last) shows its JACKPOT graphic, no label
  //     marbles[i].prizeAmount.text = (cfg.chutePrizes[i] * BetScaleFactor(_betIndex)).ToString();
  private void RefreshPrizeLabels()
  {
  }
  #endregion

  #region Transition
  private IEnumerator TransitionToBonus()
  {
    // The bonus UI is deactivated + hidden by default; turn it on before moving it.
    if (pinballSpecialUI) pinballSpecialUI.gameObject.SetActive(true);

    // Capture the on-screen resting positions once, before anything moves, so repeat bonuses
    // don't re-read a parked/off-screen position as "home".
    if (!_homesCaptured)
    {
      if (gameContentRoot) _gameContentHomeY = gameContentRoot.anchoredPosition.y;
      if (pinballSpecialUI) _specialHomeY = pinballSpecialUI.anchoredPosition.y;
      _homesCaptured = true;
    }

    // Park the bonus UI one screen above its resting spot, ready to scroll in.
    if (pinballSpecialUI)
      pinballSpecialUI.anchoredPosition = new Vector2(pinballSpecialUI.anchoredPosition.x, _specialHomeY + scrollDistance);
    if (bonusDecorations) bonusDecorations.alpha = 0f;

    if (baseGameUI)
    {
      yield return baseGameUI.DOFade(0f, fadeDuration).WaitForCompletion();
      baseGameUI.interactable = false;
      baseGameUI.blocksRaycasts = false;
    }

    // Machine scrolls down/out and the bonus UI scrolls in — two synced tweens, wait on the longer-lived one.
    Tween machineTween = gameContentRoot
      ? gameContentRoot.DOAnchorPosY(_gameContentHomeY - scrollDistance, scrollDuration).SetEase(scrollEase)
      : null;
    Tween specialTween = pinballSpecialUI
      ? pinballSpecialUI.DOAnchorPosY(_specialHomeY, scrollDuration).SetEase(scrollEase)
      : null;
    if (specialTween != null) yield return specialTween.WaitForCompletion();
    else if (machineTween != null) yield return machineTween.WaitForCompletion();

    if (bonusDecorations)
    {
      bonusDecorations.blocksRaycasts = true;
      bonusDecorations.interactable = true;
      yield return bonusDecorations.DOFade(1f, fadeDuration).WaitForCompletion();
    }
  }

  private IEnumerator TransitionFromBonus()
  {
    if (bonusDecorations)
    {
      yield return bonusDecorations.DOFade(0f, fadeDuration).WaitForCompletion();
      bonusDecorations.blocksRaycasts = false;
      bonusDecorations.interactable = false;
    }

    Tween machineTween = gameContentRoot
      ? gameContentRoot.DOAnchorPosY(_gameContentHomeY, scrollDuration).SetEase(scrollEase)
      : null;
    Tween specialTween = pinballSpecialUI
      ? pinballSpecialUI.DOAnchorPosY(_specialHomeY + scrollDistance, scrollDuration).SetEase(scrollEase)
      : null;
    if (specialTween != null) yield return specialTween.WaitForCompletion();
    else if (machineTween != null) yield return machineTween.WaitForCompletion();

    if (baseGameUI)
    {
      baseGameUI.interactable = true;
      baseGameUI.blocksRaycasts = true;
      yield return baseGameUI.DOFade(1f, fadeDuration).WaitForCompletion();
    }

    // Hide the bonus UI again now that it's parked off-screen.
    if (pinballSpecialUI) pinballSpecialUI.gameObject.SetActive(false);
  }
  #endregion

  #region Displays
  private void SetShootInteractable(bool on)
  {
    if (shootButton) shootButton.interactable = on;
  }

  private void UpdateShotsAmount(int shots)
  {
    if (shotsAmount) shotsAmount.text = shots.ToString();
  }

  private void UpdateBonusWinAmount(double amount)
  {
    if (bonusWinAmount) bonusWinAmount.text = amount.ToString("F2");
  }
  #endregion
}
