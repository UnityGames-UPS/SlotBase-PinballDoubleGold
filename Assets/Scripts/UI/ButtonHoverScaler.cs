using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using System.Collections.Generic;

public class ButtonHoverScaler : MonoBehaviour
{
    [SerializeField] private List<Button> buttonsToScale;
    [SerializeField] private float scaleFactor = 1.1f;
    [SerializeField] private float duration = 0.2f;

    private void Start()
    {
        foreach (Button button in buttonsToScale)
        {
            if (button != null) AddHoverScaler(button);
        }
    }

    private void AddHoverScaler(Button button)
    {
        Vector3 originalScale = button.transform.localScale;

        EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
        if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();

        var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entryEnter.callback.AddListener((data) =>
        {
            button.transform.DOScale(originalScale * scaleFactor, duration).SetEase(Ease.OutBack);
        });
        trigger.triggers.Add(entryEnter);

        var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        entryExit.callback.AddListener((data) =>
        {
            button.transform.DOScale(originalScale, duration).SetEase(Ease.OutBack);
        });
        trigger.triggers.Add(entryExit);
    }
}
