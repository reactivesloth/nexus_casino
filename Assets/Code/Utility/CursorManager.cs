using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Header("Cursor Images (hardware)")]
    [SerializeField] private List<Texture2D> cursorTextures = new();
    [SerializeField] private Vector2 hardwareHotspot = Vector2.zero;
    [SerializeField] private CursorMode hardwareMode = CursorMode.Auto;

    [Header("Custom UI Cursor (optional)")]
    [SerializeField] private bool useCustomCursor = false;
    [SerializeField] private RectTransform customCursorRect;
    [SerializeField] private Image customCursorImage;
    [SerializeField] private Vector2 customCursorOffset = Vector2.zero;

    [Header("Tap / Hold tuning")]
    [Tooltip("Максимальная длительность для считания как tap (в секундах)")]
    [SerializeField] private float tapMaxTime = 0.2f;
    [Tooltip("Минимальная длительность удержания для long press (в секундах)")]
    [SerializeField] private float longPressThreshold = 0.5f;
    [SerializeField] private bool enableTapToCycle = true;
    [SerializeField] private bool enableLongPressToggleLock = true;

    [SerializeField] private bool forceShowCursor;
    
    public event Action<bool> OnVisibilityChanged;
    public event Action<int> OnCursorImageChanged;
    public event Action<CursorLockMode> OnLockModeChanged;

    private int currentCursorIndex = 0;
    private bool isLocked = false;

    // input state
    private bool wasActionHeldLastFrame = false;
    private float actionHeldDuration = 0f;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        ApplyCurrentCursor();
        UpdateCursorVisibility(false); // по умолчанию скрыт/в норме
    }
    
    private void Update()
    {
        if (forceShowCursor)
        {
            ShowCursor();
            SetLockMode(CursorLockMode.None);
            return;
        }
        
        HandlePrimaryAction();

        if (useCustomCursor && customCursorRect != null)
        {
            Vector2 mousePos = Mouse.current != null
                ? Mouse.current.position.ReadValue()
                : (Vector2)Input.mousePosition;

            customCursorRect.anchoredPosition = mousePos + customCursorOffset;
        }
    }

    private void HandlePrimaryAction()
    {
        if (PlayerInput.Instance == null)
            return;

        bool isHeld = PlayerInput.Instance.ForceCursorHeld;

        if (isHeld)
        {
            actionHeldDuration += Time.unscaledDeltaTime;

            if (!wasActionHeldLastFrame)
            {
                // старт удержания
                ShowCursor();
            }
        }
        else if (wasActionHeldLastFrame)
        {
            // отпускание — решаем, был ли tap или long press
            if (enableTapToCycle && actionHeldDuration <= tapMaxTime)
            {
                CycleCursor();
            }
            else if (enableLongPressToggleLock && actionHeldDuration >= longPressThreshold)
            {
                ToggleLock();
            }

            HideCursor();
            actionHeldDuration = 0f;
        }

        wasActionHeldLastFrame = isHeld;
    }

    #region Visibility

    public void ShowCursor()
    {
#if UNITY_ANDROID || UNITY_IOS
        return;
#else
        if (useCustomCursor)
        {
            if (customCursorImage != null)
                customCursorImage.gameObject.SetActive(true);
            Cursor.visible = false;
        }
        else
        {
            Cursor.visible = true;
        }
        OnVisibilityChanged?.Invoke(true);
#endif
    }

    public void HideCursor()
    {
#if UNITY_ANDROID || UNITY_IOS
        return;
#else
        if (useCustomCursor)
        {
            if (customCursorImage != null)
                customCursorImage.gameObject.SetActive(false);
        }
        else
        {
            Cursor.visible = false;
        }

        OnVisibilityChanged?.Invoke(false);
#endif
    }

    private void UpdateCursorVisibility(bool visible)
    {
#if UNITY_ANDROID || UNITY_IOS
        return;
#else
        if (visible) ShowCursor();
        else HideCursor();
#endif 
    }

    public bool IsVisible()
    {
#if UNITY_ANDROID || UNITY_IOS
        return false;
#else
        return useCustomCursor
            ? (customCursorImage != null && customCursorImage.gameObject.activeSelf)
            : Cursor.visible;
#endif
    }

    #endregion

    #region Cursor Image

    public void SetCursorTexture(Texture2D texture, Vector2? hotspot = null, CursorMode? mode = null)
    {
        hardwareHotspot = hotspot ?? hardwareHotspot;
        hardwareMode = mode ?? hardwareMode;

        Cursor.SetCursor(texture, hardwareHotspot, hardwareMode);
        if (useCustomCursor && customCursorImage != null)
        {
            if (texture != null)
            {
                customCursorImage.sprite = Sprite.Create(texture,
                    new Rect(0, 0, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f));
            }
        }

        OnCursorImageChanged?.Invoke(currentCursorIndex);
    }

    public void ResetCursorToDefault()
    {
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        if (useCustomCursor && customCursorImage != null)
        {
            customCursorImage.sprite = null;
        }
        OnCursorImageChanged?.Invoke(currentCursorIndex);
    }

    public void CycleCursor()
    {
        if (cursorTextures == null || cursorTextures.Count == 0)
            return;

        currentCursorIndex = (currentCursorIndex + 1) % cursorTextures.Count;
        SetCursorTexture(cursorTextures[currentCursorIndex], hardwareHotspot, hardwareMode);
    }

    public void SetCursorByIndex(int index)
    {
        if (cursorTextures == null || index < 0 || index >= cursorTextures.Count) return;
        currentCursorIndex = index;
        SetCursorTexture(cursorTextures[currentCursorIndex], hardwareHotspot, hardwareMode);
    }

    public int GetCurrentCursorIndex() => currentCursorIndex;

    private void ApplyCurrentCursor()
    {
        if (cursorTextures != null && cursorTextures.Count > 0)
            SetCursorTexture(cursorTextures[currentCursorIndex], hardwareHotspot, hardwareMode);
        else
            ResetCursorToDefault();
    }

    #endregion

    #region Locking

    public void SetLockMode(CursorLockMode mode)
    {
        Cursor.lockState = mode;
        isLocked = mode != CursorLockMode.None;
        OnLockModeChanged?.Invoke(mode);
    }

    public void ToggleLock()
    {
        if (isLocked)
            SetLockMode(CursorLockMode.None);
        else
            SetLockMode(CursorLockMode.Locked);
    }

    public CursorLockMode GetLockMode() => Cursor.lockState;

    #endregion

    #region Utilities

    public void SetCustomCursorSprite(Sprite sprite)
    {
        if (!useCustomCursor || customCursorImage == null) return;
        customCursorImage.sprite = sprite;
    }

    #endregion
}