using UnityEngine;
using CurvedUI.Core;

public class CurvedUITouchModule : MonoBehaviour
{
    private bool _isPointerPressed;
    private Ray _pointerRay;
    private Camera _camera;

    private void Start()
    {
        _camera = Camera.main;
    }

    private void Update()
    {
        UpdatePointerInput();
        ApplyToCurvedUI();
    }

    private void UpdatePointerInput()
    {
        if (_camera == null) return;
        
        _isPointerPressed = false;

        foreach (var touch in Input.touches)
        {
            _isPointerPressed = touch.phase is TouchPhase.Began or TouchPhase.Stationary or TouchPhase.Moved;
            _pointerRay = _camera.ScreenPointToRay(touch.position);
            return;
        }

        _isPointerPressed = Input.GetMouseButton(0);
        _pointerRay = _camera.ScreenPointToRay(Input.mousePosition);
    }

    private void ApplyToCurvedUI()
    {
        CurvedUIInputModule.CustomRay = _pointerRay;
        CurvedUIInputModule.CustomRayButtonState = _isPointerPressed;
    }
}