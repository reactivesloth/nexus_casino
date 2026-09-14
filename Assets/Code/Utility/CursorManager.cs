using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Code.Utility
{
    public sealed class CursorManager : MonoBehaviour
    {
        public static CursorManager Instance { get; private set; }

        [Header("Cursor Images (hardware)")]
        [SerializeField] private List<Texture2D> cursorTextures = new List<Texture2D>();
        [SerializeField] private Vector2 hardwareHotspot = Vector2.zero;
        [SerializeField] private CursorMode hardwareMode = CursorMode.Auto;

        [Header("Custom UI Cursor (optional)")]
        [SerializeField] private bool useCustomCursor = false;
        [SerializeField] private RectTransform customCursorRect;
        [SerializeField] private Image customCursorImage;
        [SerializeField] private Vector2 customCursorOffset = Vector2.zero;

        [Header("Tap / Hold tuning")]
        [SerializeField] private float tapMaxTime = 0.2f;
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

        // cached sprite to avoid leaks
        private Sprite _customCursorSprite;

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            ApplyCurrentCursor();
            UpdateCursorVisibility(false);
        }

        private void OnDisable()
        {
            // return system cursor back
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void OnDestroy()
        {
            if (_customCursorSprite != null)
            {
                Destroy(_customCursorSprite);
                _customCursorSprite = null;
            }
        }

        private void Update()
        {
            if (PlayerInput.Instance != null)
                if (PlayerInput.Instance.IsUsingMobileFallback)
                    return;
            
            if (forceShowCursor)
            {
                ShowCursor();
            }
            else
            {
                HandlePrimaryAction();
            }

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
            bool isHeld = false;
            var playerInputSingleton = PlayerInput.Instance; // если есть
            if (playerInputSingleton != null)
                isHeld = playerInputSingleton.ForceCursorHeld;

            if (isHeld)
            {
                actionHeldDuration += Time.unscaledDeltaTime;
                if (!wasActionHeldLastFrame)
                    ShowCursor();
            }
            else if (wasActionHeldLastFrame)
            {
                if (enableTapToCycle && actionHeldDuration <= tapMaxTime) CycleCursor();
                else if (enableLongPressToggleLock && actionHeldDuration >= longPressThreshold) ToggleLock();

                HideCursor();
                actionHeldDuration = 0f;
            }

            wasActionHeldLastFrame = isHeld;
        }

        #region Visibility

        public void ShowCursor()
        {
            if (PlayerInput.Instance != null)
                if (PlayerInput.Instance.IsUsingMobileFallback)
                    return;
            if (useCustomCursor)
            {
                if (customCursorImage != null)
                    customCursorImage.gameObject.SetActive(true);
                Cursor.visible = false;
            }
            else Cursor.visible = true;

            SetLockMode(CursorLockMode.None);
            OnVisibilityChanged?.Invoke(true);
        }

        public void HideCursor()
        {
            if (PlayerInput.Instance != null)
                if (PlayerInput.Instance.IsUsingMobileFallback)
                    return;

            if (useCustomCursor)
            {
                if (customCursorImage != null)
                    customCursorImage.gameObject.SetActive(false);
            }
            else Cursor.visible = false;

            SetLockMode(CursorLockMode.Locked);
            OnVisibilityChanged?.Invoke(false);
        }

        private void UpdateCursorVisibility(bool visible)
        {
            if (PlayerInput.Instance != null)
                if (PlayerInput.Instance.IsUsingMobileFallback)
                    return;
            if (visible) ShowCursor();
            else HideCursor();
        }

        public bool IsVisible()
        {
            if (PlayerInput.Instance != null)
                if (PlayerInput.Instance.IsUsingMobileFallback)
                    return false;
            
            return useCustomCursor
                ? customCursorImage != null && customCursorImage.gameObject.activeSelf
                : Cursor.visible;
        }

        public void SetForceShowCursor(bool value)
        {
            forceShowCursor = value;
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
                if (_customCursorSprite != null) { Destroy(_customCursorSprite); _customCursorSprite = null; }
                if (texture != null)
                {
                    _customCursorSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    customCursorImage.sprite = _customCursorSprite;
                }
                else customCursorImage.sprite = null;
            }

            OnCursorImageChanged?.Invoke(currentCursorIndex);
        }

        public void ResetCursorToDefault()
        {
            SetCursorTexture(null, Vector2.zero, CursorMode.Auto);
        }

        public void CycleCursor()
        {
            if (cursorTextures == null || cursorTextures.Count == 0) return;
            currentCursorIndex = (currentCursorIndex + 1) % cursorTextures.Count;
            SetCursorTexture(cursorTextures[currentCursorIndex], hardwareHotspot, hardwareMode);
        }

        public void SetCursorByIndex(int index)
        {
            if (cursorTextures == null || index < 0 || index >= cursorTextures.Count) return;
            currentCursorIndex = index;
            SetCursorTexture(cursorTextures[currentCursorIndex], hardwareHotspot, hardwareMode);
        }

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

        public void ToggleLock() => SetLockMode(isLocked ? CursorLockMode.None : CursorLockMode.Locked);
        public CursorLockMode GetLockMode() => Cursor.lockState;

        #endregion
    }
}
