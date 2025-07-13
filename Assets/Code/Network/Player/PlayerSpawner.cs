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
    public struct GenderBroadcast : IBroadcast
    {
        public string Gender;
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
        [Header("Player Prefabs")]
        [Tooltip("Male player prefab")]
        [SerializeField] private NetworkObject malePrefab;
        [Tooltip("Female player prefab")]
        [SerializeField] private NetworkObject femalePrefab;

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
        private readonly Dictionary<NetworkConnection, string> _genders = new();

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

            InstanceFinder.ServerManager.RegisterBroadcast<GenderBroadcast>(OnGenderBroadcastReceived, true);
            _networkManager.SceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState += ServerManagerOnOnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionState;
        }

        private void OnDisable()
        {
            if (_networkManager == null)
                return;
            
            InstanceFinder.ServerManager.UnregisterBroadcast<GenderBroadcast>(OnGenderBroadcastReceived);
            _networkManager.SceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
            _networkManager.ServerManager.OnServerConnectionState -= ServerManagerOnOnServerConnectionState;
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionState;
        }
        
        private void OnGenderBroadcastReceived(NetworkConnection conn, GenderBroadcast msg, Channel channel)
        {
            _genders[conn] = msg.Gender;
            Debug.Log($"[Server] Получен пол '{msg.Gender}' от клиента {conn.ClientId}");
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
            
            _genders.TryGetValue(conn, out string gender);
            gender = string.IsNullOrEmpty(gender) ? "Male" : gender;
            
            NetworkObject prefab = gender == "Female" ? femalePrefab : malePrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[{nameof(PlayerSpawner)}] Нет префаба для пола '{gender}'");
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
            string gender = PlayerPrefs.GetString("PlayerGender", "Female");

            // шлём Broadcast на сервер
            var msg = new GenderBroadcast { Gender = gender };
            InstanceFinder.ClientManager.Broadcast(msg);

            Debug.Log($"[Client] Отправил Broadcast с полом '{gender}'");
        }
    }
}