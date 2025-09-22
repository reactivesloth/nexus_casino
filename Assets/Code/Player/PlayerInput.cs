using Code.UI;
using UnityEngine;
using Code.Utility;
using NUnit.Framework;
using UnityEngine.Serialization;

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
    public UltimateButton SendChatMessageButton;
    public UltimateButton ChatScrollUpButton;
    public UltimateButton ChatScrollDownButton;

    [SerializeField] private bool ForceMobile;
    private bool savedHideMobileFallback;
    private bool prevBusy;
    
    public bool IsUsingMobileFallback { get; set; }
    public bool HideMobileFallback { get; set; }
    
    
    [Header(("Interactable Base UI"))] 
    public GameObject baseInteractUI;
    public UltimateButton baseEndInteractButton;
    
    [Header(("Interactable Slots UI"))] 
    public GameObject slotInteractUI;
    public UltimateButton slotsScreenshotButton;
    public UltimateButton slotsFullscreenButton;
    public UltimateButton slotsStreamButton;
    public UltimateButton slotsEndInteractButton;
    
    private UltimateButton endInteractButton;
    
    
    
    public bool IsBusy { get; set; }
    public bool IsChatOpened { get; set; }
    
    private void Awake()
    {
        Instance = this;

        IsUsingMobileFallback = ForceMobile || Application.isMobilePlatform;

        _inputAsset = new InputAsset();
        _player = _inputAsset.Player;
        _player.Enable();
        
        ShowInteractUI(false);
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

    private void SetBusy(bool value, bool forceUpdate = false)
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

        var isPaused = false;
        if (PauseUI.Instance != null)
            isPaused = PauseUI.Instance.IsPaused;
        
        if (mobileCanvas != null && IsUsingMobileFallback != mobileCanvas.activeSelf && !isPaused)
            mobileCanvas.SetActive(IsUsingMobileFallback);
        else if (isPaused)
            mobileCanvas.SetActive(false);

        if (IsUsingMobileFallback && mobileCanvas != null)
        {
            if (HideMobileFallback != savedHideMobileFallback)
            {
                if (MoveJoystick != null)      MoveJoystick.gameObject.SetActive(!HideMobileFallback);
                if (JumpButton != null)        JumpButton.gameObject.SetActive(!HideMobileFallback);
                if (SprintButton != null)      SprintButton.gameObject.SetActive(!HideMobileFallback);

                savedHideMobileFallback = HideMobileFallback;
            }
        }
    }

    public Vector2 Move
    {
        get
        {
            if (IsUsingMobileFallback && MoveJoystick != null)
                return new Vector2(MoveJoystick.HorizontalAxis * 0.85f / 0.85f, MoveJoystick.VerticalAxis * 0.85f / 0.85f);
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
            return IsBusy ? Vector2.down : v * (IsUsingMobileFallback ? lookSensitivity/4 : lookSensitivity);
        }
    }

    public void ShowInteractUI(bool value, string name = "Base")
    {
        switch (name)
        {
            case "Slots":
                slotInteractUI.SetActive(value);
                baseInteractUI.SetActive(false);
                endInteractButton = slotsEndInteractButton;
                InteractButton.gameObject.SetActive(!value);
                break;
            case "Base":
                slotInteractUI.SetActive(false);
                baseInteractUI.SetActive(value);
                endInteractButton = baseEndInteractButton;
                InteractButton.gameObject.SetActive(!value);
                break;
            default:
                slotInteractUI.SetActive(false);
                baseInteractUI.SetActive(false);
                InteractButton.gameObject.SetActive(true);
                endInteractButton = null;
                break;
        }
    }
    
    public bool JumpDown  => !IsChatOpened && !IsBusy && (IsUsingMobileFallback && JumpButton != null ? JumpButton.GetButtonDown() : _player.Jump is { triggered: true });
    public bool VoiceHeld => !IsChatOpened && !IsBusy && (IsUsingMobileFallback && VoiceButton != null ? VoiceButton.GetButton()   : _player.Voice != null && _player.Voice.ReadValue<float>() > 0.5f);
    public bool SprintHeld=> !IsChatOpened && !IsBusy && (IsUsingMobileFallback && MoveJoystick != null ? Mathf.Abs(MoveJoystick.VerticalAxis) > 0.85f || Mathf.Abs(MoveJoystick.HorizontalAxis) > 0.85f : _player.Sprint != null && _player.Sprint.ReadValue<float>() > 0.5f);
    public bool CameraSwitchDown => !IsChatOpened && !IsBusy && (IsUsingMobileFallback && CameraSwitchButton != null ? CameraSwitchButton.GetButtonDown() : _player.CameraSwitch is { triggered: true });
    public bool InteractDown => !IsChatOpened && !IsBusy && (IsUsingMobileFallback && InteractButton != null ? InteractButton.GetButtonDown() : _player.Interact is { triggered: true });
    public bool InteractEndDown => !IsChatOpened && (endInteractButton.GetButtonDown() || _player.Interact is { triggered: true });
    public bool IsPausedDown => IsUsingMobileFallback && PauseButton != null ? PauseButton.GetButtonDown() : _player.Pause is { triggered: true };
    public bool IsOpenChatDown => IsUsingMobileFallback && OpenChatButton != null ? OpenChatButton.GetButtonDown() : _player.ChatOpen is { triggered: true };
    public bool IsSwitchChatDown => IsUsingMobileFallback && SwitchChatButton != null ? SwitchChatButton.GetButtonDown() : _player.SwitсhChat is { triggered: true };
    public bool IsRmbDown  => !IsChatOpened && !IsBusy && (IsUsingMobileFallback ? Input.touchCount >= 2 : _player.RMB != null && _player.RMB.ReadValue<float>() > 0.5f);
    public bool ForceCursorHeld => IsChatOpened || (_player.ForceCursor != null && _player.ForceCursor.ReadValue<float>() > 0.5f);
    public float Zoom => !IsChatOpened && !IsBusy ? Input.GetAxis("Mouse ScrollWheel") : 0;
    public bool IsSlotsFullscreen => !IsChatOpened && slotsFullscreenButton.GetButtonDown();
    public bool IsSlotsStream => !IsChatOpened && slotsStreamButton.GetButtonDown();
    public bool IsSlotsScreenshot => !IsChatOpened && slotsScreenshotButton.GetButtonDown() && !slotsScreenshotButton.InCooldown;
    public bool IsScrollUpButton => ChatScrollUpButton.GetButtonDown();
    public bool IsScrollDownButton => ChatScrollDownButton.GetButtonDown();
    public bool SendChatMessageButtonDown => SendChatMessageButton.GetButtonDown();
}
