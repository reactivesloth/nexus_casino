using System.Collections;
using Code.Network.Lobby;
using FishNet;
using UnityEngine;

namespace Code.Network
{
    /// <summary>
    /// Безопасно находит LobbyController и стартует поллинг.
    /// Переживает ситуацию, когда NetworkManager ещё не создан.
    /// </summary>
    public sealed class MainLoadBehaviour : MonoBehaviour
    {
        private LobbyController _lobbyController;

        private void Awake()
        {
            TryResolveLobbyController();
        }

        private void Start()
        {
            if (_lobbyController != null)
            {
                _lobbyController.StartPollingLobbies();
            }
            else
            {
                // если сетка/лобби ещё не готовы — дождёмся
                StartCoroutine(WaitAndStartPolling());
            }
        }

        private IEnumerator WaitAndStartPolling()
        {
            // ждём появления NetworkManager
            while (InstanceFinder.NetworkManager == null)
                yield return null;

            TryResolveLobbyController();

            if (_lobbyController != null)
                _lobbyController.StartPollingLobbies();
            else
                Debug.LogError("[MainLoadBehaviour] LobbyController not found on NetworkManager.");
        }

        private void TryResolveLobbyController()
        {
            if (InstanceFinder.NetworkManager != null)
                _lobbyController = InstanceFinder.NetworkManager.GetComponent<LobbyController>();

#if UNITY_2023_1_OR_NEWER
            if (_lobbyController == null)
                _lobbyController = FindAnyObjectByType<LobbyController>();
#else
            if (_lobbyController == null)
                _lobbyController = FindObjectOfType<LobbyController>();
#endif
        }
    }
}