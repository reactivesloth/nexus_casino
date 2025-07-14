using System;
using System.Collections.Generic;
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
        public string PlayerType;
    }
    
    [Serializable]
    public class PlayerSpawnableModelKeyValuePair {
        public string key;
        public NetworkObject val;
    }
    
    /// <summary>
    /// Spawns a player object for clients when they connect.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        #region Public.
        /// <summary>
        /// Called on the server when a player is spawned.
        /// </summary>
        public event Action<NetworkObject> OnSpawned;
        #endregion

        #region Serialized.

        /// <summary>
        /// True to add player to the active scene when no global scenes are specified through the SceneManager.
        /// </summary>
        [Tooltip("True to add player to the active scene when no global scenes are specified through the SceneManager.")]
        [SerializeField]
        private bool _addToDefaultScene = true;
        /// <summary>
        /// Areas in which players may spawn.
        /// </summary>
        [Tooltip("Areas in which players may spawn.")]
        public Transform[] Spawns = new Transform[0];
        #endregion

        
        [SerializeField] private List<PlayerSpawnableModelKeyValuePair> playerPrefabs = new List<PlayerSpawnableModelKeyValuePair>();
        Dictionary<string, NetworkObject> playerSpawnables = new Dictionary<string, NetworkObject>();

        void Awake() {
            foreach (var kvp in playerPrefabs) {
                playerSpawnables[kvp.key] = kvp.val;
            }
        }
        
        #region Private.
        /// <summary>
        /// First instance of the NetworkManager found. This will be either the NetworkManager on or above this object, or InstanceFinder.NetworkManager.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// Next spawns to use.
        /// </summary>
        private int _nextSpawn;
        
        private List<NetworkConnection> _dontSpawn = new();
        private readonly Dictionary<NetworkConnection, string> _playerTypes = new();

        #endregion

        private void OnEnable()
        {
            _networkManager = GetComponentInParent<NetworkManager>();
            if (_networkManager == null)
                _networkManager = InstanceFinder.NetworkManager;
            
            if (_networkManager == null)
            {
                NetworkManagerExtensions.LogWarning($"PlayerSpawner on {gameObject.name} cannot work as NetworkManager wasn't found on this object or within parent objects.");
                return;
            }

            InstanceFinder.ServerManager.RegisterBroadcast<PlayerTypeBroadcast>(OnGenderBroadcastReceived, true);
            _networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState += ServerManagerOnOnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
                return;
            
            InstanceFinder.ServerManager.UnregisterBroadcast<PlayerTypeBroadcast>(OnGenderBroadcastReceived);
            _networkManager.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState -= ServerManagerOnOnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
        
        private void OnGenderBroadcastReceived(NetworkConnection conn, PlayerTypeBroadcast msg, Channel channel)
        {
            _playerTypes[conn] = msg.PlayerType;
            Debug.Log($"[Server] Получен пол '{msg.PlayerType}' от клиента {conn.ClientId}");
        }
        
        private void ServerManagerOnOnServerConnectionState(ServerConnectionStateArgs obj)
        {
            if(obj.ConnectionState == LocalConnectionState.Stopped)
                _dontSpawn.Clear();
        }

        /// <summary>
        /// Called when a client loads initial scenes after connecting.
        /// </summary>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (!asServer)
                return;
            if(_dontSpawn.Contains(conn))
                return;
            
            _playerTypes.TryGetValue(conn, out string playerModelType);
            playerModelType = string.IsNullOrEmpty(playerModelType) ? "Male" : playerModelType;

            playerSpawnables.TryGetValue(playerModelType, out NetworkObject prefab);
            
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(PlayerSpawner)}] Нет префаба для типа модели '{playerModelType}'");
                return;
            }

            Vector3 position;
            Quaternion rotation;
            SetSpawn(prefab.transform, out position, out rotation);

            NetworkObject nob = _networkManager.GetPooledInstantiated(prefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(nob, conn);

            //If there are no global scenes 
            if (_addToDefaultScene)
                _networkManager.SceneManager.AddOwnerToDefaultScene(nob);

            OnSpawned?.Invoke(nob);
        }

        /// <summary>
        /// Sets a spawn position and rotation.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        private void SetSpawn(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            //No spawns specified.
            if (Spawns.Length == 0)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
                return;
            }

            Transform result = Spawns[_nextSpawn];
            if (result == null)
            {
                SetSpawnUsingPrefab(prefab, out pos, out rot);
            }
            else
            {
                pos = result.position;
                rot = result.rotation;
            }

            //Increase next spawn and reset if needed.
            _nextSpawn++;
            if (_nextSpawn >= Spawns.Length)
                _nextSpawn = 0;
        }

        /// <summary>
        /// Sets spawn using values from prefab.
        /// </summary>
        /// <param name="prefab"></param>
        /// <param name="pos"></param>
        /// <param name="rot"></param>
        private void SetSpawnUsingPrefab(Transform prefab, out Vector3 pos, out Quaternion rot)
        {
            pos = prefab.position;
            rot = prefab.rotation;
        }

        public void DontSpawnOnConnect(NetworkConnection conn)
        {
            Debug.Log($"[DontSpawnOnConnect] {conn}");
            _dontSpawn.Add(conn);
        }
        
        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            // читаем выбор из PlayerPrefs (или откуда угодно)
            string playerModelType = PlayerPrefs.GetString("PlayerModelType", "Male");

            // шлём Broadcast на сервер
            var msg = new PlayerTypeBroadcast { PlayerType = playerModelType };
            InstanceFinder.ClientManager.Broadcast(msg);

            Debug.Log($"[Client] Отправил Broadcast с моделью игрока '{playerModelType}'");
        }
    }
}