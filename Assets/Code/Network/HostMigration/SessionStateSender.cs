using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network
{
    public class SessionStateSender : NetworkBehaviour
    {
        public static SessionStateSender Instance;

        private void Awake() => Instance = this;
        
        public void SendSessionStateToHost(PlayerCharacterState playerCharacterState)
        {
            string playerStateJson = JsonUtility.ToJson(playerCharacterState);
            SendPlayerStateToHost(playerStateJson);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendPlayerStateToHost(string json, NetworkConnection sender = null)
        {
            var state = JsonUtility.FromJson<PlayerCharacterState>(json);
            HostSessionRestorer.Instance.RestorePlayerCharacterState(state, sender);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendSessionStateServerRpc(string json, NetworkConnection sender = null)
        {
            var state = JsonUtility.FromJson<PlayerSessionState>(json);
            HostSessionRestorer.Instance.RestorePlayerState(state, sender);
        }

        // Пример метода для получения своих объектов
        private IEnumerable<NetworkObject> GetOwnedNetworkObjects()
        {
            foreach (var obj in ClientManager.Connection.Objects)
            {
                yield return obj;
            }
        }
    }
}