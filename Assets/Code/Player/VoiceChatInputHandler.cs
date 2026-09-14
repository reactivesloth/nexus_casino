using Code.Network.Player;
using Code.UI.Popup;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Gradient = Ricimi.Gradient;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace Code.Player
{
    public class VoiceChatInputHandler : MonoBehaviour
    {
        [SerializeField] private Gradient mobileButtonImage;
        [SerializeField] private Color on1, on2, off1, off2;

        private bool _voiceHeld = false;
        private float _saveTime;
        private NexusModularPopupOpener _popupOpener;

#if UNITY_ANDROID
        private const string MicRequestedOnceKey = "perm_mic_requested_once";
        private const string MicPermission = Permission.Microphone;
        private bool _permissionRequestInFlight;
        private PermissionCallbacks _permissionCallbacks;
#endif

        private void Start()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            VoiceChatHandle(false);
        }

        private void Update()
        {
            if (_saveTime > 0)
            {
                _saveTime -= Time.deltaTime;
                return;
            }

            if (PlayerInput.Instance.VoiceHeld)
            {
                if (_voiceHeld)
                {
                    VoiceChatHandle(false);
                }
                else
                {
#if UNITY_ANDROID
                    if (HasMicPermission())
                    {
                        VoiceChatHandle(true);
                    }
                    else
                    {
                        TryRequestMicPermissionOrShowUI();
                    }
#else
                    VoiceChatHandle(true);
#endif
                }
            }
        }

#if UNITY_ANDROID
        private bool HasMicPermission()
        {
            return Permission.HasUserAuthorizedPermission(MicPermission);
        }

        private void TryRequestMicPermissionOrShowUI()
        {
            if (_permissionRequestInFlight)
                return;

            if (Permission.ShouldShowRequestPermissionRationale(MicPermission))
            {
                ShowShouldAskPopup();
                return;
            }

            RequestMicPermission();
        }

        private void RequestMicPermission()
        {
            if (_permissionRequestInFlight)
                return;

            _permissionRequestInFlight = true;

            _permissionCallbacks = new PermissionCallbacks();
            _permissionCallbacks.PermissionGranted += OnPermissionGranted;
            _permissionCallbacks.PermissionDenied += OnPermissionDenied;

            PlayerPrefs.SetInt(MicRequestedOnceKey, 1);
            PlayerPrefs.Save();

            Permission.RequestUserPermission(MicPermission, _permissionCallbacks);
        }

        private void OnPermissionGranted(string permission)
        {
            if (permission != MicPermission) return;

            _permissionRequestInFlight = false;
            
            VoiceChatHandle(true);
        }

        private void OnPermissionDenied(string permission)
        {
            if (permission != MicPermission) return;

            _permissionRequestInFlight = false;

            bool shouldShowRationale = Permission.ShouldShowRequestPermissionRationale(MicPermission);
            bool requestedOnce = PlayerPrefs.GetInt(MicRequestedOnceKey, 0) == 1;

            if (shouldShowRationale)
            {
                ShowShouldAskPopup();
                return;
            }

            if (requestedOnce)
                ShowDeniedPopup();
            else
                ShowShouldAskPopup();
        }

        private void ShowShouldAskPopup()
        {
            _popupOpener.Buttons.Clear();

            _popupOpener.Title = LocalizationHelper.GetLocalizedString("labels.record.permission_title");
            _popupOpener.Subtitle = "";
            _popupOpener.Message = LocalizationHelper.GetLocalizedString("labels.record.permission_message");

            var okButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("buttons.ok"),
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };

            var cancelButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("buttons.cancel"),
                ClosePopupWhenClicked = false,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };

            okButton.OnClickedEvent.AddListener( () =>
            {
                RequestMicPermission();
                _popupOpener.ClosePopup();
            });
            cancelButton.OnClickedEvent.AddListener(_popupOpener.ClosePopup);

            _popupOpener.Buttons.Add(okButton);
            _popupOpener.Buttons.Add(cancelButton);
            _popupOpener.OpenPopup();
        }

        private void ShowDeniedPopup()
        {
            _popupOpener.Buttons.Clear();

            _popupOpener.Title = LocalizationHelper.GetLocalizedString("labels.record.permission_title");
            _popupOpener.Subtitle = "";
            _popupOpener.Message = LocalizationHelper.GetLocalizedString("labels.record.permission_instruction");

            var okButton = new ButtonInfo
            {
                Label = LocalizationHelper.GetLocalizedString("buttons.fine"),
                ClosePopupWhenClicked = true,
                OnClickedEvent = new Button.ButtonClickedEvent()
            };

            okButton.OnClickedEvent.AddListener( () =>
            {
                OpenAppSettings();
                _popupOpener.ClosePopup();
            });

            _popupOpener.Buttons.Add(okButton);
            _popupOpener.OpenPopup();
        }

        private static void OpenAppSettings()
        {
            using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            string packageName = activity.Call<string>("getPackageName");

            using var uriClass = new AndroidJavaClass("android.net.Uri");
            using var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", packageName, null);

            using var intent = new AndroidJavaObject(
                "android.content.Intent",
                "android.settings.APPLICATION_DETAILS_SETTINGS",
                uri
            );

            intent.Call<AndroidJavaObject>("addFlags", 0x10000000);
            activity.Call("startActivity", intent);
        }
#endif

        public void VoiceChatHandle(bool value)
        {
            _saveTime = 0.2f;

            _voiceHeld = value;

            if (PlayerVoice.LocalInstance != null)
                PlayerVoice.LocalInstance.SetMuteState (!_voiceHeld);

            if (mobileButtonImage != null)
            {
                mobileButtonImage.Color1 = _voiceHeld ? on1 : off1;
                mobileButtonImage.Color2 = _voiceHeld ? on2 : off2;
                mobileButtonImage.gameObject.SetActive(false);
                mobileButtonImage.gameObject.SetActive(true);
            }
        }
    }
}
