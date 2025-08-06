using System;
using Code.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using Code.Utility;
using UnityEngine.UI; // если нужно, как было у тебя
// Предполагается, что VirtualJoystick, TouchLook и MobileActionButton уже есть в проекте (из предыдущего ответа).

[DefaultExecutionOrder(-100)]
public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance { get; private set; }

    [Header("Look Settings")]
    public float lookSensitivity => SettingsManager.Instance.CameraSensitivity / 100f;
    public bool invertY => SettingsManager.Instance != null
        ? SettingsManager.Instance.InvertCamera
        : PlayerPrefs.GetInt("InvertCamera", 0) == 1;

    // wrapper generated from .inputactions
    private InputAsset _inputAsset;
    private InputAsset.PlayerActions _player;

    // мобильный ввод (встроенный fallback)
    [Header("Mobile Fallback (optional UI)")]
    public GameObject mobileCanvas;
    public UltimateJoystick MoveJoystick;
    public UltimateTouchpad LookArea;
    public UltimateButton JumpButton;
    public UltimateButton SprintButton;
    public UltimateButton InteractButton;
    public UltimateButton CameraSwitchButton;
    public UltimateButton PauseButton;
    public UltimateButton VoiceButton;
    public UltimateButton OpenChatButton;
    public UltimateButton SwitchChatButton;
    

    [SerializeField] private bool ForceMobile;
    private bool savedHideMobileFallback;
    public bool IsUsingMobileFallback { get; set; }
    public bool HideMobileFallback { get; set; }
    
    private void Awake()
    {
        Instance = this;
        
        IsUsingMobileFallback = ForceMobile || Application.isMobilePlatform;

        _inputAsset = new InputAsset();
        _player = _inputAsset.Player;

        _player.Enable();
    }

    private void OnEnable()
    {
        _player.Enable();
    }

    private void OnDisable()
    {
        _player.Disable();
        
        foreach (Transform child in mobileCanvas.transform) child.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        _inputAsset?.Dispose();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        IsUsingMobileFallback = ForceMobile || Application.isMobilePlatform;

        if (IsUsingMobileFallback != mobileCanvas.activeSelf)
        {
            mobileCanvas.SetActive(IsUsingMobileFallback);
        }

        if (IsUsingMobileFallback)
        {
            if (HideMobileFallback != savedHideMobileFallback)
            {
                MoveJoystick.gameObject.SetActive(!HideMobileFallback);
                LookArea.gameObject.SetActive(!HideMobileFallback);
                JumpButton.gameObject.SetActive(!HideMobileFallback);
                SprintButton.gameObject.SetActive(!HideMobileFallback);
                savedHideMobileFallback = HideMobileFallback;
            }
        }
    }

    // --- Геттеры ввода (автоматически выбирают mobile если доступно) ---
    public Vector2 Move
    {
        get
        {
            if (IsUsingMobileFallback && MoveJoystick != null)
            {
                float h = MoveJoystick.HorizontalAxis;
                float v = MoveJoystick.VerticalAxis;
                return new Vector2(h, v);
            }

            return _player.Move.ReadValue<Vector2>();
        }
    }

    public Vector2 LookRaw
    {
        get
        {
            if (IsUsingMobileFallback && LookArea != null)
            {
                float h = LookArea.GetHorizontalAxis();
                float v = -LookArea.GetVerticalAxis();
                return new Vector2(h, v);
            }

            return _player.Look.ReadValue<Vector2>();
        }
    }

    public Vector2 Look
    {
        get
        {
            Vector2 v = LookRaw;
            if (invertY) v.y = -v.y;
            return v * lookSensitivity;
        }
    }

    public bool JumpDown
    {
        get
        {
            if (IsUsingMobileFallback && JumpButton != null)
                return JumpButton.GetButtonDown();
            return _player.Jump != null && _player.Jump.triggered;
        }
    }

    public bool JumpHeld
    {
        get
        {
            if (IsUsingMobileFallback && JumpButton != null)
                return JumpButton.GetButton();
            return _player.Jump != null && _player.Jump.ReadValue<float>() > 0.5f;
        }
    }
    public bool VoiceHeld
    {
        get
        {
            if (IsUsingMobileFallback && VoiceButton != null)
                return VoiceButton.GetButton();
            return _player.Voice != null && _player.Voice.ReadValue<float>() > 0.5f;
        }
    }

    public bool SprintHeld
    {
        get
        {
            if (IsUsingMobileFallback && SprintButton != null)
                return SprintButton.GetButton();
            return _player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f;
        }
    }

    public bool CameraSwitchDown
    {
        get
        {
            if (IsUsingMobileFallback && CameraSwitchButton != null)
                return CameraSwitchButton.GetButtonDown();
            return _player.CameraSwitch != null && _player.CameraSwitch.triggered;
        }
    }

    public bool InteractDown
    {
        get
        {
            if (IsUsingMobileFallback && InteractButton != null)
                return InteractButton.GetButtonDown();
            return _player.Interact != null && _player.Interact.triggered;
        }
    }
    
    public bool IsPausedDown
    {
        get
        {
            if (IsUsingMobileFallback && PauseButton != null)
                return PauseButton.GetButtonDown();
            return _player.Pause != null && _player.Pause.triggered;
        }
    }
    
    public bool IsOpenChatDown
    {
        get
        {
            if (IsUsingMobileFallback && PauseButton != null)
                return OpenChatButton.GetButtonDown();
            return _player.ChatOpen != null && _player.ChatOpen.triggered;
        }
    }
    
    public bool IsSwitchChatDown
    {
        get
        {
            if (IsUsingMobileFallback && PauseButton != null)
                return SwitchChatButton.GetButtonDown();
            return _player.SwitсhChat != null && _player.SwitсhChat.triggered;
        }
    }
    
    public bool IsRMB => _player.RMB != null && _player.RMB.triggered;
    public bool IsRMBDown => _player.RMB != null && _player.RMB.ReadValue<float>() > 0.5f;
    public bool ForceCursorHeld => _player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f;
    

    /// <summary>Включить/выключить ввод целиком (всей карты)</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled)
            _player.Enable();
        else
            _player.Disable();
    }

    /// <summary>Принудительно применить конкретную control scheme (например, Gamepad/KeyboardMouse)</summary>
    public void SetControlScheme(InputControlScheme scheme)
    {
        if (_inputAsset == null) return;
        _inputAsset.asset.bindingMask = InputBinding.MaskByGroup(scheme.bindingGroup);
    }

    public void ClearControlSchemeFilter()
    {
        if (_inputAsset == null) return;
        _inputAsset.asset.bindingMask = null;
    }

    public InputAsset.PlayerActions PlayerActions => _player;
}
