using UnityEngine;
using UnityEngine.EventSystems;

[AddComponentMenu("Mobile/Virtual Joystick")]
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("UI References")]
    public RectTransform background;
    public RectTransform handle;

    [Tooltip("Максимальное смещение ручки относительно центра (в процентах от половины размера)")]
    [Range(0.1f, 1f)]
    public float handleRange = 1f;

    public Vector2 Output { get; private set; } = Vector2.zero;

    private Canvas _parentCanvas;
    private Vector2 _backgroundSize;

    private void Awake()
    {
        _parentCanvas = GetComponentInParent<Canvas>();
        if (background == null || handle == null)
        {
            Debug.LogError("VirtualJoystick: нужно присвоить background и handle."); 
        }
        if (background != null)
            _backgroundSize = background.sizeDelta;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (background == null || handle == null) return;

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera, out localPoint);

        Vector2 normalized = new Vector2(
            localPoint.x / (background.sizeDelta.x * 0.5f),
            localPoint.y / (background.sizeDelta.y * 0.5f)
        );

        normalized = Vector2.ClampMagnitude(normalized, 1f);
        Output = normalized;

        // Перемещаем ручку
        handle.anchoredPosition = new Vector2(
            normalized.x * background.sizeDelta.x * 0.5f * handleRange,
            normalized.y * background.sizeDelta.y * 0.5f * handleRange
        );
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Output = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }
}
