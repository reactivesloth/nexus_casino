using Code.Utility;
using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;

namespace Code.Network.Lobby
{
    public class LobbyAutoDisconnect : MonoBehaviour
    {
        private static ServerManager _serverManager;
        private static ClientManager _clientManager;
        private static LobbyController _lobbyController;

        private void Awake()
        {
            _serverManager = InstanceFinder.ServerManager;
            _clientManager = InstanceFinder.ClientManager;
            _lobbyController = _clientManager.GetComponent<LobbyController>();
            
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public static void Disconnect()
        {
            if (_clientManager)
                _clientManager.StopConnection();
            if (_serverManager)
                _serverManager.StopConnection(false);
            if (_lobbyController)
                _lobbyController.LeaveLobby();
            
            CursorManager.Instance.ShowCursor();
        }
    }
}