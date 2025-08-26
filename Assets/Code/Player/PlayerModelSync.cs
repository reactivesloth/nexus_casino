using System.Collections;
using CC;
using FishNet.Component.Animating;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterCustomization))]
    public class PlayerModelSync : NetworkBehaviour
    {
        [Header("Change detection")] [SerializeField, Tooltip("Включить контроль хеша JSON, чтобы не слать дубликаты.")]
        private bool useHashGuard = true;

        private CharacterCustomization _characterCustomization;
        private NetworkAnimator _networkAnimator;

        private readonly SyncVar<string> _characterJson = new(new SyncTypeSettings
        {
            ReadPermission = ReadPermission.Observers,
            WritePermission = WritePermission.ServerOnly
        });

        private string _lastSentJson;
        private int _lastSentHash;

        private bool _initedPlayerModelSync;

        private void EnsureInit()
        {
            if (_initedPlayerModelSync) return;
            _initedPlayerModelSync = true;

            if (_characterCustomization == null)
                _characterCustomization = GetComponent<CharacterCustomization>();

            if (_networkAnimator == null)
                _networkAnimator = GetComponent<NetworkAnimator>();

            if (_characterCustomization != null)
            {
                _characterCustomization.Autoload = false;
                _characterCustomization.Initialize();
            }
        }

        private void Awake()
        {
            EnsureInit();
            _characterJson.OnChange += OnCharacterJsonChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += OnConnectionState;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ServerManager.OnRemoteConnectionState -= OnConnectionState;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureInit();

            ApplyCharacterJsonImmediate(_characterJson.Value);

            if (IsOwner)
                StartCoroutine(WaitAndCommitLocalCharacter());
        }

        private void OnDestroy()
        {
            _characterJson.OnChange -= OnCharacterJsonChanged;
        }

        private void OnConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                if (_networkAnimator != null)
                    _networkAnimator.SendAll();
            }
        }

        private void OnCharacterJsonChanged(string prev, string next, bool asServer)
        {
            Debug.Log($"[Client] Получен JSON ({(next != null ? next.Length : 0)} симв.)");
            EnsureInit();
            ApplyCharacterJsonImmediate(next);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
                StartCoroutine(WaitAndCommitLocalCharacter());
        }

        private IEnumerator WaitAndCommitLocalCharacter()
        {
            while (!IsClientInitialized || !IsClientStarted || !IsSpawned)
                yield return null;
            yield return null;

            CommitLocalCharacter();
        }
        
        public void CommitLocalCharacter()
        {
            if (!IsOwner) return;

            EnsureInit();
            string json = _characterCustomization != null ? _characterCustomization.GetJSON() : string.Empty;

            if (useHashGuard)
            {
                int hash = json != null ? json.GetHashCode() : 0;
                if (_lastSentJson == json && _lastSentHash == hash)
                {
                    return;
                }

                _lastSentJson = json;
                _lastSentHash = hash;
            }

            SendCharacterJsonServerRpc(json);
        }

        [ServerRpc(RunLocally = true)]
        private void SendCharacterJsonServerRpc(string json)
        {
            Debug.Log($"[Server] Получен JSON ({(json != null ? json.Length : 0)} симв.)");
            _characterJson.Value = json ?? string.Empty;
        }

        private void ApplyCharacterJsonImmediate(string json)
        {
            if (_characterCustomization == null)
                _characterCustomization = GetComponent<CharacterCustomization>();
            if (_characterCustomization == null) return;

            _characterCustomization.Autoload = false;
            _characterCustomization.Initialize();

            if (!string.IsNullOrEmpty(json))
                _characterCustomization.LoadFromJSON(json);
        }
    }
}