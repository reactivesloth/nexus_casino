using UnityEngine;
using FishNet;
using FishNet.Managing.Client;

namespace Code.Network.Lobby
{
    public class LobbyAutoDisconnect : MonoBehaviour
    {
        private ClientManager _clientManager;

        private void Awake()
        {
            // Получаем ссылку на ClientManager
            _clientManager = InstanceFinder.ClientManager;
        }

        private void OnApplicationQuit()
        {
            if (_clientManager != null)
                _clientManager.StopConnection();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && _clientManager != null)
                _clientManager.StopConnection();
        }
    }
}