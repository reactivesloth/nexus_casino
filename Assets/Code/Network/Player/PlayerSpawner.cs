using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.API;
using Code.API.Models;
using Code.Network.Lobby;
using Code.Player;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Network.Player
{
    public struct PlayerTypeBroadcast : IBroadcast
    {
        public MeSchema PlayerData;
        public string PlayerType;
    }

    public struct DisconnectBroadcast : IBroadcast
    {
        public string Reason;
    }

    [Serializable]
    public class PlayerSpawnableModelKeyValuePair
    {
        public string key;
        public NetworkObject val;
    }

    /// <summary>
    /// Спавнит игрока при подключении клиента. Поддерживает выбор модели через Broadcast.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        /// <summary>Вызывается на сервере сразу после спавна игрока.</summary>
        public event Action<NetworkObject> OnSpawned;

        [Tooltip(
            "True to add player to the active scene when no global scenes are specified through the SceneManager.")]
        [SerializeField]
        private bool _addToDefaultScene = true;

        [Tooltip("Areas in which players may spawn.")]
        public Transform[] Spawns = Array.Empty<Transform>();

        [SerializeField] private List<PlayerSpawnableModelKeyValuePair> playerPrefabs = new();
        private readonly Dictionary<string, NetworkObject> _playerSpawnables = new(StringComparer.Ordinal);

        private NetworkManager _networkManager;
        private int _nextSpawn;

        private readonly List<NetworkConnection> _dontSpawn = new(8);
        private readonly Dictionary<NetworkConnection, string> _playerTypes = new();

        public static readonly Dictionary<NetworkConnection, MeSchema> SpawnedPlayerData_Server = new();
        public static readonly Dictionary<string, NetworkConnection> NameConnectionsData_Server = new();

        private void Awake()
        {
            // подготовим словарь модели → префаб (последний дубликат перезаписывает)
            for (int i = 0; i < playerPrefabs.Count; i++)
            {
                var kvp = playerPrefabs[i];
                if (kvp != null && !string.IsNullOrEmpty(kvp.key) && kvp.val != null)
                    _playerSpawnables[kvp.key] = kvp.val;
            }
        }

        private void OnEnable()
        {
            _networkManager = GetComponentInParent<NetworkManager>() ?? InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                NetworkManagerExtensions.LogWarning(
                    $"PlayerSpawner on {gameObject.name} cannot work as NetworkManager wasn't found on this object or within parent objects.");
                return;
            }

            // серверная подписка: принимаем тип игрока + спавн по загрузке стартовых сцен
            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.RegisterBroadcast<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived,
                    true);
                InstanceFinder.ClientManager.RegisterBroadcast<DisconnectBroadcast>(
                    OnClientDisconnectBroadcastReceived);

                _networkManager.SceneManager.OnClientLoadedStartScenes += OnClientLoadedStartScenes_Server;
                _networkManager.ServerManager.OnServerConnectionState += OnServerConnectionState;
                _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            }

            // клиентская подписка: отправим свой тип после установления соединения
            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
                return;

            if (InstanceFinder.ServerManager != null)
            {
                InstanceFinder.ServerManager.UnregisterBroadcast<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived);
                InstanceFinder.ClientManager.UnregisterBroadcast<DisconnectBroadcast>(
                    OnClientDisconnectBroadcastReceived);

                _networkManager.SceneManager.OnClientLoadedStartScenes -= OnClientLoadedStartScenes_Server;
                _networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionState;
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }

            if (InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }

        // === сервер: получили от клиента тип модели ===
        private void OnPlayerTypeBroadcastReceived(NetworkConnection conn, PlayerTypeBroadcast msg, Channel _)
        {
            if (conn == null)
                return;

            _playerTypes[conn] = msg.PlayerType;
            if (!NameConnectionsData_Server.TryAdd(msg.PlayerData.username, conn))
                NameConnectionsData_Server[msg.PlayerData.username] = conn;
            if (!SpawnedPlayerData_Server.TryAdd(conn, msg.PlayerData))
                SpawnedPlayerData_Server[conn] = msg.PlayerData;

            Debug.Log($"[Server] Получен тип модели '{msg.PlayerType}' от клиента {conn.ClientId}");
        }

        private void OnClientDisconnectBroadcastReceived(DisconnectBroadcast data, Channel _)
        {
            DisconnectLocalPlayer(data);
        }

        private async void DisconnectLocalPlayer(DisconnectBroadcast data)
        {
            await Task.Delay(3_500);
            LobbyDisconnector.Disconnect(true, data.Reason);
        }
        
        // === сервер: общий стейт сервера (очистим список запретов при стопе) ===
        private void OnServerConnectionState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                _dontSpawn.Clear();
                _playerTypes.Clear();
                SpawnedPlayerData_Server.Clear();
                NameConnectionsData_Server.Clear();
            }
        }

        // === сервер: конкретное соединение сменило стейт (почистим кэши) ===
        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped && conn != null)
            {
                _playerTypes.Remove(conn);

                var disconnectedUsername = SpawnedPlayerData_Server.TryGetValue(conn, out var usedData)
                    ? usedData.username
                    : null;
                if (disconnectedUsername != null)
                    NameConnectionsData_Server.Remove(disconnectedUsername);

                SpawnedPlayerData_Server.Remove(conn);
                for (int i = _dontSpawn.Count - 1; i >= 0; i--)
                    if (_dontSpawn[i] == conn)
                        _dontSpawn.RemoveAt(i);
            }
        }

        // === сервер: клиент загрузил стартовые сцены — пора спавнить ===
        private void OnClientLoadedStartScenes_Server(NetworkConnection conn, bool asServer)
        {
            if (!asServer || conn == null)
                return;

            // чёрный список на этот тик
            for (int i = 0; i < _dontSpawn.Count; i++)
                if (_dontSpawn[i] == conn)
                    return;

            // тип модели (по умолчанию "Male")
            _playerTypes.TryGetValue(conn, out string playerModelType);
            if (string.IsNullOrEmpty(playerModelType))
                playerModelType = "Male";

            _playerSpawnables.TryGetValue(playerModelType, out NetworkObject prefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(PlayerSpawner)}] Нет префаба для типа модели '{playerModelType}'");
                return;
            }

            // позиция/поворот
            SetSpawn(prefab.transform, out Vector3 position, out Quaternion rotation);

            // спавним из пула
            NetworkObject nob = _networkManager.GetPooledInstantiated(prefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(nob, conn);

            // если нет глобальных сцен — добавить во «вмолчальную» сцену
            if (_addToDefaultScene)
                _networkManager.SceneManager.AddOwnerToDefaultScene(nob);

            //Invoke(nameof(ClearDoubleConnections), 2f);
            ClearDoubleConnections();

            OnSpawned?.Invoke(nob);
        }

        private void ClearDoubleConnections()
        {
            foreach (var networkConnection in InstanceFinder.ServerManager.Clients.Values.Where(networkConnection =>
                         !NameConnectionsData_Server.ContainsValue(networkConnection)))
            {
                // networkConnection.Disconnect(true);
                var player = networkConnection.Objects.FirstOrDefault(o => o.GetComponent<PlayerMovementController>());
                if(player != null)
                    _networkManager.ServerManager.Despawn(player);
                InstanceFinder.ServerManager.Broadcast(networkConnection,
                    new DisconnectBroadcast { Reason = "You connect twice" });
            }
        }

        /// <summary>Определяет позицию/поворот спавна.</summary>
        private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            if (Spawns == null || Spawns.Length == 0)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
                return;
            }

            // берём точку по кругу
            Transform point = Spawns[_nextSpawn];
            if (point == null)
                SetSpawnUsingPrefab(prefab, out pos, out rot);
            else
            {
                pos = point.position;
                rot = point.rotation;
            }

            _nextSpawn++;
            if (_nextSpawn >= Spawns.Length)
                _nextSpawn = 0;
        }

        private static void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            pos = prefab != null ? prefab.position : Vector3.zero;
            rot = prefab != null ? prefab.rotation : Quaternion.identity;
        }

        /// <summary>Запретить спавн для соединения на ближайшее событие OnClientLoadedStartScenes.</summary>
        public void DontSpawnOnConnect(NetworkConnection conn)
        {
            if (conn == null) return;
            for (int i = 0; i < _dontSpawn.Count; i++)
                if (_dontSpawn[i] == conn)
                    return;
            _dontSpawn.Add(conn);
        }

        // === клиент: после старта соединения отправим выбранную модель ===
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            string playerModelType = PlayerPrefs.GetString("PlayerModelType", "Male");
            var msg = new PlayerTypeBroadcast
            {
                PlayerType = playerModelType, 
                PlayerData = ClientDataStorage.UserData
            };

            if (InstanceFinder.ClientManager != null)
            {
                InstanceFinder.ClientManager.Broadcast(msg);
            }

            Debug.Log($"[Client] Отправил Broadcast с моделью игрока '{playerModelType}'");
        }
    }
}