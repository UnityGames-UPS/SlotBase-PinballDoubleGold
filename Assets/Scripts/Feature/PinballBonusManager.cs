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
  [SerializeField] private float perCircleLightDuration = 0.12f;   // time per circle as the light travels (higher = slower ball)
  [SerializeField] private int outerTrailMax = 2;                  // circles lit ahead/behind the ball at the start of the outer loop; shrinks to 0 by the inner layer
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
    RefreshPlusShotLabels();
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
    // 1. Outer loop, always the same, straight through — lit as a shrinking comet trail.
    yield return LightOuterPathWithTrail();

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

  // Outer loop only: the ball leaves a comet trail. Near the shoot point, `outerTrailMax` circles
  // ahead and behind the ball stay lit; the trail width shrinks linearly to 0 across the loop, so by
  // the time the ball hands off to the inner layer only the ball itself is lit. Each step lights the
  // window [i - trail, i + trail] and unlights every other outer circle, so old trail circles fade off
  // as the window advances and narrows. The final lit circle is handed to the travelling-light tracker
  // so the inner route continues seamlessly (its first MoveLightTo turns this one off).
  private IEnumerator LightOuterPathWithTrail()
  {
    if (outerPath == null || outerPath.Count == 0) yield break;
    int n = outerPath.Count;
    for (int i = 0; i < n; i++)
    {
      float progress = n > 1 ? (float)i / (n - 1) : 1f;
      int trail = Mathf.RoundToInt(outerTrailMax * (1f - progress));
      for (int j = 0; j < n; j++)
      {
        if (!outerPath[j]) continue;
        bool lit = j >= i - trail && j <= i + trail;
        outerPath[j].sprite = lit ? circleLitSprite : circleUnlitSprite;
      }
      yield return new WaitForSeconds(perCircleLightDuration);
    }
    _litCircle = outerPath[n - 1];   // single remaining lit circle; the inner route picks up from here
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

  // Sets each UFO/marble prize label from the init base values × the current line bet — i.e. the
  // MONEY that prize would pay, matching the backend's bonusWin = baseValue × lineBet.
  // NOTE: this shows the money value (e.g. 1.50). The special-game UI convention is to show POINTS
  // (money × 100 = 150); that ×100 is deliberately NOT applied yet — multiply here once confirmed.
  private void RefreshPrizeLabels()
  {
    PinballConfig cfg = socketManager != null && socketManager.GameFeatures != null
      ? socketManager.GameFeatures.pinball : null;
    if (cfg == null) return;

    double lineBet = 0;
    var bets = socketManager.InitialData != null ? socketManager.InitialData.bets : null;
    if (bets != null && _betIndex >= 0 && _betIndex < bets.Count) lineBet = bets[_betIndex];

    // UFOs: prizes[] normally, specialPrizes[] when special, by the UFO's prizeIndex.
    if (ufos != null)
      foreach (Ufo u in ufos)
      {
        if (u == null || u.prizeAmount == null || u.prizeIndex < 0) continue;
        double baseValue;
        if (u.isSpecial)
        {
          if (cfg.specialPrizes == null || u.prizeIndex >= cfg.specialPrizes.Count) continue;
          baseValue = cfg.specialPrizes[u.prizeIndex].prize;
        }
        else
        {
          if (cfg.prizes == null || u.prizeIndex >= cfg.prizes.Count) continue;
          baseValue = cfg.prizes[u.prizeIndex];
        }
        u.prizeAmount.text = (baseValue * lineBet).ToString("F2");
      }

    // Marbles: chutePrizes[] by list position. Jackpot marble (last) has no prizeAmount (JACKPOT graphic).
    if (marbles != null && cfg.chutePrizes != null)
      for (int i = 0; i < marbles.Count && i < cfg.chutePrizes.Count; i++)
      {
        Marble m = marbles[i];
        if (m == null || m.prizeAmount == null) continue;
        m.prizeAmount.text = (cfg.chutePrizes[i] * lineBet).ToString("F2");
      }
  }

  // Shows the "+1 Shot" object only on the special UFOs (hidden on the rest). Runs at bonus start and
  // reads ufo.isSpecial — set in the editor for a fixed layout, or by code once a per-bonus layout lands.
  private void RefreshPlusShotLabels()
  {
    if (ufos == null) return;
    foreach (Ufo u in ufos)
      if (u != null && u.plusShotLabel != null)
        u.plusShotLabel.SetActive(u.isSpecial);
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
