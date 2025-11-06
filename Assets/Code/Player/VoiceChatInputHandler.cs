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
                saveTime = 0.2f;
                voiceHeld = !voiceHeld;
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
}
