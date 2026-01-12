using System.Collections;
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

        private readonly SyncVar<string> _characterJson;
        
        private void Awake()
        {
            _characterCustomization = GetComponent<CharacterCustomization>();
            _networkAnimator = GetComponent<NetworkAnimator>();
            _characterJson.onChanged += OnCharacterJsonChanged;
            _characterCustomization.Initialize();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _characterJson.onChanged -= OnCharacterJsonChanged;
        }

        private void OnCharacterJsonChanged(string obj)
        {
            _characterCustomization.Autoload = false;
            _characterCustomization.Initialize();
            if (!string.IsNullOrEmpty(obj))
                _characterCustomization.LoadFromJSON(obj);
        }
        
        // public override void OnOwnershipClient(NetworkConnection prevOwner)
        // {
        //      base.OnOwnershipClient(prevOwner);
        //      if (isOwner) StartCoroutine(WaitAndSendLocalCharacter());
        // }

        [ServerRpc(requireOwnership: false)]
        public void SendCharacterJsonServerRpc(string json)
        {
            //Debug.Log($"[Server] Получен JSON ({(json != null ? json.Length : 0)} симв.)");
            _characterJson.value = json ?? string.Empty;
        }

        private IEnumerator WaitAndSendLocalCharacter()
        {
            while (!isClientAndObserving || !isFullySpawned)
                yield return null;
            yield return null;
            
            if(_updateAvatarCoroutine != null)
                StopCoroutine(_updateAvatarCoroutine);
            _updateAvatarCoroutine = StartCoroutine(UpdateLoop());
        }

        private IEnumerator UpdateLoop()
        {
            var wait = new WaitForSeconds(updateAvatarInterval);
            while (true)
            {
                TransmitLocalCharacter();
                yield return wait;
            }
        }
        
        public void TransmitLocalCharacter()
        {
            if (!isOwner) return;
            //Debug.Log("[Client] TransmitLocalCharacter");
            string json = _characterCustomization.GetJSON();
            SendCharacterJsonServerRpc(json);
        }
    }
}
