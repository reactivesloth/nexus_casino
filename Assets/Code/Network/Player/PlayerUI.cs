using System.Linq;
using Code.API;
using PurrNet;
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
        [SerializeField] private GameObject hostIndicator;
        
        public readonly SyncVar<bool> IsVoiceHeld = new SyncVar<bool>(false);
        public readonly SyncVar<bool> IsVoiceMuted = new SyncVar<bool>(false);


        public string PlayerName => playerName.text;
        public string PlayerRole => playerRole.text;
        public bool IsHost => hostIndicator.activeSelf;
        
        private void Update()
        {
            if (isOwner)
            {
                var newHeld = false; //!PlayerVoice.LocalPlayerVoiceInstance.isInputMuted;
                var newMuted = false; //PlayerVoice.LocalPlayerVoiceInstance.isInputMutedByServer;

                // Если изменилось состояние — пересылаем на сервер только голосовые данные
                if (newHeld != IsVoiceHeld.value || newMuted != IsVoiceMuted.value)
                {
                    SendVoiceStateServerRpc(newHeld, newMuted, owner);
                }
            }

            if (voiceImage != null)
            {
                voiceImage.color = IsVoiceMuted.value ? Color.red : IsVoiceHeld.value ? Color.white : Color.clear;
                voiceImage.gameObject.SetActive(IsVoiceHeld.value || IsVoiceMuted.value);
            }
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
            TransmitStaticCharacterData();
        }

        [ServerRpc(requireOwnership: false)]
        public void SendStaticDataServerRpc(string nickname, string role, bool host, PlayerID sender = default)
        {
            SendStaticDataObserversRpc(nickname, role, host);
        }

        [ObserversRpc(bufferLast: true)]
        private void SendStaticDataObserversRpc(string nickname, string role, bool host)
        {
            if (playerName != null) playerName.text = nickname ?? string.Empty;
            if (playerRole != null) playerRole.text = role ?? string.Empty;
            hostIndicator.SetActive(host);
        }

        public void TransmitStaticCharacterData()
        {
            if (!isOwner) return;
            var user = ClientDataStorage.UserData;
            SendStaticDataServerRpc(user.username ?? "", user.role ?? "", isHost);
        }

        [ServerRpc(requireOwnership: false)]
        public void SendVoiceStateServerRpc(bool voice, bool mute, PlayerID? sender)
        {
            IsVoiceHeld.value = voice;
            IsVoiceMuted.value = mute;
        }

        public static PlayerUI GetByPlayerName(string playerName)
        {
            var all = FindObjectsByType<PlayerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(ui => ui.PlayerName == playerName);
        }
    }
}