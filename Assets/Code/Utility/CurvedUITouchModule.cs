using UnityEngine;
using CurvedUI.Core;
using TouchPhase = UnityEngine.TouchPhase;

public class CurvedUITouchModule : MonoBehaviour
{
    [Header("Remote / Pointer Transform (optional)")]
    public Transform yourRemoteTransform;

    private bool isPointerPressed;
    private Ray pointerRay;

    void Update()
    {
        UpdatePointerInput();
        ApplyToCurvedUI();
    }

    void UpdatePointerInput()
    {
        isPointerPressed = false;

        // --- Touch input ---
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            isPointerPressed = touch.phase is TouchPhase.Began or TouchPhase.Stationary or TouchPhase.Moved;
            pointerRay = Camera.main.ScreenPointToRay(touch.position);
            return;
        }

        // --- Mouse input ---
        if (Input.GetMouseButton(0))
        {
            isPointerPressed = true;
        }

        pointerRay = Camera.main.ScreenPointToRay(Input.mousePosition);

        // --- Remote / VR controller fallback ---
        if (yourRemoteTransform != null && !isPointerPressed)
        {
            pointerRay = new Ray(yourRemoteTransform.position, yourRemoteTransform.forward);
            isPointerPressed = Input.GetMouseButton(0); // или ваша кнопка
        }
    }

    void ApplyToCurvedUI()
    {
        CurvedUIInputModule.CustomRay = pointerRay;
        CurvedUIInputModule.CustomRayButtonState = isPointerPressed;
    }
}