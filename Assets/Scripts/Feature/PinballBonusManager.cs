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
  [SerializeField] private UIManager uiManager;   // owns the bonus-win celebration (panel + count-up + fountain)
  [SerializeField] private AudioManager audioManager;   // bonus music + shot/complete SFX

  [Header("Transition (scroll base machine out, bonus UI in)")]
  // Sibling movers: gameContentRoot (the base machine, GameContent) scrolls down/out while
  // pinballSpecialUI scrolls in from one screen above. Two synced tweens, same duration + ease.
  [SerializeField] private RectTransform gameContentRoot;   // = GameContent; scrolls down (machine visible, faded base UI rides along)
  [SerializeField] private RectTransform pinballSpecialUI;  // = PinballSpecialUI root; scrolls in from the top
  [SerializeField] private CanvasGroup baseGameUI;          // wrapper around the surrounding base UI that fades out (not the machine)
  [SerializeField] private CanvasGroup bonusDecorations;    // ring/ships/marbles/counters that fade in after scroll
  // Bonus graphics that must sit ON TOP of the start-prompt dark overlay (paylines graphic, Total Bet /
  // Bonus Win graphics + text). Their own parent, placed AFTER BonusStartPrompt in the hierarchy so it
  // renders above the overlay; fades in/out at the same time as bonusDecorations.
  [SerializeField] private CanvasGroup bonusFrontDecorations;
  [SerializeField] private float scrollDistance = 1080f;
  [SerializeField] private float scrollDuration = 1f;
  [SerializeField] private Ease scrollEase = Ease.InOutCubic;
  [SerializeField] private float fadeDuration = 0.4f;

  [Header("Bonus Start Prompt")]
  // Fades in at the end of the scroll: a dark overlay + the "BONUS TRIGGERED / PRESS BUTTON TO START"
  // graphic + the start button, all children of ONE CanvasGroup so a single fade brings them in together
  // (the overlay is the backmost child, so it darkens the bonus board behind the prompt). Pressing
  // bonusStartButton fades it back out and hands control to the shot loop. Leave bonusStartGroup null to
  // skip the gate entirely.
  [SerializeField] private CanvasGroup bonusStartGroup;
  [SerializeField] private Button bonusStartButton;
  [SerializeField] private float startPromptFadeDuration = 0.4f;
  [SerializeField] private float startButtonPulseScale = 1.1f;      // peak scale of the pulsing start button
  [SerializeField] private float startButtonPulseDuration = 0.7f;   // half-cycle time (slow ease in/out)
  // The same group is reused at bonus end; these two content parents toggle which text it shows. At the
  // start bonusTriggeredText is active ("...PRESS BUTTON TO START"); at the end bonusWinTotal is active
  // (its "TOTAL WIN" / amount / "BONUS COMPLETE" texts). Only one is active at a time.
  [SerializeField] private GameObject bonusTriggeredText;
  [SerializeField] private GameObject bonusWinTotal;
  [SerializeField] private TMP_Text bonusWinTotalAmount;   // the dynamic win amount inside bonusWinTotal
  [SerializeField] private float bonusWinOverlayHold = 2f;  // end overlay auto-dismisses after this many seconds (no button)

  [Header("Bonus Intro (UFO chase + ball entry)")]
  // After Start: all UFOs run a rotating chase-light flourish (shared lit/dim sprite swap), then the ball
  // rolls backwards around the outer ring to the first circle. ufoChaseSteps is authored per-frame: each
  // step lists the UFOs lit on that frame (all others dimmed); the steps cycle to make the line rotate.
  [SerializeField] private Sprite ufoLitSprite;
  [SerializeField] private Sprite ufoDimSprite;
  [SerializeField] private List<UfoChaseStep> ufoChaseSteps = new List<UfoChaseStep>();
  [SerializeField] private float chaseStepInterval = 0.1f;   // time each chase frame is held
  [SerializeField] private float ufoChaseDuration = 1.5f;    // total chase length before the ball rolls in

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
  // The first N entries of outerPath are chute-marble graphics, not plain circles: identical travel/trail
  // behaviour, but they swap the shared marbleLit/marbleUnlit sprites (below) instead of the circle pair.
  [SerializeField] private int outerMarbleLeadCount = 3;

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
  [SerializeField] private float prizeFlashHalfCycle = 0.15f;     // on/off duration of each blink
  [SerializeField] private float endHoldDuration = 1.5f;

  // Runtime state
  private int _betIndex;
  private double _totalBet;
  private double _totalBonusWin;
  private int _shotsRemaining;
  private bool _featureActive;
  private bool _shotInFlight;
  private bool _startPressed;
  private Tween _startPulse;
  private Vector3 _startButtonBaseScale;
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
    if (bonusStartButton)
    {
      bonusStartButton.onClick.RemoveListener(OnStartPressed);
      bonusStartButton.onClick.AddListener(OnStartPressed);
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
    if (audioManager) audioManager.PlayBonusBgMusic();
    UpdateShotsAmount(_shotsRemaining);
    UpdateBonusWinAmount(0);
    if (totalBetAmount) totalBetAmount.text = _totalBet.ToString("F2");
    RefreshPrizeLabels();
    RefreshPlusShotLabels();
    ClearAllLights();
    SetShootInteractable(false);

    yield return StartCoroutine(TransitionToBonus());

    yield return StartCoroutine(ShowBonusOverlayAndWait(bonusTriggeredText, bonusWinTotal));

    yield return StartCoroutine(PlayBonusIntro());

    SetShootInteractable(_shotsRemaining > 0);
  }

  // After Start is pressed: the UFOs run a rotating chase-light flourish, then the ball rolls backwards
  // around the outer ring to the first circle (the shoot point), ready for the first shot.
  private IEnumerator PlayBonusIntro()
  {
    if (audioManager) audioManager.PlayBonusAnimation();
    yield return StartCoroutine(UfoChaseRoutine());
    yield return StartCoroutine(LightOuterPathReverseWithTrail());
  }

  // Rotating "line of lit UFOs": each authored step lights its own set of UFOs (all others dimmed); the
  // steps cycle for ufoChaseDuration, then every UFO is restored to its default lit sprite.
  private IEnumerator UfoChaseRoutine()
  {
    if (ufoChaseSteps == null || ufoChaseSteps.Count == 0 || ufos == null) yield break;

    float elapsed = 0f;
    int step = 0;
    while (elapsed < ufoChaseDuration)
    {
      ApplyChaseStep(ufoChaseSteps[step]);
      step = (step + 1) % ufoChaseSteps.Count;
      yield return new WaitForSeconds(chaseStepInterval);
      elapsed += chaseStepInterval;
    }
    SetAllUfos(true);   // restore default (lit)
  }

  private void ApplyChaseStep(UfoChaseStep s)
  {
    SetAllUfos(false);   // dim everything first
    if (s?.litUfos == null || ufoLitSprite == null) return;
    foreach (Ufo u in s.litUfos)
      if (u && u.image) u.image.sprite = ufoLitSprite;   // light just this frame's set
  }

  private void SetAllUfos(bool lit)
  {
    Sprite sp = lit ? ufoLitSprite : ufoDimSprite;
    if (sp == null || ufos == null) return;
    foreach (Ufo u in ufos)
      if (u && u.image) u.image.sprite = sp;
  }

  // Shows the reused BonusStartGroup overlay with one of its content parents active — the start prompt at
  // the beginning, the win-total block at the end — and fades it in (dark overlay + frame + button in one
  // CanvasGroup). If autoDismissHold > 0 (the end) it just holds that long then dismisses; otherwise (the
  // start) it pulses the button and waits for the press. No-op if the group isn't wired.
  private IEnumerator ShowBonusOverlayAndWait(GameObject activeContent, GameObject inactiveContent, float autoDismissHold = 0f)
  {
    if (bonusStartGroup == null) yield break;

    if (activeContent) activeContent.SetActive(true);
    if (inactiveContent) inactiveContent.SetActive(false);

    _startPressed = false;
    bonusStartGroup.gameObject.SetActive(true);
    bonusStartGroup.alpha = 0f;
    bonusStartGroup.interactable = true;
    bonusStartGroup.blocksRaycasts = true;   // overlay soaks up clicks
    yield return bonusStartGroup.DOFade(1f, startPromptFadeDuration).WaitForCompletion();

    if (autoDismissHold > 0f)
    {
      yield return new WaitForSeconds(autoDismissHold);
    }
    else if (bonusStartButton)
    {
      StartStartButtonPulse();
      yield return new WaitUntil(() => _startPressed);
      StopStartButtonPulse();
    }
    else
    {
      Debug.LogWarning("[PinballBonus] bonusStartButton not assigned — start overlay can't be dismissed by a press; continuing.");
    }

    bonusStartGroup.gameObject.SetActive(false);   // instant off, no fade-out
  }

  private void OnStartPressed() => _startPressed = true;

  // Smooth "breathing" pulse on the start button: yoyo scale with a slow InOutSine ease so it eases in
  // and out naturally. Base scale is captured/restored so repeat bonuses don't drift the size.
  private void StartStartButtonPulse()
  {
    if (bonusStartButton == null) return;
    StopStartButtonPulse();
    Transform t = bonusStartButton.transform;
    _startButtonBaseScale = t.localScale;
    _startPulse = t.DOScale(_startButtonBaseScale * startButtonPulseScale, startButtonPulseDuration)
      .SetEase(Ease.InOutSine)
      .SetLoops(-1, LoopType.Yoyo);
  }

  private void StopStartButtonPulse()
  {
    if (_startPulse == null) return;
    _startPulse.Kill();
    _startPulse = null;
    if (bonusStartButton) bonusStartButton.transform.localScale = _startButtonBaseScale;
  }

  private IEnumerator EndBonus()
  {
    SetShootInteractable(false);
    yield return new WaitForSeconds(endHoldDuration);

    // Reuse the start overlay for the win summary: swap to the win-total block, fill the dynamic amount,
    // and hold — shown over the bonus board before we scroll back.
    if (audioManager) audioManager.PlayBonusComplete();
    if (bonusWinTotalAmount) bonusWinTotalAmount.text = _totalBonusWin.ToString("F2");
    yield return StartCoroutine(ShowBonusOverlayAndWait(bonusWinTotal, bonusTriggeredText, bonusWinOverlayHold));

    if (audioManager) audioManager.PlayBgMusic();   // restore the main-game music as we head back
    yield return StartCoroutine(TransitionFromBonus());
    // Scrolled back to the main game — hand the total to UIManager, which owns the win celebration
    // (panel + count-up + coin fountain). Fire-and-forget: it sets IsBonusWinActive, and StartSlots
    // blocks spins while that's true, so we don't need to wait here.
    if (uiManager) uiManager.PlayBonusWinSequence(_totalBonusWin);
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
    _totalBonusWin = p.bonusState.totalBonusWin;
    UpdateShotsAmount(_shotsRemaining);
    UpdateBonusWinAmount(_totalBonusWin);

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
    // Ticking loops for the whole journey; it's stopped (and a landing sound played) when the ball arrives.
    if (audioManager) audioManager.PlayBallTick();

    // 1. Outer loop, always the same, straight through — lit as a shrinking comet trail.
    yield return LightOuterPathWithTrail();

    // 2a. Chute — approach the marble entry, then collect marbles[selectedIndex] (it stays lit).
    if (isChute)
    {
      yield return LightSequence(PickRoute(marbleApproachRoutes)?.circles);
      ClearLight();
      if (audioManager) { audioManager.StopBallTick(); audioManager.PlayBallStop(); }
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
      if (audioManager) { audioManager.StopBallTick(); audioManager.PlayBallStop(); }
      yield return FlashPrize(ufo.group);
      ClearLight();
      yield break;
    }

    if (audioManager) audioManager.StopBallTick();
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
        bool lit = j >= i - trail && j <= i + trail;
        SetOuterSprite(j, lit);
      }
      yield return new WaitForSeconds(perCircleLightDuration);
    }
    _litCircle = outerPath[n - 1];   // single remaining lit circle; the inner route picks up from here
  }

  // Lights or unlights one outer-ring entry. The first outerMarbleLeadCount entries are marble graphics
  // (shared marbleLit/marbleUnlit sprites); the rest are plain circles. One place owns that branch.
  private void SetOuterSprite(int index, bool lit)
  {
    if (outerPath == null || index < 0 || index >= outerPath.Count) return;
    Image img = outerPath[index];
    if (!img) return;
    bool isMarble = index < outerMarbleLeadCount;
    if (lit) img.sprite = isMarble ? marbleLitSprite : circleLitSprite;
    else img.sprite = isMarble ? marbleUnlitSprite : circleUnlitSprite;
  }

  // Ball entry: rolls the light backwards around the outer ring (last circle -> first/shoot circle) with
  // the same comet trail, but shrinking toward the FIRST circle so it settles there as a single dot,
  // ready for the first shot. Mirror of LightOuterPathWithTrail.
  private IEnumerator LightOuterPathReverseWithTrail()
  {
    if (outerPath == null || outerPath.Count == 0) yield break;
    int n = outerPath.Count;
    for (int i = n - 1; i >= 0; i--)
    {
      float progress = n > 1 ? (float)(n - 1 - i) / (n - 1) : 1f;   // 0 at the start (i=n-1) -> 1 at the shoot circle (i=0)
      int trail = Mathf.RoundToInt(outerTrailMax * (1f - progress));
      for (int j = 0; j < n; j++)
      {
        bool lit = j >= i - trail && j <= i + trail;
        SetOuterSprite(j, lit);
      }
      yield return new WaitForSeconds(perCircleLightDuration);
    }
    // outerPath[0] is left lit — the ball resting at the shoot point.
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
    if (m != null) yield return FlashPrize(m.group);   // celebrate the collect; the marble stays lit after
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

  // Hard on/off blink on the landed prize (UFO or collected marble): toggles the CanvasGroup's alpha so
  // the whole cluster — graphic, prize label, and (UFO) the +1 Shot label — flashes together, without a
  // GameObject SetActive (no tweens killed, no OnEnable/OnDisable churn). Ends visible (alpha 1).
  private IEnumerator FlashPrize(CanvasGroup group)
  {
    if (group == null) yield break;
    group.alpha = 1f;
    for (int i = 0; i < prizeFlashCount; i++)
    {
      group.alpha = 0f;
      yield return new WaitForSeconds(prizeFlashHalfCycle);
      group.alpha = 1f;
      yield return new WaitForSeconds(prizeFlashHalfCycle);
    }
  }

  private void ClearAllLights()
  {
    ClearLight();
    if (outerPath != null)
      for (int i = 0; i < outerPath.Count; i++)
        SetOuterSprite(i, false);
    // UFOs start every bonus unlit; the intro chase lights them and they stay lit afterwards. Reset the
    // sprite here too (not just visibility) so a repeat bonus doesn't inherit the previous run's lit UFOs,
    // and reset the CanvasGroup alpha to undo any interrupted score-blink.
    if (ufos != null)
      foreach (Ufo u in ufos)
        if (u != null)
        {
          if (u.group) u.group.alpha = 1f;
          if (u.image && ufoDimSprite) u.image.sprite = ufoDimSprite;
        }
    // Reset the chute: every marble back to uncollected (unlit) + full alpha. Called at bonus start, so
    // the accumulator starts empty; during a bonus, collected marbles are intentionally left lit.
    if (marbles != null)
      foreach (Marble m in marbles)
        if (m != null)
        {
          if (m.group) m.group.alpha = 1f;
          if (m.image && marbleUnlitSprite) m.image.sprite = marbleUnlitSprite;
        }
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
    if (bonusFrontDecorations) bonusFrontDecorations.alpha = 0f;

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

    // Bonus decorations and the on-top-of-overlay front graphics fade in together (same duration).
    Tween decoInTween = null;
    if (bonusDecorations)
    {
      bonusDecorations.blocksRaycasts = true;
      bonusDecorations.interactable = true;
      decoInTween = bonusDecorations.DOFade(1f, fadeDuration);
    }
    Tween frontInTween = null;
    if (bonusFrontDecorations)
    {
      bonusFrontDecorations.blocksRaycasts = true;
      bonusFrontDecorations.interactable = true;
      frontInTween = bonusFrontDecorations.DOFade(1f, fadeDuration);
    }
    if (decoInTween != null) yield return decoInTween.WaitForCompletion();
    else if (frontInTween != null) yield return frontInTween.WaitForCompletion();
  }

  private IEnumerator TransitionFromBonus()
  {
    Tween decoOutTween = bonusDecorations ? bonusDecorations.DOFade(0f, fadeDuration) : null;
    Tween frontOutTween = bonusFrontDecorations ? bonusFrontDecorations.DOFade(0f, fadeDuration) : null;
    if (decoOutTween != null) yield return decoOutTween.WaitForCompletion();
    else if (frontOutTween != null) yield return frontOutTween.WaitForCompletion();
    if (bonusDecorations)
    {
      bonusDecorations.blocksRaycasts = false;
      bonusDecorations.interactable = false;
    }
    if (bonusFrontDecorations)
    {
      bonusFrontDecorations.blocksRaycasts = false;
      bonusFrontDecorations.interactable = false;
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

// One frame of the UFO chase: the UFOs lit on this frame (all others dimmed). Author a list of these on
// PinballBonusManager and the frames cycle to make the lit line rotate around the ring.
[System.Serializable]
public class UfoChaseStep
{
  [Tooltip("UFOs lit on this frame of the rotation; every other UFO is dimmed.")]
  public List<Ufo> litUfos = new List<Ufo>();
}
