using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Mobile/Mobile Action Button")]
public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsHeld { get; private set; }
    public bool PressedThisFrame { get; private set; }

    public void OnPointerDown(PointerEventData eventData)
    {
        IsHeld = true;
        PressedThisFrame = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        IsHeld = false;
    }

    private void LateUpdate()
    {
        // Reset только PressedThisFrame — держание сохраняется
        PressedThisFrame = false;
    }
}