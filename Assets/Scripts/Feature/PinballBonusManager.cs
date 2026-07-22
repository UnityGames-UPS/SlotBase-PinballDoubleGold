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

  [Header("Ring — Movement Path")]
  // The circles the ball travels through, in order (outer loop -> inner layer -> toward marbles).
  // Lit one after another to convey the ball moving. All circles share the same two sprites, so
  // those are single fields here and pathCircles is just the ordered list of circle Images.
  [SerializeField] private Sprite circleLitSprite;
  [SerializeField] private Sprite circleUnlitSprite;
  [SerializeField] private List<Image> pathCircles;

  [Header("Ring — Prizes (UFOs + marbles)")]
  // Every prize the ball can land on. Matched to a shot by isSpecial + prizeIndex; stopAtCircleIndex
  // says which pathCircles index the ball rests at before landing here.
  [SerializeField] private List<Prize> prizes;

  [Header("Ring Animation Timing")]
  [SerializeField] private float perCircleLightDuration = 0.05f;
  [SerializeField] private int prizeFlashCount = 4;
  [SerializeField] private float prizeFlashHalfCycle = 0.15f;
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
    yield return StartCoroutine(AnimateShot(p.isSpecial, p.selectedIndex));

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
  // Lights the path circles in order up to the destination prize, then flashes that prize. Backend
  // gives only the destination (isSpecial + selectedIndex); the traversal is synthesized here.
  private IEnumerator AnimateShot(bool isSpecial, int selectedIndex)
  {
    Prize dest = FindPrize(isSpecial, selectedIndex);
    int lastCircle = pathCircles != null ? pathCircles.Count - 1 : -1;
    int stop = dest != null ? Mathf.Clamp(dest.stopAtCircleIndex, 0, lastCircle) : lastCircle;

    // Ball moving: single travelling light along the circles.
    for (int i = 0; i <= stop; i++)
    {
      SetCircleLit(i, true);
      yield return new WaitForSeconds(perCircleLightDuration);
      if (i < stop) SetCircleLit(i, false);
    }
    if (stop >= 0) SetCircleLit(stop, false);

    // Ball landed: flash the prize its own way.
    if (dest != null) yield return StartCoroutine(FlashPrize(dest));
  }

  // The prize whose isSpecial + prizeIndex match the shot result.
  private Prize FindPrize(bool isSpecial, int selectedIndex)
  {
    if (prizes != null)
      foreach (Prize p in prizes)
        if (p != null && p.isSpecial == isSpecial && p.prizeIndex == selectedIndex)
          return p;
    Debug.LogWarning($"[PinballBonus] No prize wired for isSpecial={isSpecial}, selectedIndex={selectedIndex}.");
    return null;
  }

  private IEnumerator FlashPrize(Prize prize)
  {
    if (prize == null || prize.image == null) yield break;
    for (int i = 0; i < prizeFlashCount; i++)
    {
      if (prize.highlightSprite) prize.image.sprite = prize.highlightSprite;
      yield return new WaitForSeconds(prizeFlashHalfCycle);
      if (prize.baseSprite) prize.image.sprite = prize.baseSprite;
      yield return new WaitForSeconds(prizeFlashHalfCycle);
    }
  }

  private void SetCircleLit(int index, bool on)
  {
    if (pathCircles == null || index < 0 || index >= pathCircles.Count) return;
    Image img = pathCircles[index];
    if (img) img.sprite = on ? circleLitSprite : circleUnlitSprite;
  }

  private void ClearAllLights()
  {
    if (pathCircles != null)
      foreach (Image img in pathCircles)
        if (img) img.sprite = circleUnlitSprite;
    if (prizes != null)
      foreach (Prize p in prizes)
        if (p != null && p.image && p.baseSprite) p.image.sprite = p.baseSprite;
  }

  // Prize point values shown on the ships/marbles are bet-dependent and set at runtime here.
  // Base values are available via socketManager.GameFeatures.pinball.prizes / specialPrizes
  // (prizes[prizeIndex] for normal prizes, specialPrizes[prizeIndex].prize for special ones).
  // TODO(pinball): apply the bet-scaling factor once known, e.g.:
  //   PinballConfig cfg = socketManager?.GameFeatures?.pinball;
  //   foreach prize with a pointsAmount:
  //     int base = prize.isSpecial ? cfg.specialPrizes[prize.prizeIndex].prize
  //                                : cfg.prizes[prize.prizeIndex];
  //     prize.pointsAmount.text = (base * BetScaleFactor(_betIndex)).ToString();
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

// One prize the ball can land on — a UFO or a marble (same class; isSpecial flags the "+1 Shot" UFOs).
// Matched to a shot by isSpecial + prizeIndex.
[System.Serializable]
public class Prize
{
  public Image image;                   // the prize's Image; sprite swapped between base/highlight when it flashes
  public Sprite baseSprite;
  public Sprite highlightSprite;
  public bool isSpecial;                // true = a "+1 Shot" pocket (backend isSpecial)
  public int prizeIndex = -1;           // the backend selectedIndex this prize pays
  public TMP_Text pointsAmount;         // runtime-set, bet-dependent points label
  public int stopAtCircleIndex = -1;    // which pathCircles index the ball rests at before landing here
}
