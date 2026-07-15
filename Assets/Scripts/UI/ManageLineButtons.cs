using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

public class ManageLineButtons : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, IPointerDownHandler
{

  [SerializeField] private SlotBehaviour slotManager;
  [SerializeField] private TMP_Text num_text;

  public void OnPointerEnter(PointerEventData eventData)
  {
    if (!slotManager.socketConnected)
    {
      return;
    }
    slotManager.GenerateStaticLine(num_text);
  }
  public void OnPointerExit(PointerEventData eventData)
  {
    if (!slotManager.socketConnected)
    {
      return;
    }
    slotManager.DestroyStaticLine();
  }
  public void OnPointerDown(PointerEventData eventData)
  {
    if (!slotManager.socketConnected)
    {
      return;
    }
    if (Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform)
    {
      this.gameObject.GetComponent<Button>().Select();
      slotManager.GenerateStaticLine(num_text);
    }
  }
  public void OnPointerUp(PointerEventData eventData)
  {
    if (!slotManager.socketConnected)
    {
      return;
    }
    if (Application.platform == RuntimePlatform.WebGLPlayer && Application.isMobilePlatform)
    {
      slotManager.DestroyStaticLine();
      DOVirtual.DelayedCall(0.1f, () =>
      {
        this.gameObject.GetComponent<Button>().spriteState = default;
        EventSystem.current.SetSelectedGameObject(null);
      });
    }
  }
}
