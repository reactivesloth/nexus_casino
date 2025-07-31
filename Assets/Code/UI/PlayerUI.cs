using Code.API;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;

namespace Code.UI
{
    public class PlayerUI : NetworkBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerName;
        [SerializeField] private TextMeshProUGUI playerRole;

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            TransmitLocalCharacter();
        }

        [ServerRpc]
        public void SendCharacterDataServerRpc(string _nickname, string _role, NetworkConnection sender = null)
        {
            SendCharacterDataObserversRpc(_nickname, _role);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterDataObserversRpc(string _nickname, string _role)
        {
            playerName.text = _nickname;
            playerRole.text = _role;
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return;
            SendCharacterDataServerRpc(ClientDataStorage.UserData.username, ClientDataStorage.UserData.role);
        }
    }
}