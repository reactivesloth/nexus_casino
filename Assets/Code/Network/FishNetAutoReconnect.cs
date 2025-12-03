using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network
{
    public class FishNetAutoReconnect : MonoBehaviour
    {
        private NetworkManager networkManager;
        private bool isReconnecting = false;

        private void Awake()
        {
            networkManager = InstanceFinder.NetworkManager;
            if(networkManager == null)
            {
                Debug.LogError("FishNet NetworkManager not found!");
                enabled = false;
                return;
            }
            networkManager.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDestroy()
        {
            if(networkManager != null)
                networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if(args.ConnectionState == LocalConnectionState.Stopped)
            {
                Debug.LogWarning("Connection failed, attempting reconnect...");
                if(!isReconnecting)
                {
                    isReconnecting = true;
                    Invoke(nameof(TryReconnect), 2f); // ждем 2 секунды перед повтором
                }
            }
        }

        private void TryReconnect()
        {
            // Берем последний IP адрес, установленный в транспорте
            string ipAddress = networkManager.TransportManager.Transport.GetClientAddress();

            if (string.IsNullOrEmpty(ipAddress))
            {
                Debug.LogWarning("No valid client IP address found for reconnect.");
                isReconnecting = false;
                return;
            }

            Debug.Log($"Reconnecting to {ipAddress}...");

            networkManager.TransportManager.Transport.SetClientAddress(ipAddress);
            bool started = networkManager.ClientManager.StartConnection();

            if (started)
            {
                Debug.Log("Reconnect attempt started.");
            }
            else
            {
                Debug.LogWarning("Failed to start reconnect attempt.");
            }

            isReconnecting = false;
        }
    }
}
