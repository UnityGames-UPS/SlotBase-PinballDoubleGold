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

  [Header("Animated Sprites")]
  [SerializeField]
  private Sprite[] Blank_Sprite;
  [SerializeField]
  private Sprite[] SingleBar_Sprite;
  [SerializeField]
  private Sprite[] DoubleBar_Sprite;
  [SerializeField]
  private Sprite[] TripleBar_Sprite;
  [SerializeField]
  private Sprite[] Bell_Sprite;
  [SerializeField]
  private Sprite[] Red7_Sprite;
  [SerializeField]
  private Sprite[] Wild2x_Sprite;
  [SerializeField]
  private Sprite[] Wild3x_Sprite;
  [SerializeField]
  private Sprite[] Wild5x_Sprite;
  [SerializeField]
  private Sprite[] Wild10x_Sprite;
  [SerializeField]
  private Sprite[] Scatter_Sprite;
  [SerializeField]
  private Sprite[] ScatterTrigger_Sprite;
  private static readonly Vector2 ScatterSymbolBaseSize = new Vector2(250f, 300f);

  [Header("Debug")]
  [SerializeField] private bool _animateAllSymbols = true;

  [Header("Miscellaneous UI")]
  [SerializeField]
  private TMP_Text Balance_text;
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

  [Header("Free Spin Special Reel")]
  [SerializeField] private GameObject SpecialReelObject;
  [SerializeField] private Transform SpecialReelTransform;
  [SerializeField] private GameObject MiddleReelObject;
  [SerializeField] private GameObject MiddleReelGlow;
  [SerializeField] private GameObject LastReelGlow;
  [SerializeField] internal GameObject FreeSpinSlotMachine;
  [SerializeField] private SlotImage SpecialReelSlotImages;
  private float specialReelSwapDelay = 2.3f;

  [Header("Reel Motion")]
  // Reel travel speed in local units/second. Durations are derived from this so every move
  // (intro, loop, landing) runs at a constant speed regardless of distance.
  [SerializeField] private float reelSpeed = 8350f;
  [SerializeField] private float specialReelSpeed = 1670f;

  // The looping spin sweeps TopY -> BottomY and snaps back. TopY - BottomY must be an exact
  // multiple of the spin band's icon pitch or the snap is visible.
  [SerializeField] private float ReelTopY;
  [SerializeField] private float ReelBottomY;
  [SerializeField] private float ReelRestY = 3393.2f;

  [SerializeField] private float SpecialReelTopY;
  [SerializeField] private float SpecialReelBottomY;
  [SerializeField] private float SpecialReelRestY = 3393.2f;

  [SerializeField] private Ease landEase = Ease.OutBack;
  [SerializeField] private float landOvershoot = 0.9f;

  // Base columns occupy 0..numberOfSlots-1; the free-spin special column gets its own slot.
  private const int SpecialReelIndex = 3;
  private readonly Tween[] reelTweens = new Tween[4];
  private Coroutine _paylineCycleCoroutine;
  [SerializeField] private float paylineHoldDuration = 1.5f;
  [SerializeField] private float paylineAllTogetherDuration = 1.5f;

  [SerializeField]
  private List<ImageAnimation> TempList;  //stores the sprites whose animation is running at present 

  [SerializeField]
  private SocketIOManager SocketManager;

  private Coroutine AutoSpinRoutine = null;
  private Coroutine FreeSpinRoutine = null;
  private Coroutine tweenroutine;
  private Tween BalanceTween;
  private Sprite _spinPressedSprite;
  internal bool IsAutoSpin = false;
  internal bool IsFreeSpin = false;
  private bool IsSpinning = false;
  internal bool CheckPopups = false;
  internal int BetCounter = 0;
  private double currentBalance = 0;
  private double currentTotalBet = 0;
  protected int Lines = 5;
  private int numberOfSlots = 3;          //number of columns
  private int numberOfRows = 5;           //number of rows per column (3 real + 2 decorative edge rows)
  private bool StopSpinToggle;
  private float SpinDelay = 0.2f;
  [SerializeField] private float autoSpinExtraDelay = 0.0f;
  private float minSpinDuration = 1.5f;
  internal bool WasAutoSpinOn;
  private bool _isFirstFreeSpin;
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

  // Cached per-landed-spin 5-row display grid: server sends only the 3 real rows
  // (payload.reels), rows 0 and numberOfRows-1 are synthesized client-side (see
  // BuildDisplayMatrix). Built once when a spin lands; every other reader of "the matrix"
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

  #region FreeSpin
  internal void FreeSpin(int spins)
  {
    if (!IsFreeSpin)
    {
      uiManager.UpdateFreeSpinsRemaining(spins);
      IsFreeSpin = true;
      if (FreeSpinSlotMachine)
      {
        CanvasGroup freeSpinSlotMachineCanvasGroup = FreeSpinSlotMachine.GetComponent<CanvasGroup>();
        if (freeSpinSlotMachineCanvasGroup)
        {
          freeSpinSlotMachineCanvasGroup.DOKill();
          freeSpinSlotMachineCanvasGroup.alpha = 1f;
        }
        FreeSpinSlotMachine.SetActive(true);
        ImageAnimation freeSpinReelAnim = FreeSpinSlotMachine.GetComponent<ImageAnimation>();
        if (freeSpinReelAnim) freeSpinReelAnim.StartAnimation();
      }
      ToggleButtonGrp(false);
      if (Spin_Button)
      {
        Spin_Button.interactable = false;
        Spin_Button.GetComponent<Image>().sprite = SpinDeactivatedSprite;
      }

      if (FreeSpinRoutine != null)
      {
        StopCoroutine(FreeSpinRoutine);
        FreeSpinRoutine = null;
      }
      FreeSpinRoutine = StartCoroutine(FreeSpinCoroutine(spins));
    }
  }

  private IEnumerator FreeSpinCoroutine(int spinchances)
  {
    yield return new WaitForSecondsRealtime(1.5f);
    uiManager.UpdateFreeSpinsRemaining(spinchances);
    bool isFreeSpinActive;
    bool isFirstFreeSpin = true;
    do
    {
      _isFirstFreeSpin = isFirstFreeSpin;
      StartSlots();
      yield return tweenroutine;
      if (SocketManager.ResultData.payload.winAmount > 0)
        yield return WaitForFreeSpinWinDisplay();
      else
        yield return new WaitForSeconds(SpinDelay);
      isFreeSpinActive = SocketManager.ResultData.payload.isFreeSpinActive;
      uiManager.UpdateFreeSpinsRemaining(SocketManager.ResultData.payload.freeSpinsRemaining);
      isFirstFreeSpin = false;
    } while (isFreeSpinActive);

    double totalFreeSpinWin = SocketManager.ResultData.payload.totalFreeSpinWin;
    uiManager.PlayBonusWinSequence(totalFreeSpinWin, currentTotalBet);
    uiManager.PlaySpinWin(totalFreeSpinWin);

    yield return new WaitForSeconds(specialReelSwapDelay);
    if (SpecialReelObject) SpecialReelObject.SetActive(false);
    if (MiddleReelObject) MiddleReelObject.SetActive(true);
    if (MiddleReelGlow) MiddleReelGlow.SetActive(false);
    if (FreeSpinSlotMachine) FreeSpinSlotMachine.SetActive(false);
    uiManager.EndFreeSpinTriggerSequence();

    if (totalFreeSpinWin > 0)
      yield return WaitForFreeSpinWinDisplay();

    IsFreeSpin = false;
    if (WasAutoSpinOn)
    {
      WasAutoSpinOn = false;
      AutoSpin();
    }
    else
    {
      ToggleButtonGrp(true);
    }
  }

  // Waits for the win popup and, if there were winning paylines, for each to get its full display time.
  private IEnumerator WaitForFreeSpinWinDisplay()
  {
    float paylineCycleDuration = lastWinLineCount * paylineHoldDuration;
    float waitStart = Time.time;
    yield return new WaitUntil(() => !uiManager.IsWinSequenceActive && Time.time - waitStart >= paylineCycleDuration);
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
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * Lines).ToString();
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * Lines;
    uiManager.SetBet(currentTotalBet);
  }

  private void ChangeBet(bool IncDec)
  {
    if (audioController) audioController.PlayButton();
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
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * Lines).ToString();
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * Lines;
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


  // Synthesizes the decorative top/bottom rows (0 and numberOfRows-1) from the server's
  // 3 real rows (payload.reels). By design a real reel column can only ever land with
  // exactly 1 symbol (centered) or exactly 2 symbols (top+bottom, gapped) - see
  // pdg_backend_clarifications. Any other pattern is a backend bug; log and fall back to
  // blank rather than throw.
  private int[,] BuildDisplayMatrix(List<List<string>> reels)
  {
    int[,] display = new int[numberOfRows, numberOfSlots];
    List<Symbol> nonBlankSymbols = SocketManager.UIData.paylines.symbols.FindAll(s => s.id != 0);

    for (int col = 0; col < numberOfSlots; col++)
    {
      int top = int.Parse(reels[0][col]);
      int mid = int.Parse(reels[1][col]);
      int bottom = int.Parse(reels[2][col]);

      display[1, col] = top;
      display[2, col] = mid;
      display[3, col] = bottom;

      bool oneCentered = top == 0 && mid != 0 && bottom == 0;
      bool twoGapped = top != 0 && mid == 0 && bottom != 0;

      if (!oneCentered && !twoGapped)
      {
        Debug.LogWarning($"[DecorativeRow] Unexpected column pattern at col {col}: ({top},{mid},{bottom}) — expected exactly 1 centered or 2 top/bottom non-blank symbols. Defaulting decorative rows to blank.");
      }

      if (oneCentered && nonBlankSymbols.Count > 0)
      {
        display[0, col] = nonBlankSymbols[UnityEngine.Random.Range(0, nonBlankSymbols.Count)].id;
        display[numberOfRows - 1, col] = nonBlankSymbols[UnityEngine.Random.Range(0, nonBlankSymbols.Count)].id;
      }
      else
      {
        display[0, col] = 0;
        display[numberOfRows - 1, col] = 0;
      }
    }

    return display;
  }

  internal void InitializeMatrix()
  {
    for (int row = 0; row < initialMatrix.GetLength(0); row++)
    {
      for (int col = 0; col < initialMatrix.GetLength(1); col++)
      {
        int val = initialMatrix[row, col];

        TempImages[col].slotImages[row].sprite = myImages[val];

        ImageAnimation animScript = TempImages[col].slotImages[row].GetComponent<ImageAnimation>();
        if (animScript != null)
        {
          PopulateAnimationSprites(animScript, val);

          if (val != 10)
          {
            animScript.StartAnimation();
            TempList.Add(animScript);
          }
        }
      }
    }
  }


  internal void SetInitialUI()
  {
    socketConnected = true;
    BetCounter = 0;
    Lines = SocketManager.InitialData.totalLines;
    if (LineBet_text) LineBet_text.text = SocketManager.InitialData.bets[BetCounter].ToString();
    if (TotalBet_text) TotalBet_text.text = (SocketManager.InitialData.bets[BetCounter] * Lines).ToString();
    if (TotalWin_text) TotalWin_text.text = "0.000";
    if (Balance_text) Balance_text.text = SocketManager.PlayerData.balance.ToString("F3");
    currentBalance = SocketManager.PlayerData.balance;
    currentTotalBet = SocketManager.InitialData.bets[BetCounter] * Lines;
    CompareBalance();
    uiManager.InitialiseUI(SocketManager.InitialData.bets, SocketManager.UIData.paylines.symbols);
    uiManager.SetBet(currentTotalBet);
  }
  #endregion

  private void OnApplicationFocus(bool focus)
  {
  }

  //function to populate animation sprites accordingly
  private void PopulateAnimationSprites(ImageAnimation animScript, int val)
  {
    animScript.textureArray.Clear();
    animScript.textureArray.TrimExcess();
    switch (val)
    {
      case 0:
        for (int i = 0; i < Blank_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Blank_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 1:
        for (int i = 0; i < SingleBar_Sprite.Length; i++)
        {
          animScript.textureArray.Add(SingleBar_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 2:
        for (int i = 0; i < DoubleBar_Sprite.Length; i++)
        {
          animScript.textureArray.Add(DoubleBar_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 3:
        for (int i = 0; i < TripleBar_Sprite.Length; i++)
        {
          animScript.textureArray.Add(TripleBar_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 4:
        for (int i = 0; i < Bell_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Bell_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 5:
        for (int i = 0; i < Red7_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Red7_Sprite[i]);
        }
        animScript.AnimationSpeed = 12f;
        break;
      case 6:
        for (int i = 0; i < Wild2x_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Wild2x_Sprite[i]);
        }
        animScript.AnimationSpeed = 30f;
        break;
      case 7:
        for (int i = 0; i < Wild3x_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Wild3x_Sprite[i]);
        }
        animScript.AnimationSpeed = 30f;
        break;
      case 8:
        for (int i = 0; i < Wild5x_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Wild5x_Sprite[i]);
        }
        animScript.AnimationSpeed = 30f;
        break;
      case 9:
        for (int i = 0; i < Wild10x_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Wild10x_Sprite[i]);
        }
        animScript.AnimationSpeed = 30f;
        break;
      case 10:
        for (int i = 0; i < Scatter_Sprite.Length; i++)
        {
          animScript.textureArray.Add(Scatter_Sprite[i]);
        }
        animScript.AnimationSpeed = 30f;
        break;
    }
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
    uiManager.SkipWinSequences();

    if (TotalWin_text && !IsFreeSpin) TotalWin_text.text = "0.000";

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
    tweenroutine = StartCoroutine(TweenRoutine());
  }

  //manage the Routine for spinning of the slots
  private IEnumerator TweenRoutine()
  {
    if (currentBalance < currentTotalBet && !IsFreeSpin)
    {
      CompareBalance();
      StopAutoSpin();
      yield return new WaitForSeconds(1);
      ToggleButtonGrp(true);
      yield break;
    }
    if (audioController) audioController.PlaySpinLoop();

    IsSpinning = true;

    if (IsFreeSpin && _isFirstFreeSpin)
    {
      InitializeReelSpin(SpecialReelTransform, SpecialReelIndex, SpecialReelTopY, SpecialReelBottomY, specialReelSpeed);
      yield return new WaitForSeconds(2f);
      ActivateStopButton();
      if (audioController) audioController.PlaySpecialReelSpin();
      for (int i = 0; i < numberOfSlots; i++)
        InitializeReelSpin(Slot_Transform[i], i, ReelTopY, ReelBottomY, reelSpeed);
    }
    else if (IsFreeSpin)
    {
      ActivateStopButton();
      InitializeReelSpin(SpecialReelTransform, SpecialReelIndex, SpecialReelTopY, SpecialReelBottomY, specialReelSpeed);
      if (audioController) audioController.PlaySpecialReelSpin();
      for (int i = 0; i < numberOfSlots; i++)
        InitializeReelSpin(Slot_Transform[i], i, ReelTopY, ReelBottomY, reelSpeed);
    }
    else
    {
      ActivateStopButton();
      for (int i = 0; i < numberOfSlots; i++)
      {
        InitializeReelSpin(Slot_Transform[i], i, ReelTopY, ReelBottomY, reelSpeed);
        yield return new WaitForSeconds(0.1f);
      }
    }
    float spinStartTime = Time.time;

    if (!IsFreeSpin)
    {
      BalanceDeduction();
    }

    SocketManager.AccumulateResult(BetCounter);
    yield return new WaitUntil(() => SocketManager.isResultdone);

    _displayMatrix = BuildDisplayMatrix(SocketManager.ResultData.payload.reels);

    for (int i = 0; i < numberOfRows; i++)
    {
      for (int j = 0; j < numberOfSlots; j++)
      {
        // Column 1 is hidden behind the special wilds reel during free spins — leave it showing
        // whatever it last had before free spins started rather than a stale free-spin wild.
        if (IsFreeSpin && j == 1) continue;

        int resultNum = _displayMatrix[i, j];
        // print("resultNum: " + resultNum);
        // print("image loc: " + j + " " + i);
        ImageAnimation animScript = TempImages[j].slotImages[i].GetComponent<ImageAnimation>();
        if (animScript != null)
        {
          PopulateAnimationSprites(animScript, resultNum);
        }
        TempImages[j].slotImages[i].sprite = myImages[resultNum];
        TempImages[j].slotImages[i].rectTransform.sizeDelta = ScatterSymbolBaseSize;
      }
    }

    if (IsFreeSpin && SpecialReelSlotImages != null)
    {
      for (int row = 0; row < numberOfRows && row < SpecialReelSlotImages.slotImages.Count; row++)
      {
        int resultNum = _displayMatrix[row, 1];
        ImageAnimation specialAnimScript = SpecialReelSlotImages.slotImages[row].GetComponent<ImageAnimation>();
        if (specialAnimScript != null)
        {
          PopulateAnimationSprites(specialAnimScript, resultNum);
        }
        SpecialReelSlotImages.slotImages[row].sprite = myImages[resultNum];
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

    bool willTriggerFreeSpin = SocketManager.ResultData.features.freeSpin.isFreeSpin && !IsFreeSpin;

    if (willTriggerFreeSpin && IsAutoSpin && AutoSpinRoutine != null)
    {
      StopCoroutine(AutoSpinRoutine);
      AutoSpinRoutine = null;
    }

    // Precompute which reel (if any) contains the scatter that completes the count to 3,
    // counting left-to-right across non-decorative rows only.
    int specialScatterReelIndex = -1;
    if (!IsFreeSpin)
    {
      int scatterTally = 0;
      for (int col = 0; col < numberOfSlots; col++)
      {
        for (int row = 1; row < numberOfRows - 1; row++)
        {
          if (_displayMatrix[row, col] == 10)
            scatterTally++;
        }
        if (specialScatterReelIndex == -1 && scatterTally >= 3)
          specialScatterReelIndex = col;
      }
    }

    bool lastReelPunched = false;
    for (int i = 0; i < numberOfSlots; i++)
    {
      if (IsFreeSpin && i == 1)
      {
        if (audioController) audioController.StopSpecialReelSpin();
        StopReelSpin(SpecialReelTransform, SpecialReelIndex, SpecialReelTopY, SpecialReelRestY, specialReelSpeed);
      }

      if (i == specialScatterReelIndex)
      {
        GameObject anticipationGlow = i == 1 ? MiddleReelGlow : (i == numberOfSlots - 1 ? LastReelGlow : null);
        Debug.Log("[ScatterAnticipation DEBUG] Entering special reel " + i + ", setting glow ON");
        if (anticipationGlow) anticipationGlow.SetActive(true);
        uiManager.StartAnticipationZoom(anticipationExtraSpinDuration);
        yield return new WaitForSeconds(anticipationExtraSpinDuration);
        Debug.Log("[ScatterAnticipation DEBUG] Anticipation wait complete, stopping reel " + i);
        yield return StopBaseReel(i);
        Debug.Log("[ScatterAnticipation DEBUG] Reel stop complete, setting glow OFF");
        if (anticipationGlow) anticipationGlow.SetActive(false);
        Debug.Log("[ScatterAnticipation DEBUG] Glow set OFF successfully");
      }
      else
      {
        yield return StopBaseReel(i);
        if (!IsFreeSpin && (specialScatterReelIndex == -1 || i < specialScatterReelIndex))
        {
          int reelScatterCount = 0;
          for (int row = 1; row < numberOfRows - 1; row++)
          {
            if (_displayMatrix[row, i] == 10)
              reelScatterCount++;
          }
          for (int p = 0; p < reelScatterCount; p++)
            uiManager.ScatterAnticipationPunch();
          if (reelScatterCount > 0 && i == numberOfSlots - 1)
            lastReelPunched = true;
        }
      }
    }

    if (!willTriggerFreeSpin)
    {
      if (lastReelPunched)
        yield return new WaitForSeconds(0.2f);
      uiManager.ResetAnticipationZoom();
    }
    StopSpinToggle = false;
    // Base columns share a speed and start landing in order, so the last one finishes last.
    yield return reelTweens[numberOfSlots - 1].WaitForCompletion();
    // The special column runs at its own slower speed, so it is still in flight here. It has to
    // finish before KillAllTweens, or it freezes short of its rest position.
    if (IsFreeSpin && reelTweens[SpecialReelIndex] != null && reelTweens[SpecialReelIndex].IsActive())
      yield return reelTweens[SpecialReelIndex].WaitForCompletion();

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

    bool anyScatter = false;
    for (int row = 0; row < numberOfRows; row++)
    {
      for (int col = 0; col < numberOfSlots; col++)
      {
        if (_displayMatrix[row, col] == 10)
        {
          bool isDecorativeRow = row == 0 || row == numberOfRows - 1;
          if (!isDecorativeRow)
          {
            if (!_animateAllSymbols)
              StartGameAnimation(TempImages[col].slotImages[row].gameObject);
            anyScatter = true;
          }
        }
      }
    }
    if (anyScatter && audioController) audioController.PlayAllScatter();

    if (!IsFreeSpin && SocketManager.ResultData.payload.totalWin > 0)
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
      CheckPayoutLineBackend(winLine, SocketManager.ResultData.features.jackpot.amount);
    }
    else
    {
      lastWinLineCount = 0;
    }

    CheckPopups = true;

    if (TotalWin_text)
    {
      double displayWin = IsFreeSpin ? SocketManager.ResultData.payload.totalFreeSpinWin : SocketManager.ResultData.payload.totalWin;
      TotalWin_text.text = displayWin.ToString("F3");
    }
    BalanceTween?.Kill();
    if (Balance_text) Balance_text.text = SocketManager.ResultData.player.balance.ToString("F3");

    currentBalance = SocketManager.PlayerData.balance;

    if (IsFreeSpin)
    {
      uiManager.PlaySpinWin(SocketManager.ResultData.payload.winAmount);
      yield return new WaitUntil(() => !uiManager.IsWinSequenceActive);
    }
    else
    {
      if (CheckAnyPureWildLine())
        uiManager.PlayBigWinSequence(SocketManager.ResultData.payload.totalWin);
      else
        uiManager.PlaySpinWin(SocketManager.ResultData.payload.totalWin);
    }

    if (SocketManager.ResultData.features.jackpot.isTriggered)
    {
      CheckPopups = false;
      yield return new WaitUntil(() => !CheckPopups);
      CheckPopups = true;
    }

    CheckWinPopups();

    yield return new WaitUntil(() => !CheckPopups);
    if (!IsAutoSpin && !IsFreeSpin && !willTriggerFreeSpin)
    {
      ToggleButtonGrp(true);
      IsSpinning = false;
    }
    else
    {
      // yield return new WaitForSeconds(2f);
      IsSpinning = false;
    }
    if (willTriggerFreeSpin)
    {
      if (audioController) audioController.PlayScatterFreeSpin();
      if (ScatterTrigger_Sprite != null && ScatterTrigger_Sprite.Length > 0)
      {
        for (int row = 0; row < numberOfRows; row++)
        {
          for (int col = 0; col < numberOfSlots; col++)
          {
            if (_displayMatrix[row, col] == 10 && row != 0 && row != numberOfRows - 1)
            {
              ImageAnimation anim = TempImages[col].slotImages[row].GetComponent<ImageAnimation>();
              if (anim != null)
              {
                anim.textureArray.Clear();
                foreach (Sprite s in ScatterTrigger_Sprite)
                  anim.textureArray.Add(s);
                anim.doLoopAnimation = true;
                anim.StartAnimation();
              }
              TempImages[col].slotImages[row].rectTransform.sizeDelta = ScatterSymbolBaseSize * 1.5f;
            }
          }
        }
      }

      yield return StartCoroutine(uiManager.PlayFreeSpinTriggerSequence(SocketManager.ResultData.features.freeSpin.count));
      if (MiddleReelGlow) MiddleReelGlow.SetActive(true);
      yield return StartCoroutine(PlaySpecialWildReel());
      FreeSpin(SocketManager.ResultData.features.freeSpin.count);
      if (IsAutoSpin)
      {
        WasAutoSpinOn = true;
        StopAutoSpin();
        yield return new WaitForSeconds(0.1f);
      }
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
      balance = double.Parse(Balance_text.text);
    }
    catch (Exception e)
    {
      Debug.Log("Error while conversion " + e.Message);
    }
    double initAmount = balance;

    balance = balance - bet;

    BalanceTween = DOTween.To(() => initAmount, (val) => initAmount = val, balance, 0.8f).OnUpdate(() =>
    {
      if (Balance_text) Balance_text.text = initAmount.ToString("F3");
    });
  }

  private bool CheckAnyPureWildLine()
  {
    if (SocketManager.ResultData.payload.wins == null) return false;
    var lines = SocketManager.InitialData.lines;
    foreach (var win in SocketManager.ResultData.payload.wins)
    {
      int lineIndex = win.line;
      if (lineIndex < 0 || lineIndex >= lines.Count) continue;
      var rows = lines[lineIndex];
      bool pureWild = true;
      for (int col = 0; col < rows.Count; col++)
      {
        int symbolId = _displayMatrix[rows[col], col];
        if (symbolId < 6 || symbolId > 9) { pureWild = false; break; }
      }
      if (pureWild) return true;
    }
    return false;
  }

  internal void CheckWinPopups()
  {
    CheckPopups = false;
  }

  // Column 1 is visually replaced by the special wilds reel during free spins, so its symbol animations
  // must target the special reel's own images rather than the hidden middle reel's.
  private GameObject GetSlotImageGameObject(int row, int column)
  {
    if (IsFreeSpin && column == 1 && SpecialReelSlotImages != null && row < SpecialReelSlotImages.slotImages.Count)
      return SpecialReelSlotImages.slotImages[row].gameObject;
    return TempImages[column].slotImages[row].gameObject;
  }

  private IEnumerator CyclePaylines(List<int> lineIds, Dictionary<int, List<KeyValuePair<int, int>>> lineCoords)
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

    // Phase 2: one winning line at a time, looping — only that line's symbols animate.
    while (true)
    {
      foreach (int id in lineIds)
      {
        List<ImageAnimation> lineAnims = new();
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
          if (anim != null) lineAnims.Add(anim);
        }
        yield return new WaitForSeconds(paylineHoldDuration);
        if (PaylineGraphics.Count > id) PaylineGraphics[id].SetActive(false);
        foreach (var anim in lineAnims) anim.StopAnimation();
      }
    }
  }

  //generate the payout lines generated
  private void CheckPayoutLineBackend(List<int> LineId, double jackpot = 0)
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
            int rowIndex = SocketManager.InitialData.lines[LineId[j]][k];
            int columnIndex = k;
            coords.Add(new KeyValuePair<int, int>(rowIndex, columnIndex));
          }
          lineCoords[LineId[j]] = coords;
        }
        _paylineCycleCoroutine = StartCoroutine(CyclePaylines(sortedIds, lineCoords));
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
    if (AutoSpin_Button) AutoSpin_Button.interactable = !IsFreeSpin && (toggle || IsAutoSpin);
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
    if (StopSpinToggle)
      yield return null;
    else
      yield return new WaitForSeconds(0.2f);
  }

  private IEnumerator PlaySpecialWildReel()
  {
    if (!SpecialReelObject || !SpecialReelTransform) yield break;
    SpecialReelObject.SetActive(true);
    if (MiddleReelObject) MiddleReelObject.SetActive(false);
    SpecialReelTransform.localPosition = new Vector2(SpecialReelTransform.localPosition.x, SpecialReelRestY);
    yield return null;
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