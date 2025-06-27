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
        
        private bool _isStartNetwork = false;

        private void Awake() => Instance = this;
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            _isStartNetwork = true;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isStartNetwork = false;
        }
        
        public void SendSessionStateToHost(MigratePlayerData playerCharacterState)
        {
            string playerStateJson = JsonUtility.ToJson(playerCharacterState);
            StartCoroutine(SendPlayerStateToHostWhereReady(playerStateJson));
        }

        private IEnumerator SendPlayerStateToHostWhereReady(string playerStateJson)
        {
            // Ждём, пока клиент полностью стартует
            yield return new WaitUntil(() => base.NetworkManager != null && base.NetworkManager.IsClientStarted);

            // Ждём, пока объект заспавнен и инициализирован
            yield return new WaitUntil(() => IsSpawned && _isStartNetwork);
            
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