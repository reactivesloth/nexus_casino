using System;
using CC;
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

        private readonly SyncVar<string> _characterCustomizationJson = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ClientUnsynchronized,
            ReadPermission = ReadPermission.Observers,
            Channel = Channel.Reliable
        });

        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
        }

        private void OnEnable()
        {
            _characterCustomizationJson.OnChange += CharacterCustomizationJsonOnOnChange;
        }

        private void OnDisable()
        {
            _characterCustomizationJson.OnChange -= CharacterCustomizationJsonOnOnChange;
        }
        
        [ServerRpc]
        public void SendCharacterJsonServerRpc(string json, NetworkConnection sender = null)
        {
            // на сервере логируем и ретранслируем всем остальным
            Debug.Log($"[Server] Получен JSON ({json.Length} симв.) от {sender.ClientId}");
            SendCharacterJsonObserversRpc(json);
        }

        [ObserversRpc(BufferLast = true)]
        private void SendCharacterJsonObserversRpc(string json)
        {
            Debug.Log($"[Client] Получил JSON ({json.Length} симв.)");
            // восстанавливаем из JSON
            _characterCustomization.LoadFromJSON(json);
        }

        public void TransmitLocalCharacter()
        {
            if (!IsOwner) return; 
            
            string json = _characterCustomization.GetJSON();
            SendCharacterJsonServerRpc(json);
        }
        
        private void CharacterCustomizationJsonOnOnChange(string prev, string next, bool asServer)
        {
            _characterCustomization.LoadFromJSON(next);
        }
    }
}