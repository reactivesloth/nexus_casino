using System;
using System.Collections;
using System.Linq;
using FishNet;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network
{
    public class HostMigrator : MonoBehaviour
    {
        
        [SerializeField] private float reconnectDelay = 5f;
        [SerializeField] private int maxReconnectAttempts = 1;

        private int _currentAttempts;
        
        private void Awake()
        {
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionChanged;
        }

        private void OnDestroy()
        {
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionChanged;
        }

        private void OnClientConnectionChanged(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                if (args.ConnectionState == LocalConnectionState.Stopped)
                {
                    Debug.Log("Соединение потеряно. Начинаю переподключение...");
                    _currentAttempts = 0;
                    StartCoroutine(TryReconnect());
                }
            }
        }
        
        private IEnumerator TryReconnect()
        {
            while (_currentAttempts < maxReconnectAttempts)
            {
                _currentAttempts++;
                Debug.Log($"Попытка переподключения {_currentAttempts}/{maxReconnectAttempts}");

                InstanceFinder.ClientManager.StartConnection();

                // Ждём reconnectDelay секунд перед следующей попыткой
                yield return new WaitForSeconds(reconnectDelay);

                if (InstanceFinder.ClientManager.Connection.IsActive)
                {
                    Debug.Log("Переподключение успешно.");
                    yield break;
                }
            }

            Debug.LogWarning("Не удалось переподключиться.");
            OnReconnectFailed();
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnReconnectFailed()
        {
            // Надо понять становится ли данный клиент хостом.
            var minConnectionIndex = InstanceFinder.ClientManager.Clients.Min(pair => pair.Key);
            var isNewHost = minConnectionIndex == InstanceFinder.ClientManager.Connection.ClientId;
            Debug.Log(isNewHost);

            // Если становится, то надо отправить на лобби изменения HOST_ID_S, запустить у себя сервер

            // Если не становится, то дождаться изменения хоста на лобби и подключится к новому хосту
        }

        private void CreateServer()
        {
            
        }
    }
}