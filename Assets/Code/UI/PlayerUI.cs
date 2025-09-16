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
        
        private VoiceBroadcastTrigger _voiceBroadcastTrigger;

        private bool _isVoiceHeld;
        private bool _isVoiceMuted;
        
        private void Start()
        {
            _voiceBroadcastTrigger ??= FindAnyObjectByType<VoiceBroadcastTrigger>();
        }

        private void Update()
        {
            if (IsOwner)
            {
                var newHeld = _voiceBroadcastTrigger.VoiceHeld;
                var newMuted = _voiceBroadcastTrigger.IsMuted;

                // Если изменилось состояние — пересылаем на сервер только голосовые данные
                if (newHeld != _isVoiceHeld || newMuted != _isVoiceMuted)
                {
                    _isVoiceHeld = newHeld;
                    _isVoiceMuted = newMuted;
                    TransmitVoiceState();
                }
            }

            if (voiceImage != null)
            {
                voiceImage.color = _isVoiceMuted ? Color.red : _isVoiceHeld ? Color.green : Color.clear;
            }
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TransmitStaticCharacterData();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendStaticDataServerRpc(string nickname, string role, NetworkConnection sender = null)
        {
            SendStaticDataObserversRpc(nickname, role);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendStaticDataObserversRpc(string nickname, string role)
        {
            if (playerName != null) playerName.text = nickname ?? string.Empty;
            if (playerRole != null) playerRole.text = role ?? string.Empty;
        }

        public void TransmitStaticCharacterData()
        {
            if (!IsOwner) return;
            var user = ClientDataStorage.UserData;
            SendStaticDataServerRpc(user.username ?? "", user.role ?? "");
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendVoiceStateServerRpc(bool voice, bool mute, NetworkConnection sender = null)
        {
            SendVoiceStateObserversRpc(voice, mute);
        }

        [ObserversRpc]
        private void SendVoiceStateObserversRpc(bool voice, bool mute)
        {
            _isVoiceHeld = voice;
            _isVoiceMuted = mute;
        }

        public void TransmitVoiceState()
        {
            if (!IsOwner) return;
            SendVoiceStateServerRpc(_isVoiceHeld, _isVoiceMuted);
        }
    }
}