using Code.API;
using Dissonance;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    public class PlayerUI : NetworkBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private TextMeshProUGUI playerRole;
        [SerializeField] private Image voiceImage;
        
        private VoiceBroadcastTrigger voiceBroadcastTrigger;

        private bool isVoiceHeld;
        private bool isVoiceMuted;
        
        private void Start()
        {
            voiceBroadcastTrigger ??= FindAnyObjectByType<VoiceBroadcastTrigger>();
        }

        private void Update()
        {
            if (IsOwner)
            {
                isVoiceHeld = voiceBroadcastTrigger.VoiceHeld;
                isVoiceMuted =  voiceBroadcastTrigger.IsMuted;
            }

            if (voiceImage != null)
            {
                voiceImage.color = isVoiceMuted ? Color.red : isVoiceHeld ? Color.green : Color.clear;
            }
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TransmitLocalCharacter();
        }

        [ServerRpc] // при необходимости можно добавить RequireOwnership=false
        public void SendCharacterDataServerRpc(string _nickname, string _role, bool _voice, bool _mute, NetworkConnection sender = null)
        {
            SendCharacterDataObserversRpc(_nickname, _role, _voice, _mute);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterDataObserversRpc(string _nickname, string _role, bool _voice, bool _mute)
        {
            if (playerName != null) playerName.text = _nickname ?? string.Empty;
            if (playerRole != null) playerRole.text = _role ?? string.Empty;
            isVoiceHeld = _voice;
            isVoiceMuted = _mute;
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return;

            var user = ClientDataStorage.UserData;

            SendCharacterDataServerRpc(user.username ?? "", user.role ?? "", isVoiceHeld, isVoiceMuted);
        }
    }
}