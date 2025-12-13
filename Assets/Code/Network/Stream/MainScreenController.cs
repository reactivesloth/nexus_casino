using Code.InteractionSystem;
using Code.Utility;
using FishNet.Component.Observing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network.Stream
{
    public class MainScreenController : NetworkBehaviour
    {
        [SerializeField] private int currentSlotId = -1;
        [Space] [SerializeField] private GameObject elementsParent;
        [SerializeField] private RawImage screenRawImage;
        [SerializeField] private TMP_Text slotIdText;
        [SerializeField] private TMP_Text streamerNameText;

        private SlotMachineInteractable CurrentStreamSlot => GetCurrentStream(StreamSlotId.Value);
        private SlotMachineInteractable _prevStreamSlot;

        public readonly SyncVar<int> StreamSlotId = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        public readonly SyncVar<string> StreamerUsername = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        protected override void OnValidate()
        {
            base.OnValidate();
            elementsParent ??= screenRawImage.transform.parent.gameObject;
        }

        private void Awake()
        {
            StreamSlotId.SetInitialValues(-1);
        }

        private void OnEnable()
        {
            StreamSlotId.OnChange += OnStreamSlotIdChange;
            StreamerUsername.OnChange += StreamerUsernameOnOnChange;
        }

        private void OnDisable()
        {
            StreamSlotId.OnChange -= OnStreamSlotIdChange;
            StreamerUsername.OnChange -= StreamerUsernameOnOnChange;
            
            if (_prevStreamSlot != null && _prevStreamSlot.NetworkImageStream != null)
                _prevStreamSlot.NetworkImageStream.OnApplyTexture -= ApplyTexture;
        }

        public void RequestStream(int slotId, string username) =>
            SetStream_ServerRpc(slotId, username);

        public void RequestCancel() => SetStream_ServerRpc(-1, string.Empty);

        [ServerRpc(RequireOwnership = false)]
        private void SetStream_ServerRpc(int slotId, string username) =>
            SetStream(slotId, username); //TODO: request mechanic, if its need

        private void SetStream(int slotId, string username)
        {
            if(CurrentStreamSlot != null)
            {
                CurrentStreamSlot.EndInteractCallback_Server -= OnTargetEndInteraction;
                SetConditionsEnable(true);
            }
            
            StreamSlotId.Value = slotId;
            StreamerUsername.Value = username;
            
            if (CurrentStreamSlot != null)
            {
                CurrentStreamSlot.EndInteractCallback_Server += OnTargetEndInteraction;
                SetConditionsEnable(true);
            }
        }

        private void OnStreamSlotIdChange(int prev, int next, bool asServer)
        {
            if (_prevStreamSlot != null && _prevStreamSlot.NetworkImageStream != null)
                _prevStreamSlot.NetworkImageStream.OnApplyTexture -= ApplyTexture;
            
            elementsParent.gameObject.SetActive(CurrentStreamSlot != null);
           
            _prevStreamSlot = CurrentStreamSlot;
            
            if (CurrentStreamSlot == null)
            {
                screenRawImage.texture = null;
                slotIdText.text = string.Empty;
                return;
            }
            
            CurrentStreamSlot.NetworkImageStream.OnApplyTexture += ApplyTexture;
            slotIdText.text = $"Slot №{next}";
        }

        private void StreamerUsernameOnOnChange(string prev, string next, bool asServer)
        {
            streamerNameText.text = $"{next}";
        }
        
        private void OnTargetEndInteraction(bool success)
        {
            if(!success) 
                return;
            
            if(CurrentStreamSlot != null)
                CurrentStreamSlot.EndInteractCallback_Server -= OnTargetEndInteraction;
            SetStream(-1, string.Empty);
        }
        
        private void SetConditionsEnable(bool enable)
        {
            if (CurrentStreamSlot == null)
                return;

            var observerCondition =
                CurrentStreamSlot.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(enable);
        }
        
        private void ApplyTexture(Texture texture)
        {
            screenRawImage.texture = texture;
            ImageUtility.AdjustAspect(screenRawImage);
        }

        private SlotMachineInteractable GetCurrentStream(int id) => SlotMachineInteractable.FindById(id);
    }
}