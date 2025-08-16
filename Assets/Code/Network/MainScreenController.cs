using Code.InteractionSystem;
using Code.Utility;
using FishNet.Component.Observing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    public class MainScreenController : NetworkBehaviour
    {
        [SerializeField] private RawImage screenRawImage;

        private NetworkImageStream _currentStream;
        
        public readonly SyncVar<int> StreamSlotId = new (new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });
        
        //public int GetStreamSlotId => StreamSlotId.Value;

        public void RequestStream(int slotId)
        {
            SetStream_ServerRpc(slotId);
        }

        public void RequestCancel()
        {
            ResetStream_ServerRpc();
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void SetStream_ServerRpc(int slotId)
        {
            var stream = SlotMachineInteractable.FindById(slotId).GetComponentInChildren<NetworkImageStream>();
            if(stream == null)
                return;
            
            ResetStreamer();
            
            StreamSlotId.Value = slotId;
            _currentStream = stream;
            
            var observerCondition = stream.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(false);
            SetStream_ObserversRpc(slotId);
        }

        [ObserversRpc(BufferLast = true)]
        public void SetStream_ObserversRpc(int slotId)
        {
            ResetStreamer();
            
            screenRawImage.gameObject.SetActive(true);
            
            var stream = SlotMachineInteractable.FindById(slotId).GetComponentInChildren<NetworkImageStream>();
            _currentStream = stream;
            var observerCondition = _currentStream.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(false);
            
            _currentStream.OnApplyTexture += ApplyTexture;
            _currentStream.OnSendTexture += ApplyTexture;
            Debug.Log($"SetStream_ObserversRpc({slotId})");
        }
        
        [ServerRpc(RequireOwnership = false)]
        public void ResetStream_ServerRpc()
        {
            var observerCondition = _currentStream.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(false);
            StreamSlotId.Value = -1;
            _currentStream = null;

            ResetStream_ObserversRpc();
        }
        
        [ObserversRpc(BufferLast = true)]
        public void ResetStream_ObserversRpc()
        {
            screenRawImage.gameObject.SetActive(false);
            ResetStreamer();
        }
        
        public void ResetStreamer()
        {
            if(_currentStream == null)
                return;
            var observerCondition =
                _currentStream.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(true);
            
            _currentStream.OnApplyTexture -= ApplyTexture;
            _currentStream.OnSendTexture -= ApplyTexture;
            _currentStream = null;
        }
        
        public void ApplyTexture(Texture texture)
        {
            Debug.Log($"ApplyTexture {texture} {texture?.height}x{texture?.width}");
            screenRawImage.texture = texture;
            ImageUtility.AdjustAspect(screenRawImage);
        }
    }
}