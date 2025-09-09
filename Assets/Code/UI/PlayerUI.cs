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

        [ServerRpc] // при необходимости можно добавить RequireOwnership=false
        public void SendCharacterDataServerRpc(string _nickname, string _role, NetworkConnection sender = null)
        {
            SendCharacterDataObserversRpc(_nickname, _role);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterDataObserversRpc(string _nickname, string _role)
        {
            if (playerName != null) playerName.text = _nickname ?? string.Empty;
            if (playerRole != null) playerRole.text = _role ?? string.Empty;
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return;

            var user = ClientDataStorage.UserData;

            SendCharacterDataServerRpc(user.username ?? "", user.role ?? "");
        }
    }
}