using System;
using Code.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using Code.Utility;

[DefaultExecutionOrder(-100)]
public class PlayerInput : MonoBehaviour
{
    public static PlayerInput Instance { get; private set; }

    [Header("Look Settings")]
    public float lookSensitivity
    {
        get
        {
            var sm = SettingsManager.Instance;
            float sens = (sm != null) ? sm.CameraSensitivity : PlayerPrefs.GetFloat("CameraSensitivity", 40f);
            return Mathf.Clamp(sens, 1f, 200f) / 100f;
        }
    }

    public bool invertY
    {
        get
        {
            var sm = SettingsManager.Instance;
            return sm != null ? sm.InvertCamera : PlayerPrefs.GetInt("InvertCamera", 0) == 1;
        }
    }

    private InputAsset _inputAsset;
    private InputAsset.PlayerActions _player;

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

    private void OnEnable() => _player.Enable();

    private void OnDisable()
    {
        _player.Disable();

        if (mobileCanvas != null)
        {
            for (int i = 0; i < mobileCanvas.transform.childCount; i++)
                mobileCanvas.transform.GetChild(i).gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        _inputAsset?.Dispose();
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        IsUsingMobileFallback = ForceMobile || Application.isMobilePlatform;

        if (mobileCanvas != null && IsUsingMobileFallback != mobileCanvas.activeSelf)
            mobileCanvas.SetActive(IsUsingMobileFallback);

        if (IsUsingMobileFallback && mobileCanvas != null)
        {
            if (HideMobileFallback != savedHideMobileFallback)
            {
                if (MoveJoystick != null)      MoveJoystick.gameObject.SetActive(!HideMobileFallback);
                if (LookArea != null)          LookArea.gameObject.SetActive(!HideMobileFallback);
                if (JumpButton != null)        JumpButton.gameObject.SetActive(!HideMobileFallback);
                if (SprintButton != null)      SprintButton.gameObject.SetActive(!HideMobileFallback);
                if (InteractButton != null)    InteractButton.gameObject.SetActive(!HideMobileFallback);
                if (CameraSwitchButton != null)CameraSwitchButton.gameObject.SetActive(!HideMobileFallback);
                if (PauseButton != null)       PauseButton.gameObject.SetActive(!HideMobileFallback);
                if (VoiceButton != null)       VoiceButton.gameObject.SetActive(!HideMobileFallback);
                if (OpenChatButton != null)    OpenChatButton.gameObject.SetActive(!HideMobileFallback);
                if (SwitchChatButton != null)  SwitchChatButton.gameObject.SetActive(!HideMobileFallback);

                savedHideMobileFallback = HideMobileFallback;
            }
        }
    }

    // --- Геттеры ввода ---
    public Vector2 Move
    {
        get
        {
            if (IsUsingMobileFallback && MoveJoystick != null)
                return new Vector2(MoveJoystick.HorizontalAxis, MoveJoystick.VerticalAxis);
            return _player.Move.ReadValue<Vector2>();
        }
    }

    public Vector2 LookRaw
    {
        get
        {
            if (IsUsingMobileFallback && LookArea != null)
                return new Vector2(LookArea.GetHorizontalAxis(), -LookArea.GetVerticalAxis());
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

    public bool JumpDown  => IsUsingMobileFallback && JumpButton != null ? JumpButton.GetButtonDown() : (_player.Jump != null && _player.Jump.triggered);
    public bool JumpHeld  => IsUsingMobileFallback && JumpButton != null ? JumpButton.GetButton()     : (_player.Jump != null && _player.Jump.ReadValue<float>() > 0.5f);
    public bool VoiceHeld => IsUsingMobileFallback && VoiceButton != null ? VoiceButton.GetButton()   : (_player.Voice != null && _player.Voice.ReadValue<float>() > 0.5f);
    public bool SprintHeld=> IsUsingMobileFallback && SprintButton != null ? SprintButton.GetButton() : (_player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f);

    public bool CameraSwitchDown =>
        IsUsingMobileFallback && CameraSwitchButton != null ? CameraSwitchButton.GetButtonDown()
        : (_player.CameraSwitch != null && _player.CameraSwitch.triggered);

    public bool InteractDown =>
        IsUsingMobileFallback && InteractButton != null ? InteractButton.GetButtonDown()
        : (_player.Interact != null && _player.Interact.triggered);

    public bool IsPausedDown =>
        IsUsingMobileFallback && PauseButton != null ? PauseButton.GetButtonDown()
        : (_player.Pause != null && _player.Pause.triggered);

    public bool IsOpenChatDown =>
        IsUsingMobileFallback && OpenChatButton != null ? OpenChatButton.GetButtonDown()
        : (_player.ChatOpen != null && _player.ChatOpen.triggered);

    public bool IsSwitchChatDown =>
        IsUsingMobileFallback && SwitchChatButton != null ? SwitchChatButton.GetButtonDown()
        : (_player.SwitсhChat != null && _player.SwitсhChat.triggered);

    public bool IsRMB      => _player.RMB != null && _player.RMB.triggered;
    public bool IsRMBDown  => _player.RMB != null && _player.RMB.ReadValue<float>() > 0.5f;
    public bool ForceCursorHeld => _player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f;

    public void SetEnabled(bool enabled) { if (enabled) _player.Enable(); else _player.Disable(); }

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
