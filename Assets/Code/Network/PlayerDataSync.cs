using Code.Network.Player;
using EOSLobby;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network
{
    public class PlayerDataSync : NetworkBehaviour
    {
        #region CLIENT

        public override void OnStartClient()
        {
            if (IsOwner)
            {
                string name = GetLocalName();
                string deviceId = SystemInfo.deviceUniqueIdentifier;
                string authId = GetLocalAuthId();

                SendAuthDataServerRpc(name, deviceId, authId);
            }
        }

        #endregion

        #region SERVER

        [ServerRpc]
        private void SendAuthDataServerRpc(string name, string deviceId, string authId, NetworkConnection sender = null)
        {
            var data = new PlayerData()
            {
                PlayerName = name,
                DeviceId = deviceId,
                AuthId = authId
            };

            sender.CustomData = data;

            // Сообщаем всем клиентам о новых данных этого подключения
            foreach (var kvp in InstanceFinder.ServerManager.Clients)
            {
                var conn = kvp.Value;
                SendConnectionDataTargetRpc(conn, sender.ClientId, name, deviceId, authId);
            }
        }

        #endregion

        #region RPC TO CLIENTS

        [TargetRpc]
        private void SendConnectionDataTargetRpc(NetworkConnection conn, int targetClientId, string name,
            string deviceId, string authId)
        {
            if (InstanceFinder.ClientManager.Clients.TryGetValue(targetClientId, out NetworkConnection targetConn))
            {
                targetConn.CustomData = new PlayerData
                {
                    PlayerName = name,
                    DeviceId = deviceId,
                    AuthId = authId
                };

                Debug.Log($"Заполнено CustomData для ClientId={targetClientId}: name={name}");
            }
            else
            {
                Debug.LogWarning($"Не найден ClientId={targetClientId} в ClientManager.Clients");
            }
        }

        #endregion

        #region UTILS

        private string GetLocalName()
        {
            return LobbyVariables.Instance.displayName;
        }

        private string GetLocalAuthId()
        {
            return System.Guid.NewGuid().ToString();
        }

        #endregion
    }
}