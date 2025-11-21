using Code.UI.Popup;
using Dissonance;
using Ricimi;
using UnityEngine;
using UnityEngine.UI;
using Gradient = Ricimi.Gradient;

namespace Code.Player
{
    public class VoiceChatInputHandler : MonoBehaviour
    {
        [SerializeField] private VoiceBroadcastTrigger voiceBroadcastTrigger;
        [SerializeField] private Gradient mobileButtonImage;
        [SerializeField] private Color on1, on2, off1, off2;

        private bool voiceHeld = false;
        private float saveTime;
        
        private NexusModularPopupOpener _popupOpener;
        
        private void Awake()
        {
            voiceBroadcastTrigger ??= GetComponent<VoiceBroadcastTrigger>();
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
                if (AndroidRuntimePermissions.CheckPermission("android.permission.RECORD_AUDIO"))
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
            AndroidRuntimePermissions.Permission result = await AndroidRuntimePermissions.RequestPermissionAsync( "android.permission.RECORD_AUDIO" );
            if (result == AndroidRuntimePermissions.Permission.Granted)
            {
                VoiceChatHandle(!voiceHeld);
            } 
            else if (result != AndroidRuntimePermissions.Permission.ShouldAsk)
            {
                if (_popupOpener != null)
                {
                    _popupOpener.Title = "Voice chat require permission for recording";
                    _popupOpener.Subtitle = "";
                    _popupOpener.Message = "You need give permission for recording for voice chat enable";
            
                    var okButton = new ButtonInfo
                    {
                        Label = "Allow",
                        ClosePopupWhenClicked = true,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    var cancellButton = new ButtonInfo
                    {
                        Label = "No",
                        ClosePopupWhenClicked = false,
                        OnClickedEvent = new Button.ButtonClickedEvent()
                    };
                    okButton.OnClickedEvent.AddListener(RequestPermission);
                    cancellButton.OnClickedEvent.AddListener(_popupOpener.ClosePopup);
                    _popupOpener.Buttons.Add(okButton);
                    _popupOpener.Buttons.Add(cancellButton);
                    _popupOpener.OpenPopup();
                }
            }
        }
#endif
   
        public void VoiceChatHandle(bool value)
        {
            saveTime = 0.2f;
            voiceHeld = value;
            voiceBroadcastTrigger.VoiceHeld = voiceHeld;
                
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
