using Code.Network;
using Code.UI.Popup;
using Code.Utility;
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
            
            VoiceChatHandle(false);
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
                    _popupOpener.Title = LocalizationHelper.GetLocalizedString("labels.record.permission_title");
                    _popupOpener.Subtitle = "";
                    _popupOpener.Message = LocalizationHelper.GetLocalizedString("labels.record.permission_message");
            
                    var okButton = new ButtonInfo
                    {
                        Label = LocalizationHelper.GetLocalizedString("buttons.ok"),
                        ClosePopupWhenClicked = true,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    var cancellButton = new ButtonInfo
                    {
                        Label = LocalizationHelper.GetLocalizedString("buttons.cancel"),
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
                    _popupOpener.Title = LocalizationHelper.GetLocalizedString("labels.record.permission_title");
                    _popupOpener.Subtitle = "";
                    _popupOpener.Message = LocalizationHelper.GetLocalizedString("labels.record.permission_instruction");
                    var okButton = new ButtonInfo
                    {
                        Label = LocalizationHelper.GetLocalizedString("buttons.fine"),
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
    }
}
