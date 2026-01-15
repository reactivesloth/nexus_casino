using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.API;
using PurrNet;
using PurrNet.Logging;
using PurrNet.Modules;
using PurrNet.Packing;
using PurrNet.Transports;
using UnityEngine;

namespace Code.Network.Player
{
    public struct PlayerTypeBroadcast : IPackedAuto
    {
        public MeSchema PlayerData;
        public string PlayerType;
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

    /// <summary>
    /// Гибридный спавнер: база от PurrNet + твоя логика выбора персонажей и защиты.
    /// </summary>
    public sealed class PlayerSpawner : PurrMonoBehaviour
    {
        public event Action<NetworkIdentity> OnSpawned;

        [Header("PurrNet Settings")]
        [SerializeField] private bool _ignoreNetworkRules;
        [SerializeField] private List<Transform> spawnPoints = new();
        
        [Header("Custom Logic Settings")]
        [SerializeField] private List<PlayerSpawnableModelKeyValuePair> playerPrefabs = new();
        [Tooltip("True to add player to the active scene when no global scenes are specified.")]
        [SerializeField] private bool _addToDefaultScene = true;

        private int _currentSpawnPoint;
        private IProvideSpawnPoints _spawnPointProvider;
        private IProvidePrefabInstantiated _prefabInstantiatedProvider;

        private readonly Dictionary<string, NetworkIdentity> _playerSpawnables = new(StringComparer.Ordinal);
        private readonly List<PlayerID> _dontSpawn = new(8);
        private readonly Dictionary<PlayerID, string> _playerTypes = new();
        private readonly HashSet<PlayerID> _sceneLoadedPlayers = new();

        public static readonly Dictionary<PlayerID, MeSchema> SpawnedPlayerData_Server = new();
        public static readonly Dictionary<string, PlayerID> NameConnectionsData_Server = new();

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
            if (asServer && manager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
            {
                scenePlayersModule.onPlayerLoadedScene += OnPlayerLoadedScene_Wrapper;

                manager.Subscribe<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived, true);
                manager.Subscribe<DisconnectBroadcast>(OnClientDisconnectBroadcastReceived, true);
                manager.onPlayerLeft += OnPlayerLeft_Server;

                if (manager.TryGetModule(out ScenesModule scenes, true) &&
                    scenes.TryGetSceneID(gameObject.scene, out var sceneID) &&
                    scenePlayersModule.TryGetPlayersInScene(sceneID, out var players))
                {
                    foreach (var player in players)
                        OnPlayerLoadedScene_Wrapper(player, sceneID, true);
                }
            }
            else
            {
                manager.onClientConnectionState += OnClientConnectionState;
            }
        }

        public override void Unsubscribe(NetworkManager manager, bool asServer)
        {
            if (asServer && manager.TryGetModule(out ScenePlayersModule scenePlayersModule, true))
            {
                scenePlayersModule.onPlayerLoadedScene -= OnPlayerLoadedScene_Wrapper;
            }

            if (asServer)
            {
                manager.Unsubscribe<PlayerTypeBroadcast>(OnPlayerTypeBroadcastReceived, true);
                manager.Unsubscribe<DisconnectBroadcast>(OnClientDisconnectBroadcastReceived, true);
                manager.onPlayerLeft -= OnPlayerLeft_Server;
            }
            else
            {
                manager.onClientConnectionState -= OnClientConnectionState;
            }
        }

        /// <summary>
        /// Обертка над событием загрузки сцены PurrNet.
        /// Вместо мгновенного спавна мы просто помечаем "Сцена готова" и пробуем спавнить.
        /// </summary>
        private void OnPlayerLoadedScene_Wrapper(PlayerID player, SceneID scene, bool asServer)
        {
            if (!asServer) return;

            if (!NetworkManager.main.TryGetModule(out ScenesModule scenesModule, true)) return;
            if (!scenesModule.TryGetSceneID(gameObject.scene, out var mySceneID)) return;
            if (mySceneID != scene) return;

            _sceneLoadedPlayers.Add(player);

            TrySpawnPlayer(player, scene);
        }

        private void OnPlayerTypeBroadcastReceived(PlayerID sender, PlayerTypeBroadcast msg, bool asServer)
        {
            _playerTypes[sender] = msg.PlayerType;
            
            if (!NameConnectionsData_Server.TryAdd(msg.PlayerData.username, sender))
                NameConnectionsData_Server[msg.PlayerData.username] = sender;
            
            if (!SpawnedPlayerData_Server.TryAdd(sender, msg.PlayerData))
                SpawnedPlayerData_Server[sender] = msg.PlayerData;

            Debug.Log($"[Server] Player Type '{msg.PlayerType}' received from {sender}");

            if (NetworkManager.main.TryGetModule(out ScenesModule scenes, true) &&
                scenes.TryGetSceneID(gameObject.scene, out var sceneID))
            {
                TrySpawnPlayer(sender, sceneID);
            }
        }

        private void TrySpawnPlayer(PlayerID player, SceneID scene)
        {
            if (_dontSpawn.Contains(player)) return;

            if (!_playerTypes.ContainsKey(player)) return;
            if (!_sceneLoadedPlayers.Contains(player)) return;

            var main = NetworkManager.main;
            bool isDestroyOnDisconnectEnabled = main.networkRules.ShouldDespawnOnOwnerDisconnect();
            
            if (!_ignoreNetworkRules && !isDestroyOnDisconnectEnabled && 
                main.TryGetModule(out GlobalOwnershipModule ownership, true) &&
                ownership.PlayerOwnsSomething(player))
            {
                return;
            }

            string type = _playerTypes[player];
            if (!_playerSpawnables.TryGetValue(type, out NetworkIdentity prefabToSpawn))
            {
                if (!_playerSpawnables.TryGetValue("Male", out prefabToSpawn))
                {
                    Debug.LogWarning($"No prefab found for type '{type}' and no 'Male' fallback.");
                    return;
                }
            }

            Vector3 spawnPos;
            Quaternion spawnRot;
            CleanupSpawnPoints();

            if (_spawnPointProvider != null)
            {
                var p = _spawnPointProvider.NextSpawnPoint(player, scene);
                spawnPos = p.position;
                spawnRot = p.rotation;
            }
            else if (spawnPoints.Count > 0)
            {
                var t = spawnPoints[_currentSpawnPoint];
                _currentSpawnPoint = (_currentSpawnPoint + 1) % spawnPoints.Count;
                spawnPos = t.position;
                spawnRot = t.rotation;
            }
            else
            {
                spawnPos = transform.position;
                spawnRot = transform.rotation;
            }

            var newPlayerGO = UnityProxy.Instantiate(prefabToSpawn.gameObject, spawnPos, spawnRot, gameObject.scene);
            
            if (newPlayerGO.TryGetComponent(out NetworkIdentity id))
            {
                id.GiveOwnership(player);
                OnSpawned?.Invoke(id);
            }

            _prefabInstantiatedProvider?.OnPrefabInstantiated(newPlayerGO, player, scene);

            ClearDoubleConnections();
        }

        private void OnPlayerLeft_Server(PlayerID player, bool asServer)
        {
            _playerTypes.Remove(player);
            _sceneLoadedPlayers.Remove(player);
            SpawnedPlayerData_Server.Remove(player);

            var keysToRemove = NameConnectionsData_Server.Where(kvp => kvp.Value == player).Select(kvp => kvp.Key).ToList();
            foreach (var key in keysToRemove) NameConnectionsData_Server.Remove(key);

            _dontSpawn.Remove(player);
        }

        private void OnClientDisconnectBroadcastReceived(PlayerID player, DisconnectBroadcast data, bool asServer)
        {
        }

        private void ClearDoubleConnections()
        {
            var duplicateNames = NameConnectionsData_Server
                .GroupBy(x => x.Key)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            foreach (var name in duplicateNames)
            {
            }
        }
        
        public void DontSpawnOnConnect(PlayerID player)
        {
            if (!_dontSpawn.Contains(player))
                _dontSpawn.Add(player);
        }

        private void OnClientConnectionState(ConnectionState state)
        {
            if (state == ConnectionState.Connected)
            {
                SendPlayerSettings();
            }
        }

        private async void SendPlayerSettings()
        {
            await Task.Delay(100);
            string type = PlayerPrefs.GetString("PlayerModelType", "Male");
            
            var msg = new PlayerTypeBroadcast
            {
                PlayerType = type,
                PlayerData = ClientDataStorage.UserData
            };

            NetworkManager.main.SendToServer(msg);
            Debug.Log($"[Client] Sent PlayerType '{type}'");
        }

        public void SetRespawnPointProvider(IProvideSpawnPoints provider) => _spawnPointProvider = provider;
        public void ResetSpawnPointProvider() => _spawnPointProvider = null;
        public void SetPrefabInstantiatedProvider(IProvidePrefabInstantiated provider) => _prefabInstantiatedProvider = provider;
        public void ResetPrefabInstantiatedProvider() => _prefabInstantiatedProvider = null;

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
