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
  // Container holding the machine (at y 0) and the bonus UI (one screen above). Scrolling it down
  // by scrollDistance sends the machine off the bottom and brings the bonus UI to centre.
  [SerializeField] private RectTransform scrollContainer;
  [SerializeField] private CanvasGroup baseGameUI;       // surrounding base UI that fades out (not the machine)
  [SerializeField] private CanvasGroup bonusDecorations; // ring/ships/marbles/counters that fade in after scroll
  [SerializeField] private float scrollDistance = 1080f;
  [SerializeField] private float scrollDuration = 1f;
  [SerializeField] private Ease scrollEase = Ease.InOutCubic;
  [SerializeField] private float fadeDuration = 0.4f;

  [Header("Controls & Displays")]
  [SerializeField] private Button shootButton;
  [SerializeField] private TMP_Text shotsText;
  [SerializeField] private TMP_Text bonusWinText;
  [SerializeField] private TMP_Text totalBetText;

  [Header("Ring")]
  // Ordered path circles the ball travels through (outer loop then inner layer). The animation
  // lights them in sequence to convey the ball moving.
  [SerializeField] private List<GameObject> pathCircleLights;
  // Destination pockets keyed to the backend prize lists: prizePockets[selectedIndex] normally,
  // specialPockets[selectedIndex] when payload.isSpecial. Sizes should match prizes[]/specialPrizes[].
  [SerializeField] private List<PinballPocket> prizePockets;
  [SerializeField] private List<PinballPocket> specialPockets;

  [Header("Ring Animation Timing")]
  [SerializeField] private float perCircleLightDuration = 0.05f;
  [SerializeField] private float pocketHighlightDuration = 0.9f;
  [SerializeField] private float endHoldDuration = 1.5f;

  // Runtime state
  private int _betIndex;
  private double _totalBet;
  private int _shotsRemaining;
  private bool _featureActive;
  private bool _shotInFlight;
  private float _scrollHomeY;

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
    UpdateShotsText(_shotsRemaining);
    UpdateBonusWinText(0);
    if (totalBetText) totalBetText.text = _totalBet.ToString("F2");
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
    UpdateShotsText(_shotsRemaining);
    UpdateBonusWinText(p.bonusState.totalBonusWin);

    _shotInFlight = false;

    if (p.isOver)
      yield return StartCoroutine(EndBonus());
    else
      SetShootInteractable(_shotsRemaining > 0);
  }
  #endregion

  #region Ring animation
  // Lights path circles in sequence up to the destination pocket's rest position, then highlights
  // the pocket. Backend gives only the destination (isSpecial + selectedIndex); the traversal is
  // synthesized here.
  private IEnumerator AnimateShot(bool isSpecial, int selectedIndex)
  {
    PinballPocket dest = GetPocket(isSpecial, selectedIndex);
    int stop = (dest != null && pathCircleLights != null && dest.ringStopIndex >= 0)
      ? Mathf.Min(dest.ringStopIndex, pathCircleLights.Count - 1)
      : (pathCircleLights != null ? pathCircleLights.Count - 1 : -1);

    for (int i = 0; i <= stop; i++)
    {
      SetLight(i, true);
      yield return new WaitForSeconds(perCircleLightDuration);
      if (i < stop) SetLight(i, false); // single moving light reads as the ball travelling
    }

    if (dest != null && dest.highlight) dest.highlight.SetActive(true);
    yield return new WaitForSeconds(pocketHighlightDuration);

    ClearAllLights();
    if (dest != null && dest.highlight) dest.highlight.SetActive(false);
  }

  private PinballPocket GetPocket(bool isSpecial, int index)
  {
    List<PinballPocket> pockets = isSpecial ? specialPockets : prizePockets;
    if (pockets == null || index < 0 || index >= pockets.Count)
    {
      Debug.LogWarning($"[PinballBonus] No pocket wired for isSpecial={isSpecial}, selectedIndex={index} — ball will stop at the ring end.");
      return null;
    }
    return pockets[index];
  }

  private void SetLight(int index, bool on)
  {
    if (pathCircleLights != null && index >= 0 && index < pathCircleLights.Count && pathCircleLights[index])
      pathCircleLights[index].SetActive(on);
  }

  private void ClearAllLights()
  {
    if (pathCircleLights == null) return;
    foreach (GameObject light in pathCircleLights)
      if (light) light.SetActive(false);
  }
  #endregion

  #region Transition
  private IEnumerator TransitionToBonus()
  {
    if (scrollContainer) _scrollHomeY = scrollContainer.anchoredPosition.y;
    if (bonusDecorations) bonusDecorations.alpha = 0f;

    if (baseGameUI)
    {
      yield return baseGameUI.DOFade(0f, fadeDuration).WaitForCompletion();
      baseGameUI.interactable = false;
      baseGameUI.blocksRaycasts = false;
    }

    if (scrollContainer)
      yield return scrollContainer.DOAnchorPosY(_scrollHomeY - scrollDistance, scrollDuration)
        .SetEase(scrollEase).WaitForCompletion();

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

    if (scrollContainer)
      yield return scrollContainer.DOAnchorPosY(_scrollHomeY, scrollDuration)
        .SetEase(scrollEase).WaitForCompletion();

    if (baseGameUI)
    {
      baseGameUI.interactable = true;
      baseGameUI.blocksRaycasts = true;
      yield return baseGameUI.DOFade(1f, fadeDuration).WaitForCompletion();
    }
  }
  #endregion

  #region Displays
  private void SetShootInteractable(bool on)
  {
    if (shootButton) shootButton.interactable = on;
  }

  private void UpdateShotsText(int shots)
  {
    if (shotsText) shotsText.text = shots.ToString();
  }

  private void UpdateBonusWinText(double amount)
  {
    if (bonusWinText) bonusWinText.text = amount.ToString("F2");
  }
  #endregion
}

// One destination pocket (ship or marble) the ball can land in. Keyed positionally to the backend
// prize lists; ringStopIndex is where along pathCircleLights the ball comes to rest at this pocket.
[System.Serializable]
public class PinballPocket
{
  public GameObject highlight;   // lit overlay shown when the ball lands here
  public int ringStopIndex = -1; // index into pathCircleLights for the ball's resting spot
  public TMP_Text pointsText;    // optional static points label (display = 100x money)
}
