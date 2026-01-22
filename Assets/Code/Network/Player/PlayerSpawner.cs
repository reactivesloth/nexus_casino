using System;
using System.Collections.Generic;
using System.Linq;
using Code.API;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Code.Network.Player
{
    public struct PlayerTypeBroadcast : IPackedAuto
    {
        public MeSchema PlayerData;
        public string PlayerType;

        // Spawn pos data
        public bool IsSpawnOnSavePos;
        public Vector3 SavePos;
    }

    public struct DisconnectBroadcast : IPackedAuto
    {
        public string Reason;
    }

    [Serializable]
    public class PlayerSpawnableModelKeyValuePair
    {
        public string key;
        public NetworkIdentity val;
    }

    public interface IProvideSpawnPoints
    {
        SpawnPoint NextSpawnPoint(PlayerID player, SceneID scene);
    }

    public interface IProvidePrefabInstantiated
    {
        void OnPrefabInstantiated(GameObject prefabInstance, PlayerID player, SceneID scene);
    }

    public struct SpawnPoint
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    public class PlayerSpawner : PurrMonoBehaviour
    {
        public event Action<NetworkIdentity> OnSpawned;

        [Header("PurrNet Settings")] [SerializeField]
        private bool _ignoreNetworkRules;

        [SerializeField] private List<Transform> spawnPoints = new();

        [Header("Custom Logic Settings")] [SerializeField]
        private List<PlayerSpawnableModelKeyValuePair> playerPrefabs = new();

        [Tooltip("True to add player to the active scene when no global scenes are specified.")] [SerializeField]
        private bool _addToDefaultScene = true;

        [Tooltip("Сцена, в которой спавнить игрока. Оставь пустым, чтобы спавнить в сцене объекта со спавнером.")]
        [SerializeField]
        private string _spawnSceneName = "Main";

        private int _currentSpawnPoint;
        private IProvideSpawnPoints _spawnPointProvider;
        private IProvidePrefabInstantiated _prefabInstantiatedProvider;

        private readonly Dictionary<string, NetworkIdentity> _playerSpawnables = new(StringComparer.Ordinal);
        private readonly HashSet<PlayerID> _dontSpawn = new();
        private readonly Dictionary<PlayerID, string> _playerTypes = new();
        private readonly HashSet<PlayerID> _sceneLoadedPlayers = new();
        private readonly HashSet<PlayerID> _spawned = new(); // защита от дубля

        public static readonly Dictionary<PlayerID, MeSchema> SpawnedPlayerData_Server = new();
        public static readonly Dictionary<string, PlayerID> NameConnectionsData_Server = new();

        // ===== Client state =====
        private bool _clientConnected;

        private void Awake()
        {
            CleanupSpawnPoints();

            foreach (var kvp in playerPrefabs)
            {
                if (kvp != null && !string.IsNullOrEmpty(kvp.key) && kvp.val != null)
                    _playerSpawnables[kvp.key] = kvp.val;
            }
        }

        public override void Subscribe(NetworkManager manager, bool asServer)
        {
            if (asServer)
            {
                // Сервер получает выбор модели (и именно это будет триггером на спавн).
                manager.Subscribe<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived_Server, true);
                manager.Subscribe<DisconnectBroadcast>(OnClientDisconnectBroadcastReceived_Server, true);

                manager.onPlayerLeft += OnPlayerLeft_Server;
            }
            else
            {
                manager.onClientConnectionState += OnClientConnectionState_Client;
            }
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            if (asServer)
            {
                manager.Unsubscribe<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived_Server, true);
                manager.Unsubscribe<DisconnectBroadcast>(OnClientDisconnectBroadcastReceived_Server, true);

                manager.onPlayerLeft -= OnPlayerLeft_Server;
            }
            else
            {
                manager.onClientConnectionState -= OnClientConnectionState_Client;
            }
        }

        // =========================
        // CLIENT API
        // =========================

        /// <summary>
        /// Вызывай этот метод, когда у тебя завершилась загрузка/инициализация/авторизация.
        /// Именно здесь клиент отправляет на сервер данные для спавна.
        /// </summary>
        public void SpawnPlayer()
        {
            if (!InstanceHandler.NetworkManager.isClient)
            {
                Debug.LogWarning($"[{nameof(PlayerSpawner)}] SpawnPlayer() called not on client.");
                return;
            }

            if (!_clientConnected)
            {
                Debug.LogWarning($"[{nameof(PlayerSpawner)}] SpawnPlayer() called before ConnectionState.Connected.");
                return;
            }

            string type = PlayerPrefs.GetString("PlayerModelType", "Male");

            var msg = new PlayerTypeBroadcast
            {
                PlayerType = type,
                PlayerData = ClientDataStorage.UserData,
                IsSpawnOnSavePos = PlayerPrefs.HasKey("SavedSpawnPosition"),
                SavePos = transform.position = new Vector3(
                    PlayerPrefs.GetFloat("SavedSpawnPositionX"),
                    PlayerPrefs.GetFloat("SavedSpawnPositionY"),
                    PlayerPrefs.GetFloat("SavedSpawnPositionZ")
                )
            };

            if (msg.IsSpawnOnSavePos)
                Debug.Log($"[{nameof(PlayerSpawner)}] SpawnPlayer on Save Position: {msg.SavePos}");

            // Клиент -> Сервер (broadcast-сообщение без привязки к объекту)
            NetworkManager.main.SendToServer(msg);
        }

        private void OnClientConnectionState_Client(ConnectionState state)
        {
            _clientConnected = (state == ConnectionState.Connected);
        }

        // =========================
        // SERVER SIDE
        // =========================

        private void OnPlayerTypeBroadcastReceived_Server(PlayerID sender, PlayerTypeBroadcast msg, bool asServer)
        {
            _playerTypes[sender] = msg.PlayerType;

            if (!NameConnectionsData_Server.TryAdd(msg.PlayerData.username, sender))
                NameConnectionsData_Server[msg.PlayerData.username] = sender;

            if (!SpawnedPlayerData_Server.TryAdd(sender, msg.PlayerData))
                SpawnedPlayerData_Server[sender] = msg.PlayerData;

            if (!TryGetSpawnSceneID(out var sceneId))
                return;

            TrySpawnPlayer_Server(sender, sceneId, msg);
        }

        private void TrySpawnPlayer_Server(PlayerID player, SceneID scene, PlayerTypeBroadcast msg)
        {
            if (!InstanceHandler.NetworkManager.isServer) return;
            if (_dontSpawn.Contains(player)) return;
            if (_spawned.Contains(player)) return;

            if (!_playerTypes.ContainsKey(player)) return;

            var main = NetworkManager.main;

            bool destroyOnDisconnect = main.networkRules.ShouldDespawnOnOwnerDisconnect();
            if (!_ignoreNetworkRules && !destroyOnDisconnect &&
                main.TryGetModule(out GlobalOwnershipModule ownership, true) &&
                ownership.PlayerOwnsSomething(player))
            {
                return;
            }

            string type = _playerTypes[player];
            if (!_playerSpawnables.TryGetValue(type, out var prefabToSpawn))
            {
                if (!_playerSpawnables.TryGetValue("Male", out prefabToSpawn))
                {
                    Debug.LogWarning($"[{nameof(PlayerSpawner)}] No prefab for type '{type}', and no 'Male' fallback.");
                    return;
                }
            }

            GetSpawnTransform(out var pos, out var rot, player, scene, prefabToSpawn.transform);

            var unityScene = ResolveSpawnScene();
            var newPlayerGO = UnityProxy.Instantiate(prefabToSpawn.gameObject, msg.IsSpawnOnSavePos ? msg.SavePos : pos,
                rot, unityScene);

            if (newPlayerGO.TryGetComponent(out NetworkIdentity id))
            {
                id.GiveOwnership(player, false, true);
                _spawned.Add(player);
                OnSpawned?.Invoke(id);
            }

            _prefabInstantiatedProvider?.OnPrefabInstantiated(newPlayerGO, player, scene);
        }

        private void OnPlayerLeft_Server(PlayerID player, bool asServer)
        {
            _playerTypes.Remove(player);
            _sceneLoadedPlayers.Remove(player);
            _spawned.Remove(player);
            SpawnedPlayerData_Server.Remove(player);

            var keysToRemove = NameConnectionsData_Server
                .Where(kvp => kvp.Value == player)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
                NameConnectionsData_Server.Remove(key);

            _dontSpawn.Remove(player);
        }

        private void OnClientDisconnectBroadcastReceived_Server(PlayerID player, DisconnectBroadcast data, bool asServer)
        {
            
        }

        private void GetSpawnTransform(out Vector3 pos, out Quaternion rot, PlayerID player, SceneID scene,
            Transform prefab)
        {
            CleanupSpawnPoints();

            if (_spawnPointProvider != null)
            {
                var p = _spawnPointProvider.NextSpawnPoint(player, scene);
                pos = p.position;
                rot = p.rotation;
                return;
            }

            if (spawnPoints.Count > 0)
            {
                var t = spawnPoints[_currentSpawnPoint];
                _currentSpawnPoint = (_currentSpawnPoint + 1) % spawnPoints.Count;
                pos = t.position;
                rot = t.rotation;
                return;
            }

            pos = prefab != null ? prefab.position : transform.position;
            rot = prefab != null ? prefab.rotation : transform.rotation;
        }

        private bool TryGetSpawnSceneID(out SceneID sceneId)
        {
            sceneId = default;

            if (!NetworkManager.main || !NetworkManager.main.TryGetModule(out ScenesModule scenes, true))
                return false;

            var unityScene = ResolveSpawnScene();
            return scenes.TryGetSceneID(unityScene, out sceneId);
        }

        private UnityEngine.SceneManagement.Scene ResolveSpawnScene()
        {
            if (!string.IsNullOrEmpty(_spawnSceneName))
            {
                var s = SceneManager.GetSceneByName(_spawnSceneName);
                if (s.IsValid() && s.isLoaded)
                    return s;
            }

            return gameObject.scene;
        }

        private void CleanupSpawnPoints()
        {
            bool hadNull = false;
            for (int i = 0; i < spawnPoints.Count; i++)
            {
                if (!spawnPoints[i])
                {
                    hadNull = true;
                    spawnPoints.RemoveAt(i--);
                }
            }

            if (hadNull) PurrLogger.LogWarning("Invalid spawn points cleanup.", this);
        }
    }
}