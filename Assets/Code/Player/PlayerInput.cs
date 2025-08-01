using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Code.Utility; // если нужно, как было у тебя
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

    // cached underlying actions (для событий)
    private InputAction _interactAction;
    private InputAction _cameraSwitchAction;

    // события
    public event Action OnInteract;
    public event Action OnCameraSwitch;

    // мобильный ввод (встроенный fallback)
    [Header("Mobile Fallback (optional UI)")]
    public GameObject mobileCanvas;
    public VirtualJoystick MoveJoystick;
    public TouchLook LookArea;
    public MobileActionButton JumpButton;
    public MobileActionButton SprintButton;
    public MobileActionButton InteractButton;
    public MobileActionButton CameraSwitchButton;
    public MobileActionButton ForceCursorButton;
    public MobileActionButton PauseButton;
    public MobileActionButton VoiceButton;

    // внутреннее отслеживание edge для моб. кнопок
    private bool _prevMobileInteract;
    private bool _prevMobileCameraSwitch;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _inputAsset = new InputAsset();
        _player = _inputAsset.Player;

        _interactAction = _player.Interact;
        _cameraSwitchAction = _player.CameraSwitch;

        Action<InputAction.CallbackContext> interactCallback = ctx => OnInteract?.Invoke();
        Action<InputAction.CallbackContext> cameraSwitchCallback = ctx => OnCameraSwitch?.Invoke();

        if (_interactAction != null)
            _interactAction.performed += interactCallback;
        if (_cameraSwitchAction != null)
            _cameraSwitchAction.performed += cameraSwitchCallback;

        _player.Enable();
    }

    private void OnEnable()
    {
        _player.Enable();
    }

    private void OnDisable()
    {
        _player.Disable();
    }

    private void OnDestroy()
    {
        if (_interactAction != null)
            _interactAction.performed -= ctx => OnInteract?.Invoke(); // безопасно, т.к. делегаты без сохранения не снимаются—можно хранить если нужно точно отписывать
        if (_cameraSwitchAction != null)
            _cameraSwitchAction.performed -= ctx => OnCameraSwitch?.Invoke();

        _inputAsset?.Dispose();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // мобильные кнопки: вручную вызываем события по edge
        if (IsUsingMobileFallback)
        {
            bool interactDown = InteractDown; // от mobile
            if (interactDown && !_prevMobileInteract)
                OnInteract?.Invoke();
            _prevMobileInteract = interactDown;

            bool camSwitchDown = CameraSwitchDown;
            if (camSwitchDown && !_prevMobileCameraSwitch)
                OnCameraSwitch?.Invoke();
            _prevMobileCameraSwitch = camSwitchDown;
        }

        if (IsUsingMobileFallback != mobileCanvas.activeSelf)
        {
            mobileCanvas.SetActive(IsUsingMobileFallback);
        }
    }

    [SerializeField] private bool ForceMobile;
    private bool IsUsingMobileFallback => ForceMobile || (Application.isMobilePlatform && (MoveJoystick != null || LookArea != null || JumpButton != null || InteractButton != null));

    // --- Геттеры ввода (автоматически выбирают mobile если доступно) ---
    public Vector2 Move
    {
        get
        {
            if (IsUsingMobileFallback && MoveJoystick != null)
                return MoveJoystick.Output;
            return _player.Move.ReadValue<Vector2>();
        }
    }

    public Vector2 LookRaw
    {
        get
        {
            if (IsUsingMobileFallback && LookArea != null)
                return LookArea.Delta;
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
                return JumpButton.PressedThisFrame;
            return _player.Jump != null && _player.Jump.triggered;
        }
    }

    public bool JumpHeld
    {
        get
        {
            if (IsUsingMobileFallback && JumpButton != null)
                return JumpButton.IsHeld;
            return _player.Jump != null && _player.Jump.ReadValue<float>() > 0.5f;
        }
    }
    public bool VoiceHeld
    {
        get
        {
            if (IsUsingMobileFallback && VoiceButton != null)
                return VoiceButton.IsHeld;
            return _player.Voice != null && _player.Voice.ReadValue<float>() > 0.5f;
        }
    }

    public bool SprintHeld
    {
        get
        {
            if (IsUsingMobileFallback && SprintButton != null)
                return SprintButton.IsHeld;
            return _player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f;
        }
    }

    public bool CameraSwitchDown
    {
        get
        {
            if (IsUsingMobileFallback && CameraSwitchButton != null)
                return CameraSwitchButton.PressedThisFrame;
            return _player.CameraSwitch != null && _player.CameraSwitch.triggered;
        }
    }

    public bool InteractDown
    {
        get
        {
            if (IsUsingMobileFallback && InteractButton != null)
                return InteractButton.PressedThisFrame;
            return _player.Interact != null && _player.Interact.triggered;
        }
    }
    
    public bool IsPausedDown
    {
        get
        {
            if (IsUsingMobileFallback && PauseButton != null)
                return PauseButton.PressedThisFrame;
            return _player.Pause != null && _player.Pause.triggered;
        }
    }
    public bool IsRMB => _player.RMB != null && _player.RMB.triggered;

    public bool ForceCursorHeld
    {
        get
        {
            if (IsUsingMobileFallback && ForceCursorButton != null)
                return ForceCursorButton.IsHeld;
            return _player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f;
        }
    }

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
