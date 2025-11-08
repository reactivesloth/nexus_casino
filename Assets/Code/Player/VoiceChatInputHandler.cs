using Dissonance;
using UnityEngine;
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
        
        private void Awake()
        {
            voiceBroadcastTrigger ??= GetComponent<VoiceBroadcastTrigger>();
        }

        private void Update()
        {
            if (saveTime > 0)
                saveTime -= Time.deltaTime;
            else if (PlayerInput.Instance.VoiceHeld)
            {
#if UNITY_ANDROID
                if (AndroidRuntimePermissions.CheckPermission("android.permission.RECORD_AUDIO"))
                {
                    VoiceChatHandle(!voiceHeld);
                }
                else
                {
                    RequestPermission();
                }
#else
                VoiceChatHandle(!voiceHeld);
#endif
            }
        }

#if UNITY_ANDROID
        async void RequestPermission()
        {
            saveTime = 1000000;
            AndroidRuntimePermissions.Permission result = await AndroidRuntimePermissions.RequestPermissionAsync( "android.permission.RECORD_AUDIO" );
            if (result == AndroidRuntimePermissions.Permission.Granted || result == AndroidRuntimePermissions.Permission.ShouldAsk)
            {
                VoiceChatHandle(!voiceHeld);
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
