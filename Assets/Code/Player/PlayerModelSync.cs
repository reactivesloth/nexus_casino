using System;
using System.Collections;
using System.Text;
using CC;
using PurrNet;
using UnityEngine;

namespace Code.Player
{
    [RequireComponent(typeof(CharacterCustomization))]
    public class PlayerModelSync : NetworkBehaviour
    {
        [SerializeField] private float updateAvatarInterval = 10f;
        
        private CharacterCustomization _characterCustomization;
        private NetworkAnimator _networkAnimator;
        private Coroutine _updateAvatarCoroutine;

        //private readonly SyncVar<string> _characterJson = new SyncVar<string>(ownerAuth:true);
        private readonly SyncBigData _characterJson = new SyncBigData(ownerAuth:true);
        
        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
            _networkAnimator = GetComponent<NetworkAnimator>();
            //_characterJson.onChanged += OnCharacterJsonChanged;
            _characterJson.onSyncStatusChanged += CharacterJsonSyncStatusChanged;
            _characterCustomization.Initialize();
        }

        private void CharacterJsonSyncStatusChanged(SyncStatus status)
        {
            if(!status.isDone)
                return;
            
            if (_characterJson.data.Array is null)
                throw new InvalidOperationException("Пустой ArraySegment");

            string text = Encoding.UTF8.GetString(
                _characterJson.data.Array,
                _characterJson.data.Offset,
                _characterJson.data.Count);
            
            OnCharacterJsonChanged(text);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            //_characterJson.onChanged -= OnCharacterJsonChanged;
            _characterJson.onSyncStatusChanged -= CharacterJsonSyncStatusChanged;
        }

        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
            if(isOwner)
                StartCoroutine(WaitAndSendLocalCharacter());
        }

        private void OnCharacterJsonChanged(string obj)
        {
            _characterCustomization.Autoload = false;
            _characterCustomization.Initialize();
            if (!string.IsNullOrEmpty(obj))
                _characterCustomization.LoadFromJSON(obj);
        }

        /*[ServerRpc(requireOwnership: false)]
        public void SendCharacterJsonServerRpc(string json)
        {
            //Debug.Log($"[Server] Получен JSON ({(json != null ? json.Length : 0)} симв.)");
            _characterJson.value = json ?? string.Empty;
        }*/

        private IEnumerator WaitAndSendLocalCharacter()
        {
            yield return null;
            yield return null;
            
            /*
            if(_updateAvatarCoroutine != null)
                StopCoroutine(_updateAvatarCoroutine);
            _updateAvatarCoroutine = StartCoroutine(UpdateLoop());*/
            TransmitLocalCharacter();
        }

        /*
        private IEnumerator UpdateLoop()
        {
            var wait = new WaitForSeconds(updateAvatarInterval);
            while (true)
            {
                TransmitLocalCharacter();
                yield return wait;
            }
        }
        */
        
        public void TransmitLocalCharacter()
        {
            if (!isOwner) return;
            Debug.Log("[Client] TransmitLocalCharacter");
            string json = _characterCustomization.GetJSON();
            // SendCharacterJsonServerRpc(json);
            //_characterJson.value = json ?? string.Empty;
            _characterJson.SetData(Encoding.UTF8.GetBytes(json));
        }
    }
}
