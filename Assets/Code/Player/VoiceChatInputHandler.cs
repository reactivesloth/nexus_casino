using Code.Network;
using Code.UI.Popup;
using Code.Utility;
using Ricimi;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Vivox;
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

        private bool voiceHeld = false;
        private float saveTime;
        private NexusModularPopupOpener _popupOpener;

#if UNITY_ANDROID
        private const string MicRequestedOnceKey = "perm_mic_requested_once";
        private const string MicPermission = Permission.Microphone;
        private bool _permissionRequestInFlight;
        private PermissionCallbacks _permissionCallbacks;
#endif

        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
            VoiceChatHandle(false);
        }

        private void Update()
        {
            if (saveTime > 0)
            {
                saveTime -= Time.deltaTime;
                return;
            }

            // Логика TOGGLE (по нажатию):
            // Если кнопка нажата (и прошёл debounce saveTime):
            // 1. Если микрофон уже включен (voiceHeld) -> выключаем.
            // 2. Если выключен -> проверяем права и включаем.
            if (PlayerInput.Instance.VoiceHeld)
            {
                if (voiceHeld)
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

            // Если Android говорит, что нужно объяснить (обычно после отказа, но до "Don't ask again")
            if (Permission.ShouldShowRequestPermissionRationale(MicPermission))
            {
                ShowShouldAskPopup();
                return;
            }

            // Иначе - либо первый раз, либо уже заблокировано. Пробуем запросить.
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

            // Запоминаем, что мы хотя бы раз пытались запросить (для определения "Don't ask again" в будущем)
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
                // Пользователь отказал, но "Don't ask again" не нажато -> предлагаем попробовать ещё раз
                ShowShouldAskPopup();
                return;
            }

            // Если shouldShowRationale == false:
            // 1. Либо это первый запрос (но мы уже сохранили requestedOnce=1 перед вызовом, так что этот кейс отсекаем проверкой requestedOnce,
            //    но на всякий случай, если logic flow изменится, первый раз лучше не пугать настройками).
            // 2. Либо "Don't ask again" (ведение в настройки).

            if (requestedOnce)
                ShowDeniedPopup();    // Уже спрашивали, значит это блок -> настройки
            else
                ShowShouldAskPopup(); // На всякий случай fallback -> обычный попап
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

            okButton.OnClickedEvent.AddListener(RequestMicPermission);
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

            okButton.OnClickedEvent.AddListener(OpenAppSettings);

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

            intent.Call<AndroidJavaObject>("addFlags", 0x10000000); // FLAG_ACTIVITY_NEW_TASK
            activity.Call("startActivity", intent);
        }
#endif

        public void VoiceChatHandle(bool value)
        {
            // Debounce, чтобы одно нажатие не переключало статус много раз подряд
            saveTime = 0.2f;

            voiceHeld = value;
            
            if (VivoxService.Instance != null && !VivoxService.Instance.IsLoggedIn && VivoxVoiceManager.Instance != null) 
                VivoxVoiceManager.Instance.LoginToVivox();
            
            if (PlayerVoice.LocalPlayerVoiceInstance != null)
                PlayerVoice.LocalPlayerVoiceInstance.isInputMuted = !voiceHeld;

            if (mobileButtonImage != null)
            {
                mobileButtonImage.Color1 = voiceHeld ? on1 : off1;
                mobileButtonImage.Color2 = voiceHeld ? on2 : off2;
                mobileButtonImage.gameObject.SetActive(false);
                mobileButtonImage.gameObject.SetActive(true);
            }
        }
    }
}
