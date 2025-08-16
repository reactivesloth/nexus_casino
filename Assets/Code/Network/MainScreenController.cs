using Code.InteractionSystem;
using Code.Utility;
using FishNet.Component.Observing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    public class MainScreenController : NetworkBehaviour
    {
        [SerializeField] private int currentSlotId = -1;
        [Space]
        [SerializeField] private RawImage screenRawImage;

        private NetworkImageStream _currentStreamOnClient;
        private NetworkImageStream _currentStreamOnServer;

        public readonly SyncVar<int> StreamSlotId = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        private void OnEnable()
        {
            StreamSlotId.OnChange += OnStreamSlotIdChange;
        }

        private void OnDisable()
        {
            StreamSlotId.OnChange -= OnStreamSlotIdChange;
        }

        public void RequestStream(int slotId) => SetStream_ServerRpc(slotId);

        public void RequestCancel() => SetStream_ServerRpc(-1);

        public void ApplyTexture(Texture texture)
        {
            screenRawImage.texture = texture;
            ImageUtility.AdjustAspect(screenRawImage);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetStream_ServerRpc(int slotId)
        {
            ServerReset();
            StreamSlotId.Value = slotId;
            _currentStreamOnServer = GetCurrentStream(slotId);
            SetConditionsEnable(false);
        }

        private void OnStreamSlotIdChange(int prev, int next, bool asServer)
        {
            if (prev == next)
                return;
            
            currentSlotId = next;
            Debug.Log($"Reset for id {prev}, new id is {next}. Current stream is {_currentStreamOnClient}");
            ClientReset();
            _currentStreamOnClient = GetCurrentStream(next);
            screenRawImage.gameObject.SetActive(_currentStreamOnClient != null);
            if(_currentStreamOnClient == null)
                return;
            
            _currentStreamOnClient.OnApplyTexture += ApplyTexture;
        }

        [Client]
        private void ClientReset()
        {
            if(_currentStreamOnClient == null)
                return;
            
            _currentStreamOnClient.OnApplyTexture -= ApplyTexture;
            _currentStreamOnClient = null;
            screenRawImage.gameObject.SetActive(false);
        }

        [Server]
        private void ServerReset()
        {
            if(_currentStreamOnServer == null)
                return;
            
            SetConditionsEnable(true);

            _currentStreamOnServer = null;
        }

        [Server]
        private void SetConditionsEnable(bool enable)
        {
            if (_currentStreamOnServer == null)
                return;

            Debug.Log($"[Server] SetConditionsEnable {enable}");
            var observerCondition =
                _currentStreamOnServer.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(enable);
        }
        
        private NetworkImageStream GetCurrentStream(int id) =>
            SlotMachineInteractable.FindById(id)?.NetworkImageStream;
    }
}