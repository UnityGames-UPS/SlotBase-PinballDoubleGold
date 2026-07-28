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
  [SerializeField] private TMP_Text PayoutText;

  [Header("Game Content")]
  // The main game content root, scaled by win sequences (SkipWinSequences resets it). Not an intro.
  [SerializeField] private RectTransform GameContent;

  [Header("Big Win Sequence")]
  [SerializeField] private GameObject BigWinSequencePanel;
  [SerializeField] private RectTransform BigWinPanel;
  [SerializeField] private RectTransform BigWinAmountPanel;
  [SerializeField] private TMP_Text BigWinAmountText;
  [SerializeField] private float bigWinShowDelay = 0f;
  [SerializeField] private float bigWinCountDuration = 1.5f;
  [SerializeField] private float bigWinHoldDuration = 6.5f;
  [SerializeField] private int bigWinAmountShowFrame = 105;
  [SerializeField] private int bigWinAmountHideFrame = 175;

  [Header("Bonus Win Sequence")]
  [SerializeField] private GameObject BonusWinSequencePanel;
  [SerializeField] private CoinFountainPool coinFountainPool;   // pooled coin-spray; replaces the old fullscreen coin ImageAnimation
  [SerializeField] private RectTransform BonusWinPanel;
  [SerializeField] private TMP_Text BonusWinAmountText;
  [SerializeField] private float bonusWinShowDelay = 1f;
  [SerializeField] private float bonusWinScaleDuration = 0.4f;
  [SerializeField] private float bonusWinCountDuration = 1.5f;
  [SerializeField] private float bonusWinHoldDuration = 2f;
  [SerializeField] private float bonusWinCoinFadeDuration = 0.5f;   // coin fountain fade-out at the end

  private bool _spinWinActive;
  private bool _bonusWinActive;
  private bool _bigWinActive;
  internal bool IsWinSequenceActive => _spinWinActive || _bonusWinActive || _bigWinActive;
  internal bool IsBonusWinActive => _bonusWinActive;   // spin-start blocks on this so the bonus win can't be skipped

  private Coroutine _spinWinCoroutine;
  private Coroutine _bonusWinCoroutine;
  private Coroutine _bigWinCoroutine;
  private Coroutine _bigWinAmountCoroutine;

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
  private double maxPayoutMultiplier;
  private readonly Dictionary<int, double> symbolPayoutMultipliers = new Dictionary<int, double>();

  [Header("Symbol Payout Info Slide")]
  [SerializeField] private GameObject SymbolPayoutOverlay;
  [SerializeField] private int symbolPayoutSlideIndex = -1;
  [SerializeField] private TMP_Text SingleBARPayoutText;
  [SerializeField] private TMP_Text DoubleBARPayoutText;
  [SerializeField] private TMP_Text TripleBARPayoutText;
  [SerializeField] private TMP_Text BellPayoutText;
  [SerializeField] private TMP_Text Red7PayoutText;
  [SerializeField] private TMP_Text Wild2xPayoutText;
  [SerializeField] private TMP_Text Wild3xPayoutText;
  [SerializeField] private TMP_Text Wild5xPayoutText;
  [SerializeField] private TMP_Text Wild10xPayoutText;

  [Header("Wild Combination Payout Info Slide")]
  [SerializeField] private GameObject WildComboPayoutOverlay;
  [SerializeField] private int wildComboPayoutSlideIndex = -1;
  [SerializeField] private TMP_Text WildCombo3PayoutText;
  [SerializeField] private TMP_Text WildCombo5PayoutText;
  [SerializeField] private TMP_Text WildCombo10PayoutText;

  // Not provided by the live backend - sourced from txt_config.json wildRules.mixedWildPayouts
  private const double WildCombo3Payout = 8;
  private const double WildCombo5Payout = 15;
  private const double WildCombo10Payout = 50;

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
  private Image SlideContainer;
  [SerializeField]
  private Sprite[] InfoSlides;

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
  [SerializeField] private GameObject SpinWinCoinSplash;
  [SerializeField] private ImageAnimation SpinWinCoinSplashAnim;
  [SerializeField] private float spinWinCountDuration = 1f;

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
    // StartCoroutine(DebugBonusWinPreview());   // TEMP TEST: preview the bonus win sequence at startup

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
      if (audioManager) audioManager.PlayUIClick();
      currentSlideIndex = 0;
      InfoSlidesPanel.SetActive(true);
      ShowSlide(currentSlideIndex);
      OnInfoScreenToggled?.Invoke(true);

      if (BackToGame_Button) BackToGame_Button.onClick.RemoveAllListeners();
      if (BackToGame_Button) BackToGame_Button.onClick.AddListener(() =>
      {
        InfoSlidesPanel.SetActive(false);
        OnInfoScreenToggled?.Invoke(false);
      });

      if (NextButton) NextButton.onClick.RemoveAllListeners();
      if (NextButton) NextButton.onClick.AddListener(() =>
      {
        currentSlideIndex = (currentSlideIndex + 1) % InfoSlides.Length;
        ShowSlide(currentSlideIndex);
      });

      if (PrevButton) PrevButton.onClick.RemoveAllListeners();
      if (PrevButton) PrevButton.onClick.AddListener(() =>
      {
        currentSlideIndex = (currentSlideIndex - 1 + InfoSlides.Length) % InfoSlides.Length;
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
    if (audioManager) audioManager.PlayUIClick();
    if (audioManager) audioManager.SetMusicEnabled(isMusic);
  }

  private void ToggleSound()
  {
    isSound = !isSound;
    SetButtonSprite(Sound_Button, isSound ? SoundOnSprite : SoundOffSprite);
    if (audioManager) audioManager.PlayUIClick();
    if (audioManager) audioManager.SetSfxEnabled(isSound);
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
    if (_bigWinActive || _bonusWinActive) yield break;
    _spinWinActive = true;
    if (audioManager) audioManager.PlayNormalIcon();
    if (SpinWinPanel)
    {
      SpinWinPanel.SetActive(true);
      SpinWinPanel.transform.localScale = Vector3.one;
      SpinWinPanel.transform.DOScale(1.2f, spinWinCountDuration).SetEase(Ease.OutQuad);
    }
    if (SpinWinCoinSplash) SpinWinCoinSplash.SetActive(true);
    if (SpinWinCoinSplashAnim)
    {
      SpinWinCoinSplashAnim.doLoopAnimation = false;
      SpinWinCoinSplashAnim.StartAnimation();
    }
    float display = 0f;
    if (TotalWin_text)
    {
      TotalWin_text.text = "0.000";
      DOTween.To(() => display, v => { TotalWin_text.text = v.ToString("F3"); }, (float)winAmount, spinWinCountDuration)
        .SetTarget(TotalWin_text);
    }
    if (SpinWinText)
      yield return DOTween.To(() => display, v => { display = v; SpinWinText.text = v.ToString("F3"); },
        (float)winAmount, spinWinCountDuration).SetTarget(SpinWinText).WaitForCompletion();
    yield return new WaitForSeconds(0.5f);
    HideSpinWin();
    _spinWinActive = false;
    _spinWinCoroutine = null;
  }

  internal void HideSpinWin()
  {
    if (SpinWinPanel) SpinWinPanel.SetActive(false);
    if (SpinWinCoinSplash) SpinWinCoinSplash.SetActive(false);
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

  internal void InitialiseUI(List<double> bets, List<Symbol> symbols)
  {
    betAmounts = bets;

    maxPayoutMultiplier = 0;
    symbolPayoutMultipliers.Clear();
    if (symbols != null)
    {
      foreach (Symbol symbol in symbols)
      {
        symbolPayoutMultipliers[symbol.id] = symbol.payout;
        if (symbol.payout > maxPayoutMultiplier) maxPayoutMultiplier = symbol.payout;
      }
    }
  }

  internal void SetBet(double totalBet)
  {
    UpdateBetDisplay(totalBet);
    if (PayoutText) PayoutText.text = (maxPayoutMultiplier * totalBet).ToString("F2");
    UpdateSymbolPayoutTexts(totalBet);
    UpdateWildComboPayoutTexts(totalBet);
  }

  private void UpdateSymbolPayoutTexts(double totalBet)
  {
    SetSymbolPayoutText(SingleBARPayoutText, 1, totalBet);
    SetSymbolPayoutText(DoubleBARPayoutText, 2, totalBet);
    SetSymbolPayoutText(TripleBARPayoutText, 3, totalBet);
    SetSymbolPayoutText(BellPayoutText, 4, totalBet);
    SetSymbolPayoutText(Red7PayoutText, 5, totalBet);
    SetSymbolPayoutText(Wild2xPayoutText, 6, totalBet);
    SetSymbolPayoutText(Wild3xPayoutText, 7, totalBet);
    SetSymbolPayoutText(Wild5xPayoutText, 8, totalBet);
    SetSymbolPayoutText(Wild10xPayoutText, 9, totalBet);
  }

  private void SetSymbolPayoutText(TMP_Text text, int symbolId, double totalBet)
  {
    if (text && symbolPayoutMultipliers.TryGetValue(symbolId, out double multiplier))
      text.text = (multiplier * totalBet).ToString("F2");
  }

  private void UpdateWildComboPayoutTexts(double totalBet)
  {
    if (WildCombo3PayoutText) WildCombo3PayoutText.text = (WildCombo3Payout * totalBet).ToString("F2");
    if (WildCombo5PayoutText) WildCombo5PayoutText.text = (WildCombo5Payout * totalBet).ToString("F2");
    if (WildCombo10PayoutText) WildCombo10PayoutText.text = (WildCombo10Payout * totalBet).ToString("F2");
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

    if (BonusWinAmountText) BonusWinAmountText.text = "0.000";
    if (BonusWinSequencePanel) BonusWinSequencePanel.SetActive(true);
    if (BonusWinPanel) { ImageAnimation panelAnim = BonusWinPanel.GetComponent<ImageAnimation>(); if (panelAnim) panelAnim.StartAnimation(); }
    if (coinFountainPool) coinFountainPool.StartFountain();   // pooled coins spray up from the panel (was the fullscreen ImageAnimation)

    if (audioManager) audioManager.PlayBigWin();

    float bonusWinDisplay = 0f;
    if (BonusWinAmountText)
      yield return DOTween.To(() => bonusWinDisplay, v => { bonusWinDisplay = v; BonusWinAmountText.text = v.ToString("F3"); },
        (float)totalWin, bonusWinCountDuration).SetTarget(BonusWinAmountText).WaitForCompletion();
    else
      yield return new WaitForSeconds(bonusWinCountDuration);

    yield return new WaitForSeconds(bonusWinHoldDuration);

    // Stop new waves, fade the coins still in the air, then reclaim them.
    if (coinFountainPool)
    {
      coinFountainPool.StopFountain();
      coinFountainPool.FadeOutAllActive(bonusWinCoinFadeDuration);
    }
    yield return new WaitForSeconds(bonusWinCoinFadeDuration);
    if (coinFountainPool) coinFountainPool.ClearAll();
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
    if (SlideContainer && InfoSlides != null && InfoSlides.Length > 0)
      SlideContainer.sprite = InfoSlides[index];

    if (SymbolPayoutOverlay) SymbolPayoutOverlay.SetActive(index == symbolPayoutSlideIndex);
    if (WildComboPayoutOverlay) WildComboPayoutOverlay.SetActive(index == wildComboPayoutSlideIndex);
  }

  internal void PlayBigWinSequence(double totalWin)
  {
    if (_bigWinCoroutine != null) StopCoroutine(_bigWinCoroutine);
    _bigWinCoroutine = StartCoroutine(BigWinRoutine(totalWin));
  }

  private IEnumerator BigWinRoutine(double totalWin)
  {
    _bigWinActive = true;

    yield return new WaitForSeconds(bigWinShowDelay);

    if (BigWinAmountText) { BigWinAmountText.text = "0.000"; BigWinAmountText.gameObject.SetActive(false); }
    if (BigWinSequencePanel) BigWinSequencePanel.SetActive(true);
    // BigWinAmountPanel scale-in disabled — board is now baked into the new BigWinPanel animation.
    // Deactivated in-scene instead of removed in case it's needed again later.
    // if (BigWinAmountPanel)
    // {
    //   Vector3 finalScale = BigWinAmountPanel.localScale;
    //   BigWinAmountPanel.localScale = Vector3.zero;
    //   BigWinAmountPanel.DOScale(finalScale, 0.6f).SetEase(Ease.OutBack).SetDelay(0.2f);
    // }
    ImageAnimation panelAnim = null;
    if (BigWinPanel)
    {
      panelAnim = BigWinPanel.GetComponent<ImageAnimation>();
      if (panelAnim) panelAnim.StartAnimation();
    }
    if (audioManager) audioManager.PlayBigWin();

    if (_bigWinAmountCoroutine != null) StopCoroutine(_bigWinAmountCoroutine);
    _bigWinAmountCoroutine = StartCoroutine(BigWinAmountRoutine(panelAnim, totalWin));

    yield return new WaitForSeconds(bigWinCountDuration + bigWinHoldDuration);

    if (BigWinSequencePanel) BigWinSequencePanel.SetActive(false);
    _bigWinActive = false;
    _bigWinCoroutine = null;
  }

  private IEnumerator BigWinAmountRoutine(ImageAnimation panelAnim, double totalWin)
  {
    if (BigWinAmountText == null) yield break;

    float showAt = 0f;
    float hideAt = 0f;
    if (panelAnim != null && panelAnim.textureArray != null && panelAnim.textureArray.Count > 0)
    {
      float perFrameDelay = panelAnim.GetTotalDuration() / panelAnim.textureArray.Count;
      showAt = perFrameDelay * bigWinAmountShowFrame;
      hideAt = perFrameDelay * bigWinAmountHideFrame;
    }

    yield return new WaitForSeconds(showAt);

    BigWinAmountText.text = "0.000";
    BigWinAmountText.gameObject.SetActive(true);
    Vector3 finalTextScale = BigWinAmountText.rectTransform.localScale;
    BigWinAmountText.rectTransform.localScale = Vector3.zero;
    BigWinAmountText.rectTransform.DOScale(finalTextScale, 0.6f).SetEase(Ease.OutBack);

    float bigWinDisplay = 0f;
    DOTween.To(() => bigWinDisplay, v => { bigWinDisplay = v; BigWinAmountText.text = v.ToString("F3"); },
      (float)totalWin, bigWinCountDuration).SetTarget(BigWinAmountText);

    yield return new WaitForSeconds(Mathf.Max(0f, hideAt - showAt));

    DOTween.Kill(BigWinAmountText);
    BigWinAmountText.rectTransform.DOKill();
    BigWinAmountText.gameObject.SetActive(false);
    _bigWinAmountCoroutine = null;
  }

  internal void SkipWinSequences()
  {
    if (_spinWinCoroutine != null) { StopCoroutine(_spinWinCoroutine); _spinWinCoroutine = null; }
    if (_bonusWinCoroutine != null) { StopCoroutine(_bonusWinCoroutine); _bonusWinCoroutine = null; }
    if (_bigWinCoroutine != null) { StopCoroutine(_bigWinCoroutine); _bigWinCoroutine = null; }
    if (_bigWinAmountCoroutine != null) { StopCoroutine(_bigWinAmountCoroutine); _bigWinAmountCoroutine = null; }

    if (SpinWinPanel) SpinWinPanel.transform.DOKill();
    if (TotalWin_text) DOTween.Kill(TotalWin_text);
    if (SpinWinText) DOTween.Kill(SpinWinText);
    if (BonusWinAmountText) DOTween.Kill(BonusWinAmountText);
    if (BigWinAmountText) { DOTween.Kill(BigWinAmountText); BigWinAmountText.rectTransform.DOKill(); }

    HideSpinWin();
    if (coinFountainPool) coinFountainPool.ClearAll();
    if (BonusWinSequencePanel) BonusWinSequencePanel.SetActive(false);
    if (BigWinSequencePanel) BigWinSequencePanel.SetActive(false);
    if (GameContent) { GameContent.DOKill(); GameContent.localScale = Vector3.one; }

    _spinWinActive = false;
    _bonusWinActive = false;
    _bigWinActive = false;
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