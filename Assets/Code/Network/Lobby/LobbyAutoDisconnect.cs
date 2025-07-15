using System;
using UnityEngine;
using FishNet;
using FishNet.Managing.Client;
using PlayEveryWare.EpicOnlineServices;

namespace Code.Network.Lobby
{
    public class LobbyAutoDisconnect : MonoBehaviour
    {
        private ClientManager _clientManager;
        private LobbyController _lobbyController;

        private void Awake()
        {
            _clientManager = InstanceFinder.ClientManager;
            _lobbyController = _clientManager.GetComponent<LobbyController>();
        }

        private void OnDestroy()
        {
            if (_clientManager)
                _clientManager.StopConnection();
            if (_lobbyController)
                _lobbyController.LeaveLobby();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && _clientManager != null)
                _clientManager.StopConnection();
        }
        
    }
}