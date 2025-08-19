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
            float sens = sm != null ? sm.CameraSensitivity : PlayerPrefs.GetFloat("CameraSensitivity", 40f);
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
    private bool prevBusy;
    public bool IsUsingMobileFallback { get; set; }
    public bool HideMobileFallback { get; set; }
    
    
    [Header(("Slots Specific UI"))] 
    public GameObject slotsUI;
    public UltimateButton slotsScreenshotButton;
    public UltimateButton slotsFullscreenButton;
    public UltimateButton slotsStreamButton;
    public bool ShowSlotsUI { get; set; }

    public bool IsBusy { get; set; }
    
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

    public void SetBusy(bool value, bool forceUpdate = false)
    {
        if (forceUpdate)
        {
            IsBusy = value;
            return;
        }
        
        if (value)
        {
            prevBusy = IsBusy;
            IsBusy = true;
        }
        else
        {
            IsBusy = prevBusy;
        }
    }
    
    private void Update()
    {
        switch (Application.isFocused)
        {
            case false when IsBusy:
                SetBusy(true);
                break;
            case false when IsBusy:
                SetBusy(false);
                break;
        }
        
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
        IsUsingMobileFallback = true;
#else
        IsUsingMobileFallback = ForceMobile;
#endif

        if (mobileCanvas != null && IsUsingMobileFallback != mobileCanvas.activeSelf)
            mobileCanvas.SetActive(IsUsingMobileFallback);

        if (IsUsingMobileFallback && mobileCanvas != null)
        {
            if (HideMobileFallback != savedHideMobileFallback)
            {
                if (MoveJoystick != null)      MoveJoystick.gameObject.SetActive(!HideMobileFallback);
                //if (LookArea != null)          LookArea.gameObject.SetActive(!HideMobileFallback);
                if (JumpButton != null)        JumpButton.gameObject.SetActive(!HideMobileFallback);
                if (SprintButton != null)      SprintButton.gameObject.SetActive(!HideMobileFallback);
                //if (InteractButton != null)    InteractButton.gameObject.SetActive(!HideMobileFallback);
                //if (CameraSwitchButton != null)CameraSwitchButton.gameObject.SetActive(!HideMobileFallback);
                //if (PauseButton != null)       PauseButton.gameObject.SetActive(!HideMobileFallback);
                //if (VoiceButton != null)       VoiceButton.gameObject.SetActive(!HideMobileFallback);
                //if (OpenChatButton != null)    OpenChatButton.gameObject.SetActive(!HideMobileFallback);
                //if (SwitchChatButton != null)  SwitchChatButton.gameObject.SetActive(!HideMobileFallback);

                savedHideMobileFallback = HideMobileFallback;
            }
        }
        
        if (slotsUI != null && ShowSlotsUI != slotsUI.activeSelf)
            slotsUI.SetActive(ShowSlotsUI);
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
            return IsBusy ? Vector2.down : _player.Look.ReadValue<Vector2>();
        }
    }

    public Vector2 Look
    {
        get
        {
            Vector2 v = LookRaw;
            if (invertY) v.y = -v.y;
            return IsBusy ? Vector2.down : v * lookSensitivity;
        }
    }

    public bool JumpDown  => IsUsingMobileFallback && JumpButton != null ? JumpButton.GetButtonDown() : !IsBusy && _player.Jump is { triggered: true };
    public bool JumpHeld  => IsUsingMobileFallback && JumpButton != null ? JumpButton.GetButton()     : !IsBusy && _player.Jump != null && _player.Jump.ReadValue<float>() > 0.5f;
    public bool VoiceHeld => IsUsingMobileFallback && VoiceButton != null ? VoiceButton.GetButton()   : _player.Voice != null && _player.Voice.ReadValue<float>() > 0.5f;
    public bool SprintHeld=> IsUsingMobileFallback && SprintButton != null ? SprintButton.GetButton() : !IsBusy && _player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f;
    public bool CameraSwitchDown => IsUsingMobileFallback && CameraSwitchButton != null ? CameraSwitchButton.GetButtonDown() : !IsBusy && _player.CameraSwitch is { triggered: true };
    public bool InteractDown => IsUsingMobileFallback && InteractButton != null ? InteractButton.GetButtonDown() : _player.Interact is { triggered: true };
    public bool IsPausedDown => IsUsingMobileFallback && PauseButton != null ? PauseButton.GetButton() : _player.Pause is { triggered: true };

    public bool IsOpenChatDown => IsUsingMobileFallback && OpenChatButton != null ? OpenChatButton.GetButtonDown() : _player.ChatOpen is { triggered: true };

    public bool IsSwitchChatDown => IsUsingMobileFallback && SwitchChatButton != null ? SwitchChatButton.GetButtonDown() : _player.SwitсhChat is { triggered: true };
    public bool IsRMB      => !IsBusy && (IsUsingMobileFallback ? Input.touchCount >= 2 :  _player.RMB is { triggered: true });
    public bool IsRMBDown  => !IsBusy && (IsUsingMobileFallback ? Input.touchCount >= 2 : _player.RMB != null && _player.RMB.ReadValue<float>() > 0.5f);
    public bool ForceCursorHeld => _player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f;

    public bool IsSlotsFullscreen => slotsFullscreenButton.GetButtonDown();
    public bool IsSlotsStream => slotsStreamButton.GetButtonDown();
    public bool IsSlotsScreenshot => slotsScreenshotButton.GetButtonDown();
    
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
