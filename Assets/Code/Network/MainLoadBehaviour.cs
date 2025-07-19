using System;
using Code.Network.Lobby;
using FishNet;
using UnityEngine;

namespace Code.Network
{
    public class MainLoadBehaviour : MonoBehaviour
    {
        private LobbyController _lobbyController;

        private void Awake()
        {
            _lobbyController = InstanceFinder.NetworkManager.GetComponent<LobbyController>();
        }

        private void Start()
        {
            _lobbyController.StartPollingLobbies();
        }
    }
}
