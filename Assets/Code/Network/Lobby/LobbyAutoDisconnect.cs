using System;
using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using FishNet.Managing.Server;
using FishNet.Transporting.FishyEOSPlugin;
using PlayEveryWare.EpicOnlineServices;

namespace Code.Network.Lobby
{
    public class LobbyAutoDisconnect : MonoBehaviour
    {
        private ClientManager _clientManager;
        private ServerManager _serverManager;
        private LobbyController _lobbyController;
        private FishyEOS _fishyEos;

        private void Awake()
        {
            _clientManager = InstanceFinder.ClientManager;
            _serverManager = InstanceFinder.ServerManager;
            _lobbyController = _clientManager.GetComponent<LobbyController>();
            _fishyEos = _clientManager.GetComponent<FishyEOS>();
            
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
                Disconnect();
        }

        private void Disconnect()
        {
            if (_clientManager)
                _clientManager.StopConnection();
            if (_lobbyController)
                _lobbyController.LeaveLobby();
            
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}