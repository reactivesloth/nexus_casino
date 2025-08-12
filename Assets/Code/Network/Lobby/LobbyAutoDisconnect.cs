using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;

namespace Code.Network.Lobby
{
    /// <summary>
    /// На выходе из приложения/уничтожении объекта корректно рвёт сетевые соединения и покидает лобби.
    /// </summary>
    public sealed class LobbyAutoDisconnect : MonoBehaviour
    {
        private static ServerManager _server;
        private static ClientManager _client;
        private static LobbyController _lobby;

        private void Awake()
        {
            _server = InstanceFinder.ServerManager;
            _client = InstanceFinder.ClientManager;
            _lobby  = (_client != null) ? _client.GetComponent<LobbyController>() : null;
        }

        private void OnDestroy() => Disconnect();
        private void OnApplicationQuit() => Disconnect();

        public static void Disconnect()
        {
            if (_client != null)
                _client.StopConnection();
            if (_server != null)
                _server.StopConnection(false);
            if (_lobby != null)
                _lobby.LeaveLobby();

            var cursorMgr = CursorManager.Instance;
            if (cursorMgr != null)
                cursorMgr.ShowCursor();
        }
    }
}