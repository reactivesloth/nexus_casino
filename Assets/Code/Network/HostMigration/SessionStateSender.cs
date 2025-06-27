using System;
using System.Collections;
using System.Collections.Generic;
using Code.Network.HostMigration.Data;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.HostMigration
{
    public class SessionStateSender : NetworkBehaviour
    {
        public static SessionStateSender Instance;

        private void Awake() => Instance = this;

        public void SendSessionStateToHost(MigratePlayerData playerCharacterState)
        {
            string playerStateJson = JsonUtility.ToJson(playerCharacterState);
            StartCoroutine(SendPlayerStateToHostWhereReady(playerStateJson));
        }

        public IEnumerator SendPlayerStateToHostWhereReady(string playerStateJson)
        {
            yield return new WaitWhile(() => !ClientManager.Started);
            SendPlayerStateToHost(playerStateJson);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendPlayerStateToHost(string json, NetworkConnection sender = null)
        {
            var state = JsonUtility.FromJson<MigratePlayerData>(json);
            HostSessionRestorer.Instance.RestorePlayerData(state, sender);
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