using System.Linq;
using Code.API;
using Code.Network;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
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
        
        public readonly SyncVar<bool> IsVoiceHeld = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        public readonly SyncVar<bool> IsVoiceMuted = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        
        public string PlayerName => playerName.text;
        public string PlayerRole => playerRole.text;
        public bool IsHost => hostIndicator.activeSelf;
        
        private void Update()
        {
            if (IsOwner)
            {
                var newHeld = !PlayerVoice.LocalPlayerVoiceInstance.isInputMuted;
                var newMuted = PlayerVoice.LocalPlayerVoiceInstance.isInputMutedByServer;

                // Если изменилось состояние — пересылаем на сервер только голосовые данные
                if (newHeld != IsVoiceHeld.Value || newMuted != IsVoiceMuted.Value)
                {
                    SendVoiceStateServerRpc(newHeld, newMuted, Owner);
                }
            }

            if (voiceImage != null)
            {
                voiceImage.color = IsVoiceMuted.Value ? Color.red : IsVoiceHeld.Value ? Color.white : Color.clear;
                voiceImage.gameObject.SetActive(IsVoiceHeld.Value || IsVoiceMuted.Value);
            }
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TransmitStaticCharacterData();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendStaticDataServerRpc(string nickname, string role, bool isHost, NetworkConnection sender = null)
        {
            SendStaticDataObserversRpc(nickname, role, isHost);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendStaticDataObserversRpc(string nickname, string role, bool isHost)
        {
            if (playerName != null) playerName.text = nickname ?? string.Empty;
            if (playerRole != null) playerRole.text = role ?? string.Empty;
            hostIndicator.SetActive(isHost);
        }

        public void TransmitStaticCharacterData()
        {
            if (!IsOwner) return;
            var user = ClientDataStorage.UserData;
            SendStaticDataServerRpc(user.username ?? "", user.role ?? "", ServerManager.Started);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SendVoiceStateServerRpc(bool voice, bool mute, NetworkConnection sender = null)
        {
            IsVoiceHeld.Value = voice;
            IsVoiceMuted.Value = mute;
        }

        public static PlayerUI GetByPlayerName(string playerName)
        {
            var all = FindObjectsByType<PlayerUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(ui => ui.PlayerName == playerName);
        }
    }
}