using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
  internal event Action<bool> OnInfoScreenToggled;

  [Header("Menu UI")]
  [SerializeField]
  private Button Menu_Button;
  [SerializeField]
  private GameObject Menu_Object;

  [Header("Settings UI")]
  [SerializeField]
  private Button Settings_Button;
  [SerializeField]
  private GameObject Settings_Object;
  [SerializeField]
  private RectTransform Settings_RT;

  [SerializeField]
  private Button Exit_Button;
  [SerializeField]
  private GameObject Exit_Object;

  [Header("Betting UI")]
  [SerializeField] private TMP_Text TotalBetAmountText;
  [SerializeField] private TMP_Text BalanceAmount;
  [SerializeField] private TMP_Text TotalBet_text;
  [SerializeField] private TMP_Text TotalWin_text;
  [SerializeField] private TMP_Text PinballPayoutText;
  // Same Double Gold Jackpot figure as TripleDouble7PayoutText (totalBet x 1000), but this is the
  // main-UI-screen object ("777PayoutText" in the scene — digits can't lead a C# identifier).
  [SerializeField] private TMP_Text SevenSevenSevenPayoutText;

  [Header("Game Content")]
  // The main game content root, scaled by win sequences (SkipWinSequences resets it). Not an intro.
  [SerializeField] private RectTransform GameContent;

  [Header("Bonus Win Sequence")]
  [SerializeField] private GameObject BonusWinSequencePanel;
  [SerializeField] private ImageAnimation BonusWinCoinFallingAnim;   // fullscreen coin shower (looping)
  [SerializeField] private RectTransform BonusWinPanel;
  [SerializeField] private TMP_Text BonusWinAmountText;
  [SerializeField] private float bonusWinShowDelay = 1f;
  [SerializeField] private float bonusWinScaleDuration = 0.4f;
  [SerializeField] private float bonusWinCountDuration = 1.5f;
  [SerializeField] private float bonusWinHoldDuration = 2f;

  private bool _spinWinActive;
  private bool _bonusWinActive;
  internal bool IsWinSequenceActive => _spinWinActive || _bonusWinActive;
  internal bool IsBonusWinActive => _bonusWinActive;   // spin-start blocks on this so the bonus win can't be skipped

  private Coroutine _spinWinCoroutine;
  private Coroutine _bonusWinCoroutine;

  [Header("Ticker UI")]
  [SerializeField] private RectTransform TickerContainer;
  [SerializeField] private TMP_Text TickerText;
  [SerializeField] private float TickerDuration = 3f;

  private readonly string[] tickerMessages = { "GOOD LUCK", "ALL THE BEST" };
  private int tickerIndex = 0;
  private Tween tickerTween;

  private List<double> betAmounts;
  internal int BetCount => betAmounts?.Count ?? 0;
  internal double GetBetAmount(int index) => betAmounts[index];
  private Dictionary<string, double> anyPayouts;   // group-payout lookup for the "mixed" combination paytable texts
  private double baseCoinValue = 1;   // anyPayouts values are 10x (baseCoinValue) too high against totalBet — confirmed live
  private readonly Dictionary<int, double> symbolPayoutMultipliers = new Dictionary<int, double>();

  [Header("Combination Payouts (paytable slide)")]
  [SerializeField] private TMP_Text TripleDouble7PayoutText;
  [SerializeField] private TMP_Text TripleRed7PayoutText;
  [SerializeField] private TMP_Text TripleBlue7PayoutText;
  [SerializeField] private TMP_Text TripleYellow7PayoutText;
  [SerializeField] private TMP_Text Triple5BarPayoutText;
  [SerializeField] private TMP_Text MixedSevensPayoutText;
  [SerializeField] private TMP_Text TripleBarPayoutText;
  [SerializeField] private TMP_Text MixedBarsPayoutText;
  [SerializeField] private TMP_Text MixedSevensBarsPayoutText;

  [Header("Information UI")]
  [SerializeField]
  private Button Info_Button;
  [SerializeField]
  private GameObject InfoSlidesPanel;
  [SerializeField]
  private Button BackToGame_Button;
  [SerializeField]
  private Button NextButton;
  [SerializeField]
  private Button PrevButton;
  [SerializeField]
  private List<GameObject> InfoSlideObjects;   // one child GameObject per slide, cycled by ShowSlide

  private int currentSlideIndex = 0;

  [Header("Popus UI")]
  [SerializeField]
  private GameObject PopupsPanel;

  [Header("About Popup")]
  [SerializeField]
  private GameObject AboutPopup_Object;
  [SerializeField]
  private Button AboutExit_Button;

  [Header("Settings Popup")]
  [SerializeField]
  private GameObject SettingsPopup_Object;
  [SerializeField]
  private Button SettingsExit_Button;
  [SerializeField]
  private Button Sound_Button;
  [SerializeField]
  private Button Music_Button;

  [SerializeField]
  private Sprite MusicOnSprite;
  [SerializeField]
  private Sprite MusicOffSprite;
  [SerializeField]
  private Sprite SoundOnSprite;
  [SerializeField]
  private Sprite SoundOffSprite;

  [Header("Spin Win Display")]
  [SerializeField] private GameObject SpinWinPanel;
  [SerializeField] private TMP_Text SpinWinText;
  [SerializeField] private float spinWinCountDuration = 2.0f;

  [Header("Disconnection Popup")]
  [SerializeField]
  private Button CloseDisconnect_Button;
  [SerializeField]
  private GameObject DisconnectPopup_Object;

  [Header("AnotherDevice Popup")]
  [SerializeField]
  private Button CloseAD_Button;
  [SerializeField]
  private GameObject ADPopup_Object;

  [Header("Reconnection Popup")]
  [SerializeField]
  private TMP_Text ReconnectingText;
  [SerializeField]
  private TMP_Text ReconnectingAttemptText;
  [SerializeField]
  private TMP_Text ReconnectingAttemptAmount;
  [SerializeField]
  private GameObject ReconnectPopup_Object;

  [Header("LowBalance Popup")]
  [SerializeField]
  private Button LBExit_Button;
  [SerializeField]
  private GameObject LBPopup_Object;

  [Header("Quit Popup")]
  [SerializeField]
  private GameObject QuitPopup_Object;
  [SerializeField]
  private Button YesQuit_Button;
  [SerializeField]
  private Button NoQuit_Button;
  [SerializeField]
  private Button CrossQuit_Button;

  [Header("References")]
  [SerializeField] private AudioManager audioManager;
  [SerializeField] private Button GameExit_Button;
  [SerializeField] private Button Home_Button;

  [SerializeField]
  private SlotBehaviour slotManager;

  private Tween _balanceTween;
  private bool isMusic = true;
  private bool isSound = true;
  internal bool isExit = false;

  private void Start()
  {
    //StartCoroutine(DebugBonusWinPreview());   // TEMP TEST: preview the bonus win sequence at startup

    if (Menu_Button) Menu_Button.onClick.RemoveAllListeners();
    if (Menu_Button) Menu_Button.onClick.AddListener(OpenMenu);

    if (Exit_Button) Exit_Button.onClick.RemoveAllListeners();
    if (Exit_Button) Exit_Button.onClick.AddListener(CloseMenu);

    if (AboutExit_Button) AboutExit_Button.onClick.RemoveAllListeners();
    if (AboutExit_Button) AboutExit_Button.onClick.AddListener(delegate { ClosePopup(AboutPopup_Object); });

    if (InfoSlidesPanel) InfoSlidesPanel.SetActive(false);

    if (Info_Button) Info_Button.onClick.RemoveAllListeners();
    if (Info_Button) Info_Button.onClick.AddListener(() =>
    {
      if (audioManager) audioManager.PlayInfoButton();
      currentSlideIndex = 0;
      InfoSlidesPanel.SetActive(true);
      ShowSlide(currentSlideIndex);
      OnInfoScreenToggled?.Invoke(true);

      if (BackToGame_Button) BackToGame_Button.onClick.RemoveAllListeners();
      if (BackToGame_Button) BackToGame_Button.onClick.AddListener(() =>
      {
        if (audioManager) audioManager.PlayInfoButton();
        InfoSlidesPanel.SetActive(false);
        OnInfoScreenToggled?.Invoke(false);
      });

      if (NextButton) NextButton.onClick.RemoveAllListeners();
      if (NextButton) NextButton.onClick.AddListener(() =>
      {
        currentSlideIndex = (currentSlideIndex + 1) % InfoSlideObjects.Count;
        ShowSlide(currentSlideIndex);
      });

      if (PrevButton) PrevButton.onClick.RemoveAllListeners();
      if (PrevButton) PrevButton.onClick.AddListener(() =>
      {
        currentSlideIndex = (currentSlideIndex - 1 + InfoSlideObjects.Count) % InfoSlideObjects.Count;
        ShowSlide(currentSlideIndex);
      });
    });

    if (Settings_Button) Settings_Button.onClick.RemoveAllListeners();
    if (Settings_Button) Settings_Button.onClick.AddListener(delegate { OpenPopup(SettingsPopup_Object); });

    if (SettingsExit_Button) SettingsExit_Button.onClick.RemoveAllListeners();
    if (SettingsExit_Button) SettingsExit_Button.onClick.AddListener(delegate { ClosePopup(SettingsPopup_Object); });

    SetButtonSprite(Music_Button, isMusic ? MusicOnSprite : MusicOffSprite);
    SetButtonSprite(Sound_Button, isSound ? SoundOnSprite : SoundOffSprite);

    if (GameExit_Button) GameExit_Button.onClick.RemoveAllListeners();
    if (GameExit_Button) GameExit_Button.onClick.AddListener(delegate
    {
      OpenPopup(QuitPopup_Object);
    });

    if (Home_Button) Home_Button.onClick.RemoveAllListeners();
    if (Home_Button) Home_Button.onClick.AddListener(delegate { OpenPopup(QuitPopup_Object); });

    if (NoQuit_Button) NoQuit_Button.onClick.RemoveAllListeners();
    if (NoQuit_Button) NoQuit_Button.onClick.AddListener(delegate
    {
      if (!isExit)
      {
        ClosePopup(QuitPopup_Object);
      }
    });

    if (CrossQuit_Button) CrossQuit_Button.onClick.RemoveAllListeners();
    if (CrossQuit_Button) CrossQuit_Button.onClick.AddListener(delegate
    {
      if (!isExit)
      {
        ClosePopup(QuitPopup_Object);
      }
    });

    if (LBExit_Button) LBExit_Button.onClick.RemoveAllListeners();
    if (LBExit_Button) LBExit_Button.onClick.AddListener(delegate { ClosePopup(LBPopup_Object); });

    if (YesQuit_Button) YesQuit_Button.onClick.RemoveAllListeners();
    if (YesQuit_Button) YesQuit_Button.onClick.AddListener(delegate
    {
      CallOnExitFunction();
      Debug.Log("quit event: pressed YES Button ");

    });

    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.RemoveAllListeners();
    if (CloseDisconnect_Button) CloseDisconnect_Button.onClick.AddListener(CallOnExitFunction); //BackendChanges

    if (CloseAD_Button) CloseAD_Button.onClick.RemoveAllListeners();
    if (CloseAD_Button) CloseAD_Button.onClick.AddListener(CallOnExitFunction);

    isMusic = audioManager == null || audioManager.MusicEnabled;
    isSound = audioManager == null || audioManager.SfxEnabled;

    if (Sound_Button) Sound_Button.onClick.RemoveAllListeners();
    if (Sound_Button) Sound_Button.onClick.AddListener(ToggleSound);

    if (Music_Button) Music_Button.onClick.RemoveAllListeners();
    if (Music_Button) Music_Button.onClick.AddListener(ToggleMusic);
  }


  internal void LowBalPopup()
  {
    OpenPopup(LBPopup_Object);
  }

  internal void DisconnectionPopup()
  {
    if (!isExit)
    {
      if (ReconnectPopup_Object) ReconnectPopup_Object.SetActive(false);
      OpenPopup(DisconnectPopup_Object);
    }
  }

  internal void ReconnectionPopup(int attempt, int max)
  {
    if (ReconnectingText) ReconnectingText.text = "Reconnecting...";
    UpdateReconnectingAttempt(attempt, max);
    OpenPopup(ReconnectPopup_Object);
  }

  internal void UpdateReconnectingAttempt(int attempt, int max)
  {
    if (ReconnectingAttemptAmount) ReconnectingAttemptAmount.text = $"{attempt}/{max}";
  }

  internal void CheckAndClosePopups()
  {
    if (ReconnectPopup_Object != null && ReconnectPopup_Object.activeInHierarchy)
    {
      ClosePopup(ReconnectPopup_Object);
    }
    if (DisconnectPopup_Object != null && DisconnectPopup_Object.activeInHierarchy)
    {
      ClosePopup(DisconnectPopup_Object);
    }
  }

  internal void ADfunction()
  {
    OpenPopup(ADPopup_Object);
  }

  private void CallOnExitFunction()
  {
    if (!isExit)
    {
      isExit = true;
      if (audioManager) audioManager.PlayButton();
      slotManager.CallCloseSocket();
    }
  }

  private void OpenMenu()
  {
    audioManager.PlayButton();
    if (Menu_Object) Menu_Object.SetActive(false);
    if (Exit_Object) Exit_Object.SetActive(true);
    if (Settings_Object) Settings_Object.SetActive(true);

    DOTween.To(() => Settings_RT.anchoredPosition, (val) => Settings_RT.anchoredPosition = val, new Vector2(Settings_RT.anchoredPosition.x, Settings_RT.anchoredPosition.y + 250), 0.1f).OnUpdate(() =>
    {
      LayoutRebuilder.ForceRebuildLayoutImmediate(Settings_RT);
    });
  }

  private void CloseMenu()
  {

    if (audioManager) audioManager.PlayButton();
    DOTween.To(() => Settings_RT.anchoredPosition, (val) => Settings_RT.anchoredPosition = val, new Vector2(Settings_RT.anchoredPosition.x, Settings_RT.anchoredPosition.y - 250), 0.1f).OnUpdate(() =>
    {
      LayoutRebuilder.ForceRebuildLayoutImmediate(Settings_RT);
    });

    DOVirtual.DelayedCall(0.1f, () =>
     {
       if (Menu_Object) Menu_Object.SetActive(true);
       if (Exit_Object) Exit_Object.SetActive(false);
       if (Settings_Object) Settings_Object.SetActive(false);
     });
  }

  private void OpenPopup(GameObject Popup)
  {
    if (audioManager) audioManager.PlayButton();
    if (Popup) Popup.SetActive(true);
    if (PopupsPanel) PopupsPanel.SetActive(true);
  }

  internal void ClosePopup(GameObject Popup)
  {
    if (audioManager) audioManager.PlayButton();
    if (Popup) Popup.SetActive(false);
    if (DisconnectPopup_Object == null || !DisconnectPopup_Object.activeSelf)
    {
      if (PopupsPanel) PopupsPanel.SetActive(false);
    }
  }

  private void ToggleMusic()
  {
    isMusic = !isMusic;
    SetButtonSprite(Music_Button, isMusic ? MusicOnSprite : MusicOffSprite);
    if (audioManager) audioManager.PlayButton();
    if (audioManager) audioManager.SetMusicEnabled(isMusic);
  }

  private void ToggleSound()
  {
    isSound = !isSound;
    SetButtonSprite(Sound_Button, isSound ? SoundOnSprite : SoundOffSprite);
    if (audioManager) audioManager.PlayButton();
    if (audioManager) audioManager.SetSfxEnabled(isSound);
  }

  // Bridge for the WebGL focus path: SocketIOManager holds no AudioManager reference,
  // so it routes through here to reach the same SetMuteAll the native path calls.
  internal void SetFocusMute(bool forceMute)
  {
    if (audioManager) audioManager.SetMuteAll(forceMute);
  }

  private void SetButtonSprite(Button button, Sprite sprite)
  {
    if (button == null || sprite == null) return;
    var img = button.GetComponent<Image>();
    if (img) img.sprite = sprite;
  }

  internal void InitialiseBalanceAndWin(double balance, double bet)
  {
    if (BalanceAmount) BalanceAmount.text = balance.ToString("F3");
    if (TotalBet_text) TotalBet_text.text = bet.ToString();
    if (TotalWin_text) TotalWin_text.text = "0.000";
  }

  internal void PlaySpinWin(double winAmount)
  {
    if (_spinWinCoroutine != null) StopCoroutine(_spinWinCoroutine);
    _spinWinCoroutine = StartCoroutine(SpinWinRoutine(winAmount));
  }

  private IEnumerator SpinWinRoutine(double winAmount)
  {
    if (winAmount <= 0) yield break;
    if (_bonusWinActive) yield break;
    _spinWinActive = true;
    if (SpinWinPanel) SpinWinPanel.SetActive(true);
    float display = 0f;
    if (TotalWin_text)
    {
      TotalWin_text.text = "0.000";
      DOTween.To(() => display, v => { TotalWin_text.text = v.ToString("F3"); }, (float)winAmount, spinWinCountDuration)
        .SetTarget(TotalWin_text);
    }
    if (SpinWinText)
    {
      if (audioManager) audioManager.PlayCountLoop();
      yield return DOTween.To(() => display, v => { display = v; SpinWinText.text = TextFormat.ToSpriteDigits(v.ToString("F2")); },
        (float)winAmount, spinWinCountDuration).SetTarget(SpinWinText).WaitForCompletion();
      if (audioManager) { audioManager.StopCountLoop(); audioManager.PlayCountStop(); }
    }
    yield return new WaitForSeconds(0.5f);
    HideSpinWin();
    _spinWinActive = false;
    _spinWinCoroutine = null;
  }

  internal void HideSpinWin()
  {
    if (SpinWinPanel) SpinWinPanel.SetActive(false);
    if (SpinWinText) SpinWinText.text = "";
  }

  internal void ResetTotalWin()
  {
    if (TotalWin_text) TotalWin_text.text = "0.000";
  }

  internal void UpdateTotalWin(double amount)
  {
    if (TotalWin_text) TotalWin_text.text = amount.ToString("F3");
  }

  internal void UpdateBalance(double amount)
  {
    _balanceTween?.Kill();
    if (BalanceAmount) BalanceAmount.text = amount.ToString("F3");
  }

  internal void AnimateBalanceDeduction(double from, double to)
  {
    _balanceTween?.Kill();
    double current = from;
    _balanceTween = DOTween.To(() => current, v => { current = v; if (BalanceAmount) BalanceAmount.text = current.ToString("F3"); }, to, 0.8f);
  }

  internal void InitialiseUI(List<double> bets, List<Symbol> symbols, Dictionary<string, double> anyPayouts = null, double baseCoinValue = 1)
  {
    betAmounts = bets;
    this.anyPayouts = anyPayouts;
    this.baseCoinValue = baseCoinValue > 0 ? baseCoinValue : 1;

    symbolPayoutMultipliers.Clear();
    if (symbols != null)
      foreach (Symbol symbol in symbols)
        symbolPayoutMultipliers[symbol.id] = symbol.payout;
  }

  internal void SetBet(double totalBet)
  {
    UpdateBetDisplay(totalBet);
    if (PinballPayoutText) PinballPayoutText.text = (totalBet * 500).ToString("F2");
    if (SevenSevenSevenPayoutText) SevenSevenSevenPayoutText.text = (totalBet * 2000 / baseCoinValue).ToString("F2");
    UpdateSymbolPayoutTexts(totalBet);
  }

  private void UpdateSymbolPayoutTexts(double totalBet)
  {
    // Three-of-a-kind combos = the symbol's own payout, by id: Red7=1, Blue7=2, Yellow7=3, 5Bar=4, Bar=5.
    SetSymbolPayoutText(TripleRed7PayoutText, 1, totalBet);
    SetSymbolPayoutText(TripleBlue7PayoutText, 2, totalBet);
    SetSymbolPayoutText(TripleYellow7PayoutText, 3, totalBet);
    SetSymbolPayoutText(Triple5BarPayoutText, 4, totalBet);
    SetSymbolPayoutText(TripleBarPayoutText, 5, totalBet);

    // Double Gold Jackpot: totalBet x 2000, flat (not a per-symbol payout lookup); / baseCoinValue like
    // the Mixed combos below — same underlying "bet amount 10" ambiguity caused both bugs.
    if (TripleDouble7PayoutText) TripleDouble7PayoutText.text = (totalBet * 2000 / baseCoinValue).ToString("F2");

    // Mixed combos: group-level payout, not tied to one symbol id.
    SetAnyPayoutText(MixedSevensPayoutText, totalBet, "sevens");
    SetAnyPayoutText(MixedBarsPayoutText, totalBet, "bars");
    // Live payload echoes this key as "defaults" (plural); the static config calls it "default" —
    // try both so either shape works.
    SetAnyPayoutText(MixedSevensBarsPayoutText, totalBet, "defaults", "default");
  }

  private void SetAnyPayoutText(TMP_Text text, double totalBet, params string[] keys)
  {
    if (text == null || anyPayouts == null) return;
    foreach (string key in keys)
      if (anyPayouts.TryGetValue(key, out double value))
      {
        text.text = (value * totalBet / baseCoinValue).ToString("F2");
        return;
      }
  }

  private void SetSymbolPayoutText(TMP_Text text, int symbolId, double totalBet)
  {
    if (text && symbolPayoutMultipliers.TryGetValue(symbolId, out double multiplier))
      text.text = (multiplier * totalBet).ToString("F2");
  }

  internal void ShowTicker()
  {
    if (TickerContainer == null || TickerText == null) return;

    tickerTween?.Kill();

    TickerText.text = tickerMessages[tickerIndex];
    tickerIndex = (tickerIndex + 1) % tickerMessages.Length;

    float containerWidth = TickerContainer.rect.width;
    float startX = containerWidth / 2f + TickerText.preferredWidth / 2f;
    float endX = -(containerWidth / 2f + TickerText.preferredWidth / 2f);

    TickerText.rectTransform.anchoredPosition = new Vector2(startX, TickerText.rectTransform.anchoredPosition.y);
    tickerTween = TickerText.rectTransform.DOAnchorPosX(endX, TickerDuration).SetEase(Ease.Linear);
  }

  internal void PlayBonusWinSequence(double totalWin)
  {
    if (_bonusWinCoroutine != null) StopCoroutine(_bonusWinCoroutine);
    _bonusWinCoroutine = StartCoroutine(BonusWinRoutine(totalWin));
  }

  private IEnumerator BonusWinRoutine(double totalWin)
  {
    _bonusWinActive = true;

    yield return new WaitForSeconds(bonusWinShowDelay);

    if (BonusWinAmountText) BonusWinAmountText.text = TextFormat.ToSpriteDigits("0.00");
    if (BonusWinSequencePanel) BonusWinSequencePanel.SetActive(true);
    if (BonusWinPanel) { ImageAnimation panelAnim = BonusWinPanel.GetComponent<ImageAnimation>(); if (panelAnim) panelAnim.StartAnimation(); }
    if (BonusWinCoinFallingAnim) { BonusWinCoinFallingAnim.doLoopAnimation = true; BonusWinCoinFallingAnim.StartAnimation(); }

    if (audioManager) audioManager.PlayBigWin();

    float bonusWinDisplay = 0f;
    if (BonusWinAmountText)
    {
      if (audioManager) audioManager.PlayCountLoop();
      yield return DOTween.To(() => bonusWinDisplay, v => { bonusWinDisplay = v; BonusWinAmountText.text = TextFormat.ToSpriteDigits(v.ToString("F2")); },
        (float)totalWin, bonusWinCountDuration).SetTarget(BonusWinAmountText).WaitForCompletion();
      if (audioManager) { audioManager.StopCountLoop(); audioManager.PlayCountStop(); }
    }
    else
      yield return new WaitForSeconds(bonusWinCountDuration);

    yield return new WaitForSeconds(bonusWinHoldDuration);

    if (BonusWinCoinFallingAnim) { BonusWinCoinFallingAnim.StopAnimation(); BonusWinCoinFallingAnim.doLoopAnimation = false; }
    if (BonusWinSequencePanel) BonusWinSequencePanel.SetActive(false);
    _bonusWinActive = false;
    _bonusWinCoroutine = null;
  }

  private void UpdateBetDisplay(double bet)
  {
    if (TotalBetAmountText) TotalBetAmountText.text = bet.ToString("F2");
  }

  private void ShowSlide(int index)
  {
    for (int i = 0; i < InfoSlideObjects.Count; i++)
      if (InfoSlideObjects[i]) InfoSlideObjects[i].SetActive(i == index);
  }

  internal void SkipWinSequences()
  {
    if (_spinWinCoroutine != null) { StopCoroutine(_spinWinCoroutine); _spinWinCoroutine = null; }
    if (_bonusWinCoroutine != null) { StopCoroutine(_bonusWinCoroutine); _bonusWinCoroutine = null; }

    if (SpinWinPanel) SpinWinPanel.transform.DOKill();
    if (TotalWin_text) DOTween.Kill(TotalWin_text);
    if (SpinWinText) DOTween.Kill(SpinWinText);
    if (BonusWinAmountText) DOTween.Kill(BonusWinAmountText);

    HideSpinWin();
    if (BonusWinCoinFallingAnim) { BonusWinCoinFallingAnim.StopAnimation(); BonusWinCoinFallingAnim.doLoopAnimation = false; }
    if (BonusWinSequencePanel) BonusWinSequencePanel.SetActive(false);
    if (GameContent) { GameContent.DOKill(); GameContent.localScale = Vector3.one; }

    _spinWinActive = false;
    _bonusWinActive = false;
  }

  // TODO: Add scale animation for BonusWinAmountText — frames/timing TBD with team
  // TEMP TEST: at startup wait 1s, play the bonus win sequence, wait until it finishes and deactivates,
  // then wait 1.5s and play it again. Remove this and its StartCoroutine call in Start() when done.
  private IEnumerator DebugBonusWinPreview()
  {
    yield return new WaitForSeconds(1f);
    PlayBonusWinSequence(100);
    yield return new WaitUntil(() => !_bonusWinActive);
    yield return new WaitForSeconds(1.5f);
    PlayBonusWinSequence(100);
  }

}