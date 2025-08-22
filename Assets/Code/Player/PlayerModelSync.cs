using System;
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
        private CharacterCustomization _characterCustomization;
        private NetworkAnimator _networkAnimator;

        private readonly SyncVar<string> _characterJson = new(new SyncTypeSettings
        {
            ReadPermission = ReadPermission.Observers,
            WritePermission = WritePermission.ServerOnly
        });
        
        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
            _networkAnimator = GetComponent<NetworkAnimator>();
            _characterJson.OnChange += OnCharacterJsonChanged;
            _characterCustomization.Initialize();
        }

        private void Start()
        {
            ServerManager.OnRemoteConnectionState += OnConnectionState;
        }

        private void OnDestroy()
        {
            _characterJson.OnChange -= OnCharacterJsonChanged;
            
            
        }

        private void OnConnectionState(NetworkConnection arg1, RemoteConnectionStateArgs arg2)
        {
            if(arg2.ConnectionState == RemoteConnectionState.Started)
                _networkAnimator.SendAll();
        }
        
        private void OnCharacterJsonChanged(string prev, string next, bool asServer)
        {
            Debug.Log($"[Client] Получил JSON ({(next != null ? next.Length : 0)} симв.)");

            _characterCustomization.Autoload = false;
            _characterCustomization.Initialize();
            if (!string.IsNullOrEmpty(next))
                _characterCustomization.LoadFromJSON(next);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner) StartCoroutine(WaitAndSendLocalCharacter());
        }

        [ServerRpc(RunLocally = true)]
        public void SendCharacterJsonServerRpc(string json)
        {
            Debug.Log($"[Server] Получен JSON ({(json != null ? json.Length : 0)} симв.)");
            _characterJson.Value = json ?? string.Empty;
        }

        private IEnumerator WaitAndSendLocalCharacter()
        {
            while (!IsClientInitialized || !IsClientStarted || !IsSpawned)
                yield return null;
            yield return null;
            TransmitLocalCharacter();
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return;
            Debug.Log("[Client] TransmitLocalCharacter");
            string json = _characterCustomization.GetJSON();
            SendCharacterJsonServerRpc(json);
        }
    }
}
