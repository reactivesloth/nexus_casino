using System;
using Code.API;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;

namespace Code.Network
{
    public class PlayerUI : NetworkBehaviour
    {
        [SerializeField] private TextMeshProUGUI playerName;

        private readonly SyncVar<string> _nickname = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ClientUnsynchronized,
            ReadPermission = ReadPermission.Observers
        });
        
        private void Start()
        { 
            Invoke("SetNickname", 1);
        }
        
        private void OnNicknameChanged(string newName, bool asServer)
        {
            // Обновляем UI
            if (playerName != null)
                playerName.text = newName;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnNicknameChanged(_nickname.Value, false);
        }

        public void SetNickname()
        {
            if (IsOwner)
            {
                string newName = ClientDataStorage.UserData.username;
                _nickname.Value = newName;
                SetNicknameServerRpc(newName);
            }
            
            playerName.text = _nickname.Value;
        }

        [ServerRpc(RequireOwnership = true)]
        private void SetNicknameServerRpc(string newName)
        {
            _nickname.Value = newName;
            playerName.text = _nickname.Value;
        }

        // [ObserversRpc(BufferLast = true)]
        // private void UpdateNicknames(string name)
        // {
        //     _nickname.Value = name;
        //     playerName.text = _nickname.Value;
        // }
    }
}