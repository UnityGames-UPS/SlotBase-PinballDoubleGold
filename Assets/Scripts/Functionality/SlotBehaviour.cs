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
  [SerializeField] private float specialReelSpeedMultiplier = 0.6f;
  [SerializeField] private float specialReelDuration = 2f;
  [SerializeField] private GameObject MiddleReelGlow;
  [SerializeField] private GameObject LastReelGlow;
  [SerializeField] internal GameObject FreeSpinSlotMachine;

  int tweenHeight = 0;
  private float topSlotImageY = 3393.2f;


  private List<Tweener> alltweens = new List<Tweener>();
  private Image[][] reelImages = new Image[3][];
  private float[][] reelImageInitialLocalY = new float[3][];
  private Coroutine[] recycleCoroutines = new Coroutine[3];
  private Coroutine _paylineCycleCoroutine;
  private Tweener _specialReelSpinTween;
  private Coroutine _specialReelRecycleCoroutine;
  private Image[] _specialReelImages;
  private float[] _specialReelImageInitialLocalY;
  [SerializeField] private float paylineHoldDuration = 1.5f;

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
  [SerializeField]
  private int IconSizeFactor = 100;       //set this parameter according to the size of the icon and spacing
  private int numberOfSlots = 3;          //number of columns
  private int numberOfRows = 5;           //number of rows per column (3 real + 2 decorative edge rows)
  private bool StopSpinToggle;
  private float SpinDelay = 0.2f;
  private float minSpinDuration = 1.5f;
  internal bool WasAutoSpinOn;
  private bool _isFirstFreeSpin;
  internal bool socketConnected = false;
  private int[,] initialMatrix = new int[,]
  {
    { 4, 4, 5 },
    { 0, 0, 0 },
    { 10, 10, 10 },
    { 0, 0, 0 },
    { 3, 3, 3 }
  };

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

    tweenHeight = (15 * IconSizeFactor) - 280;
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
      yield return new WaitForSeconds(SpinDelay);
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
      }
      ToggleButtonGrp(false);

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
      yield return new WaitForSeconds(SpinDelay);
      isFreeSpinActive = SocketManager.ResultData.payload.isFreeSpinActive;
      uiManager.UpdateFreeSpinsRemaining(SocketManager.ResultData.payload.freeSpinsRemaining);
      isFirstFreeSpin = false;
    } while (isFreeSpinActive);
    if (SpecialReelObject) SpecialReelObject.SetActive(false);
    if (MiddleReelObject) MiddleReelObject.SetActive(true);
    if (MiddleReelGlow) MiddleReelGlow.SetActive(false);
    if (FreeSpinSlotMachine) FreeSpinSlotMachine.SetActive(false);
    uiManager.EndFreeSpinTriggerSequence();

    double totalFreeSpinWin = SocketManager.ResultData.payload.totalFreeSpinWin;
    StartCoroutine(uiManager.ShowSpinWin(totalFreeSpinWin));
    StartCoroutine(uiManager.ShowBonusWinSequence(totalFreeSpinWin, currentTotalBet));

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
  }

  //starts the spin process
  private void StartSlots(bool autoSpin = false)
  {

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

    ToggleButtonGrp(false);
    if (Spin_Button)
    {
      Spin_Button.GetComponent<Image>().sprite = StopSprite;
      var ss = Spin_Button.spriteState;
      ss.pressedSprite = StopPressedSprite;
      Spin_Button.spriteState = ss;
    }
    if (IsFreeSpin && _isFirstFreeSpin)
    {
      InitializeSpecialReelTweening();
      yield return new WaitForSeconds(2f);
      if (audioController) audioController.PlaySpecialReelSpin();
      for (int i = 0; i < numberOfSlots; i++)
        InitializeTweening(Slot_Transform[i], i);
    }
    else if (IsFreeSpin)
    {
      InitializeSpecialReelTweening();
      if (audioController) audioController.PlaySpecialReelSpin();
      for (int i = 0; i < numberOfSlots; i++)
        InitializeTweening(Slot_Transform[i], i);
    }
    else
    {
      for (int i = 0; i < numberOfSlots; i++)
      {
        InitializeTweening(Slot_Transform[i], i);
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

    for (int i = 0; i < numberOfRows; i++)
    {
      for (int j = 0; j < numberOfSlots; j++)
      {
        int resultNum = int.Parse(SocketManager.ResultData.matrix[i][j]);
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

    for (int j = 0; j < numberOfSlots; j++)
    {
      bool isCaseA = int.Parse(SocketManager.ResultData.matrix[0][j]) != 0;
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
          if (int.Parse(SocketManager.ResultData.matrix[row][col]) == 10)
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
        StopSpecialReelTweening();

      if (i == specialScatterReelIndex)
      {
        GameObject anticipationGlow = i == 1 ? MiddleReelGlow : (i == numberOfSlots - 1 ? LastReelGlow : null);
        Debug.Log("[ScatterAnticipation DEBUG] Entering special reel " + i + ", setting glow ON");
        if (anticipationGlow) anticipationGlow.SetActive(true);
        uiManager.StartAnticipationZoom(anticipationExtraSpinDuration);
        yield return new WaitForSeconds(anticipationExtraSpinDuration);
        Debug.Log("[ScatterAnticipation DEBUG] Anticipation wait complete, stopping reel " + i);
        yield return StopTweening(5, Slot_Transform[i], i, StopSpinToggle);
        Debug.Log("[ScatterAnticipation DEBUG] StopTweening complete, setting glow OFF");
        if (anticipationGlow) anticipationGlow.SetActive(false);
        Debug.Log("[ScatterAnticipation DEBUG] Glow set OFF successfully");
      }
      else
      {
        yield return StopTweening(5, Slot_Transform[i], i, StopSpinToggle);
        if (!IsFreeSpin && (specialScatterReelIndex == -1 || i < specialScatterReelIndex))
        {
          int reelScatterCount = 0;
          for (int row = 1; row < numberOfRows - 1; row++)
          {
            if (int.Parse(SocketManager.ResultData.matrix[row][i]) == 10)
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
    yield return alltweens[^1].WaitForCompletion();

    if (Spin_Button)
    {
      Spin_Button.GetComponent<Image>().sprite = SpinSprite;
      var ss = Spin_Button.spriteState;
      ss.pressedSprite = _spinPressedSprite;
      Spin_Button.spriteState = ss;
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
        if (int.Parse(SocketManager.ResultData.matrix[row][col]) == 10)
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

    if (!IsFreeSpin && SocketManager.ResultData.payload.winAmount > 0)
    {
      SpinDelay = 1.2f;
    }
    else
    {
      SpinDelay = 0.2f;
    }

    if (SocketManager.ResultData.payload.winAmount > 0)
    {
      List<int> winLine = new();
      foreach (var item in SocketManager.ResultData.payload.wins)
      {
        winLine.Add(item.line);
      }
      CheckPayoutLineBackend(winLine, SocketManager.ResultData.features.jackpot.amount);
    }

    CheckPopups = true;

    if (TotalWin_text)
    {
      double displayWin = IsFreeSpin ? SocketManager.ResultData.payload.totalFreeSpinWin : SocketManager.ResultData.payload.winAmount;
      TotalWin_text.text = displayWin.ToString("F3");
    }
    BalanceTween?.Kill();
    if (Balance_text) Balance_text.text = SocketManager.ResultData.player.balance.ToString("F3");

    currentBalance = SocketManager.PlayerData.balance;

    if (IsFreeSpin)
    {
      yield return StartCoroutine(uiManager.ShowSpinWin(SocketManager.ResultData.payload.winAmount));
    }
    else
    {
      if (CheckAnyPureWildLine())
        yield return StartCoroutine(uiManager.ShowBigWinSequence(SocketManager.ResultData.payload.winAmount));
      else
        StartCoroutine(uiManager.ShowSpinWin(SocketManager.ResultData.payload.winAmount));
    }

    if (SocketManager.ResultData.features.jackpot.isTriggered)
    {
      CheckPopups = false;
      yield return new WaitUntil(() => !CheckPopups);
      CheckPopups = true;
    }

    CheckWinPopups();

    yield return new WaitUntil(() => !CheckPopups);
    if (!IsAutoSpin && !IsFreeSpin)
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
            if (int.Parse(SocketManager.ResultData.matrix[row][col]) == 10 && row != 0 && row != numberOfRows - 1)
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
        int symbolId = int.Parse(SocketManager.ResultData.matrix[rows[col]][col]);
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

  private IEnumerator CyclePaylines(List<int> lineIds)
  {
    while (true)
    {
      for (int i = 0; i < lineIds.Count; i++)
      {
        int id = lineIds[i];
        if (PaylineGraphics.Count > id)
        {
          PaylineGraphics[id].SetActive(true);
          StartGameAnimation(PaylineGraphics[id]);
        }
        yield return new WaitForSeconds(paylineHoldDuration);
        if (PaylineGraphics.Count > id)
          PaylineGraphics[id].SetActive(false);
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
      _paylineCycleCoroutine = StartCoroutine(CyclePaylines(sortedIds));

      if (jackpot > 0)
      {
        for (int i = 0; i < TempImages.Count; i++)
        {
          for (int k = 0; k < TempImages[i].slotImages.Count; k++)
          {
            StartGameAnimation(TempImages[i].slotImages[k].gameObject);
          }
        }
      }
      else
      {
        List<KeyValuePair<int, int>> coords = new();
        for (int j = 0; j < LineId.Count; j++)
        {
          for (int k = 0; k < SocketManager.ResultData.payload.wins[j].positions.Count; k++)
          {
            int rowIndex = SocketManager.InitialData.lines[LineId[j]][k];
            int columnIndex = k;
            coords.Add(new KeyValuePair<int, int>(rowIndex, columnIndex));
          }
        }

        foreach (var coord in coords)
        {
          int rowIndex = coord.Key;
          int columnIndex = coord.Value;
          StartGameAnimation(TempImages[columnIndex].slotImages[rowIndex].gameObject);
        }
      }
    }
  }

  #endregion

  internal void CallCloseSocket()
  {
    StartCoroutine(SocketManager.CloseSocket());
  }


  void ToggleButtonGrp(bool toggle)
  {
    bool active = toggle && !IsAutoSpin;
    if (Spin_Button) Spin_Button.interactable = toggle ? active : true;
    if (AutoSpin_Button) AutoSpin_Button.interactable = !IsFreeSpin && (toggle || IsAutoSpin);
    if (MaxBet_Button) MaxBet_Button.interactable = active;
    if (TBetMinus_Button) TBetMinus_Button.interactable = active;
    if (TBetPlus_Button) TBetPlus_Button.interactable = active;
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
  private void InitializeTweening(Transform slotTransform, int colIndex)
  {
    // Exclude any Image on slotTransform itself (e.g. Mask component image)
    List<Image> imageList = new List<Image>();
    foreach (Image img in slotTransform.GetComponentsInChildren<Image>())
      if (img.transform != slotTransform) imageList.Add(img);
    Image[] images = imageList.ToArray();
    System.Array.Sort(images, (a, b) => b.transform.localPosition.y.CompareTo(a.transform.localPosition.y));

    reelImages[colIndex] = images;
    reelImageInitialLocalY[colIndex] = new float[images.Length];
    for (int i = 0; i < images.Length; i++)
      reelImageInitialLocalY[colIndex][i] = images[i].transform.localPosition.y;

    float imageSpacing = images.Length > 1
      ? Mathf.Abs(images[0].transform.localPosition.y - images[1].transform.localPosition.y)
      : (float)IconSizeFactor;

    // Threshold entirely in local-space units: slotTransform.localY + image.localY < this value
    // means the image has scrolled below the visible window bottom
    float visibleBottomLocalY = float.MaxValue;
    for (int r = 0; r < numberOfRows && r < TempImages[colIndex].slotImages.Count; r++)
      visibleBottomLocalY = Mathf.Min(visibleBottomLocalY, TempImages[colIndex].slotImages[r].transform.localPosition.y);
    float visibleBottomCanvasY = topSlotImageY + visibleBottomLocalY;

    float recycleThreshold = visibleBottomCanvasY - imageSpacing;

    if (recycleCoroutines[colIndex] != null) StopCoroutine(recycleCoroutines[colIndex]);
    recycleCoroutines[colIndex] = StartCoroutine(RecycleReel(slotTransform, images, imageSpacing, recycleThreshold));

    Tweener tweener = slotTransform.DOLocalMoveY(slotTransform.localPosition.y - tweenHeight, 0.2f)
      .SetLoops(-1, LoopType.Incremental)
      .SetEase(Ease.Linear)
      .SetDelay(0);
    tweener.Play();
    while (alltweens.Count <= colIndex) alltweens.Add(null);
    alltweens[colIndex] = tweener;
  }

  private IEnumerator RecycleReel(Transform slotTransform, Image[] images, float imageSpacing, float recycleThreshold)
  {
    while (true)
    {
      float parentLocalY = slotTransform.localPosition.y;
      float maxLocalY = float.MinValue;
      for (int i = 0; i < images.Length; i++)
        maxLocalY = Mathf.Max(maxLocalY, images[i].transform.localPosition.y);

      for (int i = 0; i < images.Length; i++)
      {
        if (parentLocalY + images[i].transform.localPosition.y < recycleThreshold)
        {
          Vector3 pos = images[i].transform.localPosition;
          pos.y = maxLocalY + imageSpacing;
          maxLocalY = pos.y;
          images[i].transform.localPosition = pos;
        }
      }
      yield return null;
    }
  }

  private IEnumerator PlaySpecialWildReel()
  {
    if (!SpecialReelObject || !SpecialReelTransform) yield break;
    SpecialReelObject.SetActive(true);
    if (MiddleReelObject) MiddleReelObject.SetActive(false);
    SpecialReelTransform.localPosition = new Vector2(SpecialReelTransform.localPosition.x, topSlotImageY);
    yield return null;
  }



  private void InitializeSpecialReelTweening()
  {
    if (!SpecialReelTransform) return;
    SpecialReelTransform.DOKill();

    List<Image> imageList = new List<Image>();
    foreach (Image img in SpecialReelTransform.GetComponentsInChildren<Image>())
      if (img.transform != SpecialReelTransform) imageList.Add(img);
    _specialReelImages = imageList.ToArray();
    System.Array.Sort(_specialReelImages, (a, b) => b.transform.localPosition.y.CompareTo(a.transform.localPosition.y));

    _specialReelImageInitialLocalY = new float[_specialReelImages.Length];
    for (int i = 0; i < _specialReelImages.Length; i++)
      _specialReelImageInitialLocalY[i] = _specialReelImages[i].transform.localPosition.y;

    float imageSpacing = _specialReelImages.Length > 1
      ? Mathf.Abs(_specialReelImages[0].transform.localPosition.y - _specialReelImages[1].transform.localPosition.y)
      : 150f;

    float visibleBottomLocalY = float.MaxValue;
    for (int r = 0; r < numberOfRows && r < TempImages[1].slotImages.Count; r++)
      visibleBottomLocalY = Mathf.Min(visibleBottomLocalY, TempImages[1].slotImages[r].transform.localPosition.y);
    float recycleThreshold = topSlotImageY + visibleBottomLocalY - imageSpacing;

    if (_specialReelRecycleCoroutine != null) StopCoroutine(_specialReelRecycleCoroutine);
    _specialReelRecycleCoroutine = StartCoroutine(RecycleReel(SpecialReelTransform, _specialReelImages, imageSpacing, recycleThreshold));

    _specialReelSpinTween = SpecialReelTransform.DOLocalMoveY(SpecialReelTransform.localPosition.y - tweenHeight, 0.2f / specialReelSpeedMultiplier)
      .SetLoops(-1, LoopType.Incremental)
      .SetEase(Ease.Linear);
    _specialReelSpinTween.Play();
  }

  private void StopSpecialReelTweening()
  {
    if (!SpecialReelTransform) return;
    if (audioController) audioController.StopSpecialReelSpin();
    _specialReelSpinTween?.Kill();
    _specialReelSpinTween = null;
    if (_specialReelRecycleCoroutine != null)
    {
      StopCoroutine(_specialReelRecycleCoroutine);
      _specialReelRecycleCoroutine = null;
    }
    if (_specialReelImages != null)
    {
      for (int i = 0; i < _specialReelImages.Length; i++)
      {
        Vector3 pos = _specialReelImages[i].transform.localPosition;
        pos.y = _specialReelImageInitialLocalY[i];
        _specialReelImages[i].transform.localPosition = pos;
      }
    }
    SpecialReelTransform.localPosition = new Vector2(SpecialReelTransform.localPosition.x, topSlotImageY - IconSizeFactor);
    SpecialReelTransform.DOLocalMoveY(topSlotImageY, 0.25f).SetEase(Ease.OutQuad);
  }

  private IEnumerator StopTweening(int reqpos, Transform slotTransform, int index, bool isStop, float duration = 0.5f)
  {
    alltweens[index].Kill();

    if (recycleCoroutines[index] != null)
    {
      StopCoroutine(recycleCoroutines[index]);
      recycleCoroutines[index] = null;
    }

    if (reelImages[index] != null)
    {
      for (int i = 0; i < reelImages[index].Length; i++)
      {
        Vector3 pos = reelImages[index][i].transform.localPosition;
        pos.y = reelImageInitialLocalY[index][i];
        reelImages[index][i].transform.localPosition = pos;
      }
    }

    slotTransform.localPosition = new Vector2(slotTransform.localPosition.x, topSlotImageY - IconSizeFactor);
    alltweens[index] = slotTransform.DOLocalMoveY(topSlotImageY, 0.25f).SetEase(Ease.OutQuad);
    if (!isStop)
    {
      yield return new WaitForSeconds(0.2f);
    }
    else
    {
      yield return null;
    }
  }



  private void KillAllTweens()
  {
    for (int i = 0; i < numberOfSlots; i++)
    {
      alltweens[i].Kill();
      if (recycleCoroutines[i] != null)
      {
        StopCoroutine(recycleCoroutines[i]);
        recycleCoroutines[i] = null;
      }
    }
    alltweens.Clear();
    _specialReelSpinTween?.Kill();
    _specialReelSpinTween = null;
    if (_specialReelRecycleCoroutine != null)
    {
      StopCoroutine(_specialReelRecycleCoroutine);
      _specialReelRecycleCoroutine = null;
    }
  }
  #endregion

}

[Serializable]
public class SlotImage
{
  public List<Image> slotImages = new List<Image>(10);
}

