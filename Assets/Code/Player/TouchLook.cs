using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Mobile/Touch Look")]
public class TouchLook : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("Множитель чувствительности. Умножается на глобальную чувствительность из SettingsManager.")]
    public float sensitivity = 1f;

    public Vector2 Delta { get; private set; } = Vector2.zero;

    private bool _isDragging;

    public void OnPointerDown(PointerEventData eventData)
    {
        _isDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        // Unity даёт delta в пикселях
        Delta += eventData.delta * sensitivity * Time.deltaTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isDragging = false;
    }

    private void LateUpdate()
    {
        // После того как внешний код прочитал дельту, сбросим — внешнее потребление должно происходить в Update.
        Delta = Vector2.zero;
    }
}