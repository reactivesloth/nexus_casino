using Code.API;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;

namespace Code.Network
{
    public class PlayerUI : NetworkBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerName;

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TransmitLocalCharacter();
        }

        [ServerRpc]
        public void SendCharacterNameServerRpc(string _nickname, NetworkConnection sender = null)
        {
            SendCharacterNameObserversRpc(_nickname);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterNameObserversRpc(string _nickname)
        {
            playerName.text = _nickname;
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return;
            SendCharacterNameServerRpc(ClientDataStorage.UserData.username);
        }
    }
}