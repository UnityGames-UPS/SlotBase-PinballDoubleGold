using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using System;
using System.Reflection;

public class SlotBehaviour : MonoBehaviour
{
  [Header("Sprites")]
  [SerializeField]
  private Sprite[] myImages;  //images taken initially

  [Header("Slot Images")]
  [SerializeField]
  private List<SlotImage> TempImages;     //class to store the result matrix

  [Header("Slots Transforms")]
  [SerializeField]
  private Transform[] Slot_Transform;

  [Header("Line Button Objects")]
  [SerializeField]
  private List<GameObject> StaticLine_Objects;

  [Header("Line Button Texts")]
  [SerializeField]
  private List<TMP_Text> StaticLine_Texts;

  [Header("Payline Graphics")]
  [SerializeField]
  private List<GameObject> PaylineGraphics;

  private int _hoverLineIndex = -1;

  [Header("Buttons")]
  [SerializeField]
  private Button Spin_Button;
  [SerializeField]
  private Button AutoSpin_Button;
  [SerializeField]
  private Button MaxBet_Button;
  [SerializeField]
  private Button TBetPlus_Button;
  [SerializeField]
  private Button TBetMinus_Button;
  [SerializeField] private Sprite SpinSprite;
  [SerializeField] private Sprite StopSprite;
  [SerializeField] private Sprite StopPressedSprite;
  [SerializeField] private Sprite SpinDeactivatedSprite;
  [SerializeField] private Sprite AutoSpinIdleSprite;
  [SerializeField] private Sprite AutoSpinActiveSprite;

  // Fixed display size applied to each slot image when its sprite is set.
  private static readonly Vector2 ScatterSymbolBaseSize = new Vector2(250f, 300f);

  [Header("Debug")]
  [SerializeField] private bool _animateAllSymbols = true;

  [Header("Miscellaneous UI")]
  [SerializeField]
  private TMP_Text BalanceAmount;
  [SerializeField]
  private TMP_Text TotalBet_text;
  [SerializeField]
  private TMP_Text LineBet_text;
  [SerializeField]
  private TMP_Text TotalWin_text;

  [Header("Audio Management")]
  [SerializeField]
  private AudioManager audioController;

  [SerializeField]
  private UIManager uiManager;

  [Header("Free Spin Trigger Anticipation")]
  [SerializeField] private float anticipationExtraSpinDuration = 1.5f;

  [Header("Pinball Bonus Trigger")]
  [SerializeField] private PinballBonusManager pinballBonusManager;
  // One entry per payline (0..8); activated when that line holds the three id-11 Pinball symbols.
  // Works whether each is a single image or a parent with child boxes/lines.
  [SerializeField] private List<GameObject> PinballLineGraphics;
  // Per-payline win-amount labels (indexed 0..8, same as PinballLineGraphics); each shows its line's payout.
  [SerializeField] private List<TMP_Text> PinballLineWinTexts;
  // Per-payline win-amount panels (black background behind PinballLineWinTexts), indexed 0..8. These are
  // normally always active for the per-payline win cycle; suppressed during the 3-pinball trigger flash
  // since that line has no win amount to show.
  [SerializeField] private List<GameObject> PinballLineWinPanels;
  [SerializeField] private float pinballFlashDuration = 2f;
  [SerializeField] private float pinballFlashHalfCycle = 0.3f;
  [SerializeField] private float pinballFlashMinAlpha = 0.2f;

  [Header("Reel Motion")]
  // Reel travel speed in local units/second. Durations are derived from this so every move
  // (intro, loop, landing) runs at a constant speed regardless of distance.
  [SerializeField] private float reelSpeed = 8350f;

  // The looping spin sweeps TopY -> BottomY and snaps back. TopY - BottomY must be an exact
  // multiple of the spin band's icon pitch or the snap is visible.
  [SerializeField] private float ReelTopY;
  [SerializeField] private float ReelBottomY;
  [SerializeField] private float ReelRestY = 3393.2f;

  [SerializeField] private Ease landEase = Ease.OutBack;
  [SerializeField] private float landOvershoot = 0.9f;

  private readonly Tween[] reelTweens = new Tween[3];   // one per base column
  private Coroutine _paylineCycleCoroutine;
  [SerializeField] private float paylineHoldDuration = 1.5f;
  [SerializeField] private float paylineAllTogetherDuration = 1.5f;

  [SerializeField]
  private List<ImageAnimation> TempList;  //stores the sprites whose animation is running at present 

  [SerializeField]
  private SocketIOManager SocketManager;

  private Coroutine AutoSpinRoutine = null;
  private Coroutine tweenroutine;
  private Tween BalanceTween;
  private Sprite _spinPressedSprite;
  internal bool IsAutoSpin = false;
  private bool IsSpinning = false;
  internal bool CheckPopups = false;
  internal int BetCounter = 0;
  private double currentBalance = 0;
  private double currentTotalBet = 0;
  // Total stake = lineBet (bets[BetCounter]) * betMultiplier. In SL-PDG the multiplier is the config
  // baseCoinValue (10), NOT the payline count (9) — the bets array is tuned so lineBet * 10 gives the
  // clean total bets (0.01->0.1, 0.05->0.5, 0.1->1.0, ...). Sourced from GameFeatures on init.
  private double betMultiplier = 1;
  private int numberOfSlots = 3;          //number of columns
  private int numberOfRows = 5;           //number of rows per column (3 real + 2 decorative edge rows)
  private bool StopSpinToggle;
  private float SpinDelay = 0.2f;
  [SerializeField] private float autoSpinExtraDelay = 0.0f;
  private float minSpinDuration = 1.5f;
  private int lastWinLineCount = 0;
  internal bool socketConnected = false;
  private int[,] initialMatrix = new int[,]
  {
    { 4, 4, 5 },
    { 0, 0, 0 },
    { 10, 10, 10 },
    { 0, 0, 0 },
    { 3, 3, 3 }
  };

  // Cached per-landed-spin 5-row display grid: server sends all 5 display rows in payload.reels
  // (rows 0/4 decorative, 1..3 active), copied across as-is (see BuildDisplayMatrix). Built once
  // when a spin lands; every other reader of "the matrix"
  // in this file should read from this instead of SocketManager.ResultData directly.
  private int[,] _displayMatrix;

  private void Awake()
  {
    ToggleButtonGrp(false);
    if (Spin_Button)
    {
      Spin_Button.interactable = false;
      Spin_Button.GetComponent<Image>().sprite = SpinDeactivatedSprite;
    }
  }

  private void Start()
  {
    IsAutoSpin = false;

    if (Spin_Button) _spinPressedSprite = Spin_Button.spriteState.pressedSprite;
    if (Spin_Button) Spin_Button.onClick.RemoveAllListeners();
    if (Spin_Button) Spin_Button.onClick.AddListener(OnSpinButtonPressed);

    if (TBetPlus_Button) TBetPlus_Button.onClick.RemoveAllListeners();
    if (TBetPlus_Button) TBetPlus_Button.onClick.AddListener(delegate { ChangeBet(true); });

    if (TBetMinus_Button) TBetMinus_Button.onClick.RemoveAllListeners();
    if (TBetMinus_Button) TBetMinus_Button.onClick.AddListener(delegate { ChangeBet(false); });

    if (MaxBet_Button) MaxBet_Button.onClick.RemoveAllListeners();
    if (MaxBet_Button) MaxBet_Button.onClick.AddListener(MaxBet);

    if (AutoSpin_Button) AutoSpin_Button.onClick.RemoveAllListeners();
    if (AutoSpin_Button) AutoSpin_Button.onClick.AddListener(AutoSpin);
  }


  #region Autospin
  private void AutoSpin()
  {
    if (!IsAutoSpin)
    {
      if (IsSpinning) return;
      if (currentBalance < currentTotalBet) { uiManager.LowBalPopup(); return; }
      IsAutoSpin = true;
      SetAutoSpinButtonSprite(true);
      if (AutoSpinRoutine != null) { StopCoroutine(AutoSpinRoutine); AutoSpinRoutine = null; }
      AutoSpinRoutine = StartCoroutine(AutoSpinCoroutine());
    }
    else
    {
      StopAutoSpin();
    }
  }

  private void StopAutoSpin()
  {
    if (IsAutoSpin)
    {
      IsAutoSpin = false;
      SetAutoSpinButtonSprite(false);
    }
  }

  private void SetAutoSpinButtonSprite(bool active)
  {
    if (AutoSpin_Button == null) return;
    var img = AutoSpin_Button.GetComponent<Image>();
    if (img == null) return;
    img.sprite = active ? AutoSpinActiveSprite : AutoSpinIdleSprite;
  }

  private IEnumerator AutoSpinCoroutine()
  {
    while (IsAutoSpin)
    {
      yield return new WaitUntil(() => !CheckPopups && !IsSpinning);
      if (currentBalance < currentTotalBet) { uiManager.LowBalPopup(); StopAutoSpin(); break; }
      StartSlots(true);
      yield return new WaitUntil(() => !IsSpinning);
      yield return new WaitForSeconds(SpinDelay + autoSpinExtraDelay);
    }
    yield return new WaitUntil(() => !IsSpinning);
    ToggleButtonGrp(true);
  }
  #endregion

  private void CompareBalance()
  {
    if (currentBalance < currentTotalBet)
    {
      uiManager.LowBalPopup();
    }
  }

  #region LinesCalculation
  //Fetch Lines from backend
  internal void FetchLines(string LineVal, int count)
  {
    if (StaticLine_Texts.Count > count) StaticLine_Texts[count].text = (count + 1).ToString();
    if (StaticLine_Objects.Count > count) StaticLine_Objects[count].SetActive(true);
  }

  //Generate Static Lines from button hovers
  internal void GenerateStaticLine(TMP_Text LineID_Text)
  {
    DestroyStaticLine();
    int LineID = 1;
    try
    {
      LineID = int.Parse(LineID_Text.text);
    }
    catch (Exception e)
    {
      Debug.Log("Exception while parsing " + e.Message);
    }
    int index = LineID - 1;
    if (PaylineGraphics.Count > index)
    {
      PaylineGraphics[index].SetActive(true);
      StartGameAnimation(PaylineGraphics[index]);
      _hoverLineIndex = index;
    }
  }

  //Destroy Static Lines from button hovers
  internal void DestroyStaticLine()
  {
    if (_hoverLineIndex >= 0 && PaylineGraphics.Count > _hoverLineIndex)
    {
      PaylineGraphics[_hoverLineIndex].SetActive(false);
    }
    _hoverLineIndex = -1;
  }
  #endregion

  private void MaxBet()
  {
    if (audioController) audioController.PlayButton();
    BetCounter = SocketManager.InitialData.bets.Count - 1;
    if (LineBet_text) LineBet_text.text = SocketManager.InitialData.bets[BetCounter].ToString();
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * betMultiplier).ToString();
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * betMultiplier;
    uiManager.SetBet(currentTotalBet);
  }

  private void ChangeBet(bool IncDec)
  {
    if (audioController) audioController.PlayBetButton();
    if (IncDec)
    {
      BetCounter++;
      if (BetCounter >= SocketManager.InitialData.bets.Count)
      {
        BetCounter = 0; // Loop back to the first bet
      }
    }
    else
    {
      BetCounter--;
      if (BetCounter < 0)
      {
        BetCounter = SocketManager.InitialData.bets.Count - 1; // Loop to the last bet
      }
    }
    if (LineBet_text) LineBet_text.text = SocketManager.InitialData.bets[BetCounter].ToString();
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * betMultiplier).ToString();
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * betMultiplier;
    uiManager.SetBet(currentTotalBet);
  }

  #region InitialFunctions
  // internal void shuffleInitialMatrix()
  // {
  //   for (int i = 0; i < TempImages.Count; i++)
  //   {
  //     for (int j = 0; j < 3; j++)
  //     {
  //       int randomIndex = UnityEngine.Random.Range(0, 14);
  //       TempImages[i].slotImages[j].sprite = myImages[randomIndex];
  //     }
  //   }
  // }


  // The server now sends all numberOfRows display rows directly in payload.reels (reels[row][col]):
  // rows 0 and numberOfRows-1 are the decorative top/bottom rows, rows 1..3 the active rows. So we just
  // copy them straight across — no client-side decorative synthesis, and no 1-centered/2-gapped rule to
  // police (the backend owns the whole grid, including trigger/diagonal spins that used to break it).
  private int[,] BuildDisplayMatrix(List<List<string>> reels)
  {
    int[,] display = new int[numberOfRows, numberOfSlots];
    for (int row = 0; row < numberOfRows; row++)
      for (int col = 0; col < numberOfSlots; col++)
        display[row, col] = int.Parse(reels[row][col]);
    return display;
  }

  internal void InitializeMatrix()
  {
    for (int row = 0; row < initialMatrix.GetLength(0); row++)
    {
      for (int col = 0; col < initialMatrix.GetLength(1); col++)
      {
        // initialMatrix is a mask: cells preset to Blank (0) stay Blank; every other cell shows a
        // random non-Blank symbol, so the board looks like a fresh random spin at startup. With the
        // current preset that means rows 0/2/4 are randomised and rows 1/3 stay fully Blank.
        int val = initialMatrix[row, col] == 0 ? 0 : UnityEngine.Random.Range(1, myImages.Length);
        TempImages[col].slotImages[row].sprite = myImages[val];
      }
    }
  }


  internal void SetInitialUI()
  {
    socketConnected = true;
    BetCounter = 0;
    betMultiplier = SocketManager.GameFeatures.baseCoinValue;
    if (LineBet_text) LineBet_text.text = SocketManager.InitialData.bets[BetCounter].ToString();
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * betMultiplier).ToString();
    if (TotalWin_text) TotalWin_text.text = "0.000";
    if (BalanceAmount) BalanceAmount.text = SocketManager.PlayerData.balance.ToString("F3");
    currentBalance = SocketManager.PlayerData.balance;
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * betMultiplier;
    CompareBalance();
    uiManager.InitialiseUI(SocketManager.InitialData.bets, SocketManager.UIData.paylines.symbols, SocketManager.GameFeatures?.anyPayouts, SocketManager.GameFeatures?.baseCoinValue ?? 1);
    uiManager.SetBet(currentTotalBet);
    // Game has connected — enable the buttons (they start disabled in Awake). This is the one thing
    // the old intro sequence used to do at its end; the intro itself has been removed.
    ToggleButtonGrp(true);
  }
  #endregion

  // Backend-pushed balance correction (balance:sync). Snapped, not tweened — this is an
  // external correction, not a spin result.
  internal void UpdateBalanceDisplay(double newBalance)
  {
    BalanceTween?.Kill();
    currentBalance = newBalance;
    if (BalanceAmount) BalanceAmount.text = newBalance.ToString("F3");
    CompareBalance();
  }

  #region SlotSpin
  private void OnSpinButtonPressed()
  {
    if (audioController) audioController.PlayButton();
    if (IsSpinning)
      OnStopSpinPressed();
    else
      StartSlots();
  }

  private void OnStopSpinPressed()
  {
    StopSpinToggle = true;
    if (Spin_Button)
    {
      Spin_Button.GetComponent<Image>().sprite = SpinDeactivatedSprite;
      Spin_Button.interactable = false;
    }
  }

  //starts the spin process
  private void StartSlots(bool autoSpin = false)
  {
    // Don't let a spin interrupt the bonus-win celebration (it fires after the bonus scrolls back and
    // runs over the main game). Scoped to the bonus win only — base spin/big wins keep spin-to-skip below.
    if (uiManager.IsBonusWinActive) return;

    uiManager.SkipWinSequences();

    if (TotalWin_text) TotalWin_text.text = "0.000";

    if (!autoSpin)
    {
      if (AutoSpinRoutine != null)
      {
        StopCoroutine(AutoSpinRoutine);
        StopCoroutine(tweenroutine);
        tweenroutine = null;
        AutoSpinRoutine = null;
      }
    }
    if (TempList.Count > 0)
    {
      StopGameAnimation();
    }
    if (_paylineCycleCoroutine != null)
    {
      StopCoroutine(_paylineCycleCoroutine);
      _paylineCycleCoroutine = null;
    }
    for (int i = 0; i < PaylineGraphics.Count; i++)
    {
      PaylineGraphics[i].SetActive(false);
    }
    // The Phase-2 cycle uses the pinball line graphics — clear those too so an interrupted cycle
    // doesn't leave one active.
    if (PinballLineGraphics != null)
      for (int i = 0; i < PinballLineGraphics.Count; i++)
        if (PinballLineGraphics[i]) PinballLineGraphics[i].SetActive(false);
    tweenroutine = StartCoroutine(TweenRoutine());
  }

  //manage the Routine for spinning of the slots
  private IEnumerator TweenRoutine()
  {
    if (currentBalance < currentTotalBet)
    {
      CompareBalance();
      StopAutoSpin();
      yield return new WaitForSeconds(1);
      ToggleButtonGrp(true);
      yield break;
    }
    if (audioController) audioController.PlaySpinLoop();

    IsSpinning = true;

    ActivateStopButton();
    for (int i = 0; i < numberOfSlots; i++)
    {
      InitializeReelSpin(Slot_Transform[i], i, ReelTopY, ReelBottomY, reelSpeed);
      yield return new WaitForSeconds(0.1f);
    }
    float spinStartTime = Time.time;

    BalanceDeduction();

    SocketManager.AccumulateResult(BetCounter);
    yield return new WaitUntil(() => SocketManager.isResultdone);

    _displayMatrix = BuildDisplayMatrix(SocketManager.ResultData.payload.reels);

    for (int i = 0; i < numberOfRows; i++)
    {
      for (int j = 0; j < numberOfSlots; j++)
      {
        int resultNum = _displayMatrix[i, j];
        TempImages[j].slotImages[i].sprite = myImages[resultNum];
        TempImages[j].slotImages[i].rectTransform.sizeDelta = ScatterSymbolBaseSize;
      }
    }

    for (int j = 0; j < numberOfSlots; j++)
    {
      bool isCaseA = _displayMatrix[0, j] != 0;
      float edgeRotation = isCaseA ? 50f : 10f;
      TempImages[j].slotImages[0].rectTransform.localEulerAngles = new Vector3(edgeRotation, 0, 0);
      TempImages[j].slotImages[numberOfRows - 1].rectTransform.localEulerAngles = new Vector3(-edgeRotation, 0, 0);
    }

    yield return new WaitUntil(() => StopSpinToggle || Time.time - spinStartTime >= minSpinDuration);

    for (int i = 0; i < 5; i++)
    {
      yield return null;
      if (StopSpinToggle)
      {
        break;
      }
    }

    // Pinball bonus is authoritative from the backend flag (not from client scatter counting).
    bool willTriggerPinball = SocketManager.ResultData.payload.features.pinball.triggered;

    if (willTriggerPinball && IsAutoSpin && AutoSpinRoutine != null)
    {
      StopCoroutine(AutoSpinRoutine);
      AutoSpinRoutine = null;
    }

    for (int i = 0; i < numberOfSlots; i++)
    {
      // On a pinball-trigger spin the last reel holds back and spins longer to build tension
      // (PDG keeps the "last reel spins longer" beat from the old game, but drops the zoom/glow).
      if (willTriggerPinball && i == numberOfSlots - 1)
        yield return new WaitForSeconds(anticipationExtraSpinDuration);

      yield return StopBaseReel(i);
    }

    StopSpinToggle = false;
    // Base columns share a speed and start landing in order, so the last one finishes last.
    yield return reelTweens[numberOfSlots - 1].WaitForCompletion();

    if (Spin_Button)
    {
      Spin_Button.GetComponent<Image>().sprite = SpinDeactivatedSprite;
      Spin_Button.interactable = false;
    }

    KillAllTweens();
    if (audioController) audioController.StopSpinLoop();

    if (_animateAllSymbols)
    {
      for (int i = 0; i < numberOfRows; i++)
      {
        for (int j = 0; j < numberOfSlots; j++)
        {
          StartGameAnimation(TempImages[j].slotImages[i].gameObject);
        }
      }
    }

    if (SocketManager.ResultData.payload.totalWin > 0)
    {
      SpinDelay = 3f;
    }
    else
    {
      SpinDelay = 2f;
    }

    if (SocketManager.ResultData.payload.totalWin > 0)
    {
      List<int> winLine = new();
      foreach (var item in SocketManager.ResultData.payload.winningLines)
      {
        winLine.Add(item.lineIndex);
      }
      lastWinLineCount = winLine.Count;
      CheckPayoutLineBackend(winLine, willTriggerPinball: willTriggerPinball);
    }
    else
    {
      lastWinLineCount = 0;
    }

    CheckPopups = true;

    if (TotalWin_text)
    {
      TotalWin_text.text = SocketManager.ResultData.payload.totalWin.ToString("F3");
    }
    BalanceTween?.Kill();
    if (BalanceAmount) BalanceAmount.text = SocketManager.ResultData.player.balance.ToString("F3");

    currentBalance = SocketManager.PlayerData.balance;

    uiManager.PlaySpinWin(SocketManager.ResultData.payload.totalWin);

    CheckWinPopups();

    yield return new WaitUntil(() => !CheckPopups);
    if (willTriggerPinball)
    {
      // Leave IsSpinning true and buttons disabled — the bonus manager owns game state from here
      // and restores the base game via OnBonusComplete() when the feature ends.
    }
    else if (!IsAutoSpin)
    {
      ToggleButtonGrp(true);
      IsSpinning = false;
    }
    else
    {
      // yield return new WaitForSeconds(2f);
      IsSpinning = false;
    }

    if (willTriggerPinball)
    {
      // Let any other-line wins (all-together display + total win amount) finish playing naturally —
      // no individual per-line cycle for a pinball-trigger spin — before flashing the pinball line.
      if (_paylineCycleCoroutine != null) yield return _paylineCycleCoroutine;
      yield return new WaitUntil(() => !uiManager.IsWinSequenceActive);

      // Flash the winning pinball line(s), then hand off to the bonus manager, which runs the
      // base->bonus transition and the per-press shot loop.
      if (audioController) audioController.PlayThreePinballsFlash();
      List<int> pinballLines = FindPinballLines();
      yield return StartCoroutine(FlashPinballTrigger(pinballLines));
      int startShots = SocketManager.ResultData.payload.features.pinball.shotsRemaining;
      if (pinballBonusManager)
        pinballBonusManager.BeginBonus(startShots, BetCounter, currentTotalBet);
      else
        Debug.LogWarning("[PinballBonus] pinballBonusManager reference not assigned — cannot start the bonus.");
    }

  }

  private void BalanceDeduction()
  {
    double bet = 0;
    double balance = 0;
    try
    {
      bet = double.Parse(TotalBet_text.text);
    }
    catch (Exception e)
    {
      Debug.Log("Error while conversion " + e.Message);
    }

    try
    {
      balance = double.Parse(BalanceAmount.text);
    }
    catch (Exception e)
    {
      Debug.Log("Error while conversion " + e.Message);
    }
    double initAmount = balance;

    balance = balance - bet;

    BalanceTween = DOTween.To(() => initAmount, (val) => initAmount = val, balance, 0.8f).OnUpdate(() =>
    {
      if (BalanceAmount) BalanceAmount.text = initAmount.ToString("F3");
    });
  }

  internal void CheckWinPopups()
  {
    CheckPopups = false;
  }

  #region PinballBonusTrigger
  // Backend paylines (InitialData.lines) are indexed against the 3 active reel rows (0..2), while the
  // 5-row display puts the active rows at 1..3 (rows 0/4 are decorative). So a payline row maps to
  // _displayMatrix / slotImages row +1. Confirmed against live winningLines: lines[0]=[1,1,1] (active
  // row 1) lands at display row 2. Used by the pinball-trigger scan/flash and the base-game win
  // highlight (CheckPayoutLineBackend). NOTE: winningLines[].positions are already in display (0..4)
  // space — do NOT PaddedRow those; only the lines[] values need the offset.
  private int PaddedRow(int backendLineRow) => backendLineRow + 1;

  // Returns every active payline whose three positions all hold the Pinball symbol (id 11).
  // Normally one line; all matches are returned so the caller can flash them all (multi-line
  // behaviour pending team-lead confirmation).
  private List<int> FindPinballLines()
  {
    List<int> result = new();
    var lines = SocketManager.InitialData.lines;
    for (int li = 0; li < lines.Count; li++)
    {
      var rows = lines[li];
      bool allPinball = true;
      for (int col = 0; col < rows.Count; col++)
      {
        if (_displayMatrix[PaddedRow(rows[col]), col] != 11) { allPinball = false; break; }
      }
      if (allPinball) result.Add(li);
    }
    return result;
  }

  // Flashes the winning pinball line graphic(s) and the id-11 symbols on those lines (alpha
  // fade in/out) for ~pinballFlashDuration, then restores them, before the bonus transition.
  private IEnumerator FlashPinballTrigger(List<int> lines)
  {
    if (lines == null || lines.Count == 0)
    {
      Debug.LogWarning("[PinballBonus] Backend triggered the bonus but no active line has three id-11 symbols — skipping flash.");
      yield break;
    }

    List<Image> symbolImages = new();
    List<CanvasGroup> lineGroups = new();

    foreach (int line in lines)
    {
      if (PinballLineGraphics != null && line >= 0 && line < PinballLineGraphics.Count && PinballLineGraphics[line])
      {
        GameObject g = PinballLineGraphics[line];
        g.SetActive(true);
        // Needs a CanvasGroup to flash (works whether the graphic is one image or a parent of boxes/lines).
        CanvasGroup cg = g.GetComponent<CanvasGroup>();
        if (cg) lineGroups.Add(cg);
        else Debug.LogWarning($"[PinballBonus] Line graphic '{g.name}' has no CanvasGroup — it'll show but won't flash. Add one to enable the flash.");
      }
      // The 3-pinball line has no win amount — hide its (normally always-active) win panel during the flash.
      if (PinballLineWinPanels != null && line >= 0 && line < PinballLineWinPanels.Count && PinballLineWinPanels[line])
        PinballLineWinPanels[line].SetActive(false);
      var rows = SocketManager.InitialData.lines[line];
      for (int col = 0; col < numberOfSlots; col++)
      {
        Image img = TempImages[col].slotImages[PaddedRow(rows[col])];
        if (img && !symbolImages.Contains(img)) symbolImages.Add(img);
      }
    }

    // Yoyo fade for the flash window; even loop count returns targets to full alpha.
    int halfCycles = Mathf.Max(2, Mathf.RoundToInt(pinballFlashDuration / pinballFlashHalfCycle));
    if (halfCycles % 2 != 0) halfCycles++;
    foreach (Image img in symbolImages)
      img.DOFade(pinballFlashMinAlpha, pinballFlashHalfCycle).SetLoops(halfCycles, LoopType.Yoyo);
    // Inverse of the symbol flash: starts dim and fades to full, so it's at min alpha exactly when
    // the symbols are at full alpha, and vice versa.
    foreach (CanvasGroup cg in lineGroups)
    {
      cg.alpha = pinballFlashMinAlpha;
      cg.DOFade(1f, pinballFlashHalfCycle).SetLoops(halfCycles, LoopType.Yoyo);
    }

    yield return new WaitForSeconds(halfCycles * pinballFlashHalfCycle);

    // Restore and clean up so nothing lingers when the machine scrolls away / back.
    foreach (Image img in symbolImages) { img.DOKill(); Color c = img.color; c.a = 1f; img.color = c; }
    foreach (CanvasGroup cg in lineGroups) { cg.DOKill(); cg.alpha = 1f; }
    foreach (int line in lines)
    {
      if (PinballLineGraphics != null && line >= 0 && line < PinballLineGraphics.Count && PinballLineGraphics[line])
        PinballLineGraphics[line].SetActive(false);
      // Restore the win panel so future normal per-payline win cycles show their amounts again.
      if (PinballLineWinPanels != null && line >= 0 && line < PinballLineWinPanels.Count && PinballLineWinPanels[line])
        PinballLineWinPanels[line].SetActive(true);
    }
  }

  // Called by PinballBonusManager once the feature ends and the machine has scrolled back in.
  // Backend already credited the balance on the isOver shot, so just resync and re-enable play.
  internal void OnBonusComplete()
  {
    currentBalance = SocketManager.PlayerData.balance;
    if (BalanceAmount) BalanceAmount.text = SocketManager.PlayerData.balance.ToString("F3");
    if (TotalWin_text) TotalWin_text.text = "0.000";
    IsSpinning = false;
    ToggleButtonGrp(true);
    CompareBalance();
  }
  #endregion

  private GameObject GetSlotImageGameObject(int row, int column)
  {
    return TempImages[column].slotImages[row].gameObject;
  }

  private IEnumerator CyclePaylines(List<int> lineIds, Dictionary<int, List<KeyValuePair<int, int>>> lineCoords, bool loopIndividualLines = true)
  {
    // Phase 1: show every winning line together, all symbol animations playing at once.
    List<ImageAnimation> allAnims = new();
    foreach (int id in lineIds)
    {
      if (PaylineGraphics.Count > id)
      {
        PaylineGraphics[id].SetActive(true);
        StartGameAnimation(PaylineGraphics[id]);
      }
      foreach (var coord in lineCoords[id])
      {
        GameObject symbolObj = GetSlotImageGameObject(coord.Key, coord.Value);
        ImageAnimation anim = symbolObj.GetComponent<ImageAnimation>();
        StartGameAnimation(symbolObj);
        if (anim != null) allAnims.Add(anim);
      }
    }
    yield return new WaitForSeconds(paylineAllTogetherDuration);
    foreach (int id in lineIds)
      if (PaylineGraphics.Count > id) PaylineGraphics[id].SetActive(false);
    foreach (var anim in allAnims) anim.StopAnimation();

    // Pinball-trigger spins only show the all-together phase above, then hand off to the pinball
    // flash — no individual per-line cycle.
    if (!loopIndividualLines) yield break;

    // Phase 2: one winning line at a time, looping. Uses the special pinball line graphics (shown
    // statically for the hold); the line's winning symbols still animate.
    while (true)
    {
      foreach (int id in lineIds)
      {
        List<ImageAnimation> lineAnims = new();
        if (PinballLineGraphics != null && PinballLineGraphics.Count > id && PinballLineGraphics[id])
          PinballLineGraphics[id].SetActive(true);
        foreach (var coord in lineCoords[id])
        {
          GameObject symbolObj = GetSlotImageGameObject(coord.Key, coord.Value);
          ImageAnimation anim = symbolObj.GetComponent<ImageAnimation>();
          StartGameAnimation(symbolObj);
          if (anim != null) lineAnims.Add(anim);
        }
        yield return new WaitForSeconds(paylineHoldDuration);
        if (PinballLineGraphics != null && PinballLineGraphics.Count > id && PinballLineGraphics[id])
          PinballLineGraphics[id].SetActive(false);
        foreach (var anim in lineAnims) anim.StopAnimation();
      }
    }
  }

  //generate the payout lines generated
  private void CheckPayoutLineBackend(List<int> LineId, double jackpot = 0, bool willTriggerPinball = false)
  {
    if (LineId.Count > 0)
    {
      List<int> sortedIds = new List<int>(LineId);
      sortedIds.Sort();

      if (jackpot > 0)
      {
        for (int i = 0; i < TempImages.Count; i++)
        {
          for (int k = 0; k < TempImages[i].slotImages.Count; k++)
          {
            StartGameAnimation(GetSlotImageGameObject(k, i));
          }
        }
      }
      else
      {
        Dictionary<int, List<KeyValuePair<int, int>>> lineCoords = new();
        for (int j = 0; j < LineId.Count; j++)
        {
          List<KeyValuePair<int, int>> coords = new();
          for (int k = 0; k < SocketManager.ResultData.payload.winningLines[j].positions.Count; k++)
          {
            // Backend line rows are 0..2 (active rows only); the padded display puts them at 1..3.
            int rowIndex = PaddedRow(SocketManager.InitialData.lines[LineId[j]][k]);
            int columnIndex = k;
            coords.Add(new KeyValuePair<int, int>(rowIndex, columnIndex));
          }
          lineCoords[LineId[j]] = coords;
        }
        // Fill each winning line's own win-amount label (shown when it appears in the Phase-2 cycle).
        foreach (var wl in SocketManager.ResultData.payload.winningLines)
          if (PinballLineWinTexts != null && wl.lineIndex >= 0 && wl.lineIndex < PinballLineWinTexts.Count
              && PinballLineWinTexts[wl.lineIndex])
            PinballLineWinTexts[wl.lineIndex].text = TextFormat.ToSpriteDigits(wl.payout.ToString("F2"));
        _paylineCycleCoroutine = StartCoroutine(CyclePaylines(sortedIds, lineCoords, !willTriggerPinball));
      }
    }
  }

  #endregion

  internal void CallCloseSocket()
  {
    StartCoroutine(SocketManager.CloseSocket());
  }


  internal void ToggleButtonGrp(bool toggle)
  {
    bool active = toggle && !IsAutoSpin;
    if (Spin_Button)
    {
      Spin_Button.interactable = toggle ? active : true;
      if (toggle)
      {
        Spin_Button.GetComponent<Image>().sprite = SpinSprite;
        var ss = Spin_Button.spriteState;
        ss.pressedSprite = _spinPressedSprite;
        Spin_Button.spriteState = ss;
      }
    }
    if (AutoSpin_Button) AutoSpin_Button.interactable = toggle || IsAutoSpin;
    if (MaxBet_Button) MaxBet_Button.interactable = active;
    if (TBetMinus_Button) TBetMinus_Button.interactable = active;
    if (TBetPlus_Button) TBetPlus_Button.interactable = active;
  }

  private void ActivateStopButton()
  {
    ToggleButtonGrp(false);
    if (Spin_Button)
    {
      Spin_Button.GetComponent<Image>().sprite = StopSprite;
      var ss = Spin_Button.spriteState;
      ss.pressedSprite = StopPressedSprite;
      Spin_Button.spriteState = ss;
    }
  }

  //start the icons animation
  private void StartGameAnimation(GameObject animObjects)
  {
    if (!animObjects.activeInHierarchy) return;
    ImageAnimation temp = animObjects.GetComponent<ImageAnimation>();
    if (temp == null) return;
    temp.StartAnimation();
    TempList.Add(temp);
  }

  //stop the icons animation
  private void StopGameAnimation()
  {
    for (int i = 0; i < TempList.Count; i++)
    {
      TempList[i].StopAnimation();
    }
    TempList.Clear();
    TempList.TrimExcess();
  }


  #region TweeningCode
  // Time needed to travel between two Y positions at the given speed.
  private float DurationFor(float fromY, float toY, float speed)
    => Mathf.Abs(toY - fromY) / Mathf.Max(speed, 0.0001f);

  // Sweeps the reel down to bottomY at a constant speed, then loops topY -> bottomY forever.
  // The reel's icons are never touched, so no state accumulates across spins.
  private void InitializeReelSpin(Transform slotTransform, int index, float topY, float bottomY, float speed)
  {
    if (!slotTransform) return;
    reelTweens[index]?.Kill();

    float startY = slotTransform.localPosition.y;
    Sequence seq = DOTween.Sequence();
    seq.Append(slotTransform.DOLocalMoveY(bottomY, DurationFor(startY, bottomY, speed)).SetEase(Ease.Linear));
    seq.AppendCallback(() =>
    {
      slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, topY);
      reelTweens[index] = slotTransform.DOLocalMoveY(bottomY, DurationFor(topY, bottomY, speed))
        .SetLoops(-1, LoopType.Restart)
        .SetEase(Ease.Linear);
    });
    // Holding the intro sequence here means a Kill during the sweep stops the callback from
    // ever running, so the loop tween can't be orphaned.
    reelTweens[index] = seq;
  }

  private void StopReelSpin(Transform slotTransform, int index, float topY, float restY, float speed)
  {
    if (!slotTransform) return;
    reelTweens[index]?.Kill();
    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, topY);
    reelTweens[index] = slotTransform.DOLocalMoveY(restY, DurationFor(topY, restY, speed))
      .SetEase(landEase, landOvershoot);
  }

  // Lands one base column and holds for the stagger between reels, unless the player has
  // pressed Stop, in which case the columns land back to back.
  private IEnumerator StopBaseReel(int index)
  {
    StopReelSpin(Slot_Transform[index], index, ReelTopY, ReelRestY, reelSpeed);
    if (audioController) audioController.PlayReelStop();
    // Once per reel even if it shows the Pinball icon more than once — not once per icon.
    if (audioController && ReelHasPinballIcon(index)) audioController.PlayPinballIconAppearsInReel();
    if (StopSpinToggle)
      yield return null;
    else
      yield return new WaitForSeconds(0.2f);
  }

  // True if reel column `col`'s landed strip (all 5 rows, including decorative) shows the Pinball symbol (id 11).
  private bool ReelHasPinballIcon(int col)
  {
    for (int row = 0; row < numberOfRows; row++)
      if (_displayMatrix[row, col] == 11) return true;
    return false;
  }

  private void KillAllTweens()
  {
    for (int i = 0; i < reelTweens.Length; i++)
    {
      reelTweens[i]?.Kill();
      reelTweens[i] = null;
    }
  }
  #endregion

}

[Serializable]
public class SlotImage
{
  public List<Image> slotImages = new List<Image>(10);
}