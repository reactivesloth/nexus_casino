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

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"OnStartClient: {Owner}. Is owner = {IsOwner}");
            if (IsOwner)
                _characterCustomizationJson.Value = _characterCustomization.GetJSON();
            //TransmitLocalCharacter();
        }
        
        private void CharacterCustomizationJsonOnOnChange(string prev, string next, bool asServer)
        {
            
            Debug.Log($"Clent {Owner} recived:\n{next}");
            if(!string.IsNullOrEmpty(next))
                _characterCustomization.LoadFromJSON(next);
        }
    }
}