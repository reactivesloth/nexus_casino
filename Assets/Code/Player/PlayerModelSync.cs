using CC;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterCustomization))]
    public class PlayerModelSync : NetworkBehaviour
    {
        private CharacterCustomization _characterCustomization;

        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            Invoke(nameof(TransmitLocalCharacter), 1f);
        }

        [ServerRpc]
        public void SendCharacterJsonServerRpc(string json, NetworkConnection sender = null)
        {
            Debug.Log($"[Server] Получен JSON ({json.Length} симв.) от {sender.ClientId}");
            SendCharacterJsonObserversRpc(json);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterJsonObserversRpc(string json)
        {
            Debug.Log($"[Client] Получил JSON ({json.Length} симв.)");
            _characterCustomization.Initialize();
            _characterCustomization.LoadFromJSON(json);
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return; 
            
            Debug.Log($"[Client] TransmitLocalCharacter");
            string json = _characterCustomization.GetJSON();
            SendCharacterJsonServerRpc(json);
        }
    }
}