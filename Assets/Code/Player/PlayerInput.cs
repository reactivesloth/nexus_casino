using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance { get; private set; }

    [Header("Look Settings")]
    public float lookSensitivity = 1f;
    public bool invertY = false;

    // wrapper generated from .inputactions
    private InputAsset _inputAsset;
    private InputAsset.PlayerActions _player;

    // cached underlying actions (for convenience / unsub)
    private InputAction _interactAction;
    private InputAction _cameraSwitchAction;

    // events
    public event Action OnInteract;
    public event Action OnCameraSwitch;

    // callbacks for unsubscribing
    private Action<InputAction.CallbackContext> _interactCallback;
    private Action<InputAction.CallbackContext> _cameraSwitchCallback;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Создаём input asset (внутри он десериализует JSON и создаёт карту "Player")
        _inputAsset = new InputAsset();
        _player = _inputAsset.Player;

        // Подписки на действия
        _interactAction = _player.Interact;
        _cameraSwitchAction = _player.CameraSwitch;

        _interactCallback = ctx => OnInteract?.Invoke();
        _cameraSwitchCallback = ctx => OnCameraSwitch?.Invoke();

        if (_interactAction != null)
            _interactAction.performed += _interactCallback;
        if (_cameraSwitchAction != null)
            _cameraSwitchAction.performed += _cameraSwitchCallback;

        // Включаем
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
            _interactAction.performed -= _interactCallback;
        if (_cameraSwitchAction != null)
            _cameraSwitchAction.performed -= _cameraSwitchCallback;

        // Dispose уничтожает внутренний asset object
        _inputAsset?.Dispose();
    }

    // --- Публичные геттеры ---

    public Vector2 Move => _player.Move.ReadValue<Vector2>();

    public Vector2 LookRaw => _player.Look.ReadValue<Vector2>();

    public Vector2 Look
    {
        get
        {
            Vector2 v = LookRaw;
            if (invertY) v.y = -v.y;
            return v * lookSensitivity;
        }
    }

    public bool JumpDown => _player.Jump != null && _player.Jump.triggered;
    public bool JumpHeld => _player.Jump != null && _player.Jump.ReadValue<float>() > 0.5f;
    public bool SprintHeld => _player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f;
    public bool CameraSwitchDown => _player.CameraSwitch != null && _player.CameraSwitch.triggered;
    public bool InteractDown => _player.Interact != null && _player.Interact.triggered;
    public bool ForceCursorHeld => _player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f;

    /// <summary>Включить/выключить ввод целиком (всей карты)</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled)
            _player.Enable();
        else
            _player.Disable();
    }

    /// <summary>Принудительно применить конкретную control scheme через binding mask (например, "Gamepad" или "KeyboardMouse")</summary>
    public void SetControlScheme(InputControlScheme scheme)
    {
        if (_inputAsset == null) return;
        _inputAsset.asset.bindingMask = InputBinding.MaskByGroup(scheme.bindingGroup);
    }

    /// <summary>Снять фильтр control scheme (использовать все)</summary>
    public void ClearControlSchemeFilter()
    {
        if (_inputAsset == null) return;
        _inputAsset.asset.bindingMask = null;
    }

    /// <summary>Доступ к самому PlayerActions на случай расширения</summary>
    public InputAsset.PlayerActions PlayerActions => _player;
}
