using Code.Network;
using Code.UI.Popup;
using Ricimi;
using UnityEngine;
using UnityEngine.UI;
using static AndroidRuntimePermissions;
using Gradient = Ricimi.Gradient;

namespace Code.Player
{
    public class VoiceChatInputHandler : MonoBehaviour
    {
        [SerializeField] private Gradient mobileButtonImage;
        [SerializeField] private Color on1, on2, off1, off2;
        
        private bool voiceHeld = false;
        private float saveTime;
        
        private NexusModularPopupOpener _popupOpener;
        
        private void Awake()
        {
            _popupOpener = FindAnyObjectByType<NexusModularPopupOpener>(FindObjectsInactive.Include);
        }

        private void Update()
        {
            if (saveTime > 0)
                saveTime -= Time.deltaTime;
            else if (PlayerInput.Instance.VoiceHeld)
            {
                if (voiceHeld) VoiceChatHandle(false);
                else
                {
#if UNITY_ANDROID
                if (CheckPermission("android.permission.RECORD_AUDIO"))
                {
                    VoiceChatHandle(true);
                }
                else
                {
                    RequestPermission();
                }
#else
                    VoiceChatHandle(true);
#endif
                }
            }
        }
        
#if UNITY_ANDROID
        async void RequestPermission()
        {
            saveTime = 1000000;
            var result = await RequestPermissionAsync( "android.permission.RECORD_AUDIO" );
            switch (result)
            {
                case Permission.Granted:
                    VoiceChatHandle(true);
                    break;
                case Permission.ShouldAsk:
                {
                    _popupOpener.Title = "Разрешение записи голоса";
                    _popupOpener.Subtitle = "";
                    _popupOpener.Message = "Вам необходимо разрешить использование микрофона для того чтобы работал голосовой чат";
            
                    var okButton = new ButtonInfo
                    {
                        Label = "Ок",
                        ClosePopupWhenClicked = true,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    var cancellButton = new ButtonInfo
                    {
                        Label = "Нет",
                        ClosePopupWhenClicked = false,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    okButton.OnClickedEvent.AddListener(RequestPermission);
                    cancellButton.OnClickedEvent.AddListener(_popupOpener.ClosePopup);
                    _popupOpener.Buttons.Add(okButton);
                    _popupOpener.Buttons.Add(cancellButton);
                    _popupOpener.OpenPopup();
                    break;
                }
                case Permission.Denied:
                {
                    _popupOpener.Title = "Разрешение записи голоса";
                    _popupOpener.Subtitle = "";
                    _popupOpener.Message = "Т.к. вы выбрали больше не спрашивать, то приложение не может снова вызвать разрешение для микрофона, " +
                                           "необходимое для работы голосового чата. Вам необходимо зайти в настройки, в поиске найти Nexus Meta Club," +
                                           " внутри зайти в пункт Разрешения и в разрешении для микрофона выбрать пункт Разрешить всегда";
            
                    var okButton = new ButtonInfo
                    {
                        Label = "Хорошо",
                        ClosePopupWhenClicked = true,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    okButton.OnClickedEvent.AddListener(RequestPermission);
                    _popupOpener.Buttons.Add(okButton);
                    _popupOpener.OpenPopup();
                    break;
                }
            }
        }
#endif
   
        public void VoiceChatHandle(bool value)
        {
            saveTime = 0.2f;
            voiceHeld = value;
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

        public void OnApplicationPause(bool pauseStatus)
        {
            VoiceChatHandle(false);
        }

        public void OnApplicationFocus(bool hasFocus)
        {
            VoiceChatHandle(false);
        }
    }
}
