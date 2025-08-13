using System.Collections;
using CC;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterCustomization))]
    public class PlayerModelSync : NetworkBehaviour
    {
        private CharacterCustomization _characterCustomization;

        private readonly SyncVar<string> _characterJson = new(new SyncTypeSettings
        {
            ReadPermission = ReadPermission.Observers,
            WritePermission = WritePermission.ServerOnly
        });
        
        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
            _characterJson.OnChange += OnCharacterJsonChanged;
            _characterCustomization.Initialize();
        }

        private void OnDestroy()
        {
            _characterJson.OnChange -= OnCharacterJsonChanged;
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
