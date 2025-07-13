using System;
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


        /// <summary>
        /// Клиент → сервер: шлёт свой JSON
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void SendCharacterJsonServerRpc(string json, NetworkConnection sender = null)
        {
            // на сервере логируем и ретранслируем всем остальным
            Debug.Log($"[Server] Получен JSON ({json.Length} симв.) от {sender.ClientId}");
            SendCharacterJsonObserversRpc(json);
        }

        /// <summary>
        /// Сервер → все клиенты (ObserversRpc автоматически шлёт всем, у кого есть этот NetworkBehaviour)
        /// </summary>
        [ObserversRpc(BufferLast = true)]
        private void SendCharacterJsonObserversRpc(string json)
        {
            Debug.Log($"[Client] Получил JSON ({json.Length} симв.)");
            // восстанавливаем из JSON
            _characterCustomization.LoadFromJSON(json);
        }

        /// <summary>
        /// Вызывается на клиенте, когда нужно отправить свой локальный JSON
        /// </summary>
        public void TransmitLocalCharacter()
        {
            string json = _characterCustomization.GetJSON();
            SendCharacterJsonServerRpc(json);
        }
    }
}