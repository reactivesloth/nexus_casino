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
            string name = GetLocalName();
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string productUserId = GetLocalAuthId();

            Debug.Log(name + " : " + deviceId + " : " + productUserId);

            SendAuthDataServerRpc(name, deviceId, productUserId);
        }

        #endregion

        #region SERVER

        [ServerRpc(RequireOwnership = false)]
        private void SendAuthDataServerRpc(string name, string deviceId, string productUserId, NetworkConnection sender = null)
        {
            var data = new PlayerData
            {
                PlayerName = name,
                DeviceId = deviceId,
                ProductUserId = productUserId
            };

            sender.CustomData = data;

            // Сообщаем всем клиентам о новых данных этого подключения
            foreach (var kvp in InstanceFinder.ServerManager.Clients)
            {
                var conn = kvp.Value;
                SendConnectionDataTargetRpc(conn, sender.ClientId, name, deviceId, productUserId);
            }

            // Отправляем новому подключившемуся данные о всех других клиентах
            foreach (var kvp in InstanceFinder.ServerManager.Clients)
            {
                var conn = kvp.Value;
                if (conn.ClientId == sender.ClientId)
                    continue; // самого себя не надо

                if (conn.CustomData is PlayerData existingData)
                {
                    SendConnectionDataTargetRpc(sender, conn.ClientId, existingData.PlayerName, existingData.DeviceId, existingData.ProductUserId);
                }
            }
        }

        #endregion

        #region RPC TO CLIENTS

        [TargetRpc]
        private void SendConnectionDataTargetRpc(NetworkConnection conn, int targetClientId, string name,
            string deviceId, string productUserId)
        {
            if (InstanceFinder.ClientManager.Clients.TryGetValue(targetClientId, out NetworkConnection targetConn))
            {
                targetConn.CustomData = new PlayerData
                {
                    PlayerName = name,
                    DeviceId = deviceId,
                    ProductUserId = productUserId
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
            return LobbyVariables.Instance.ProductUserId.ToString();
        }

        #endregion
    }
}