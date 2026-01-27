using Code.Network.InteractionSystem;
using Code.Utility;
using PurrNet;
using TMPro;
using UnityEngine;

namespace Code.Network.Stream
{
    public class MainScreenController : NetworkBehaviour
    {
        [SerializeField] private int currentSlotId = -1;
        [Space] [SerializeField] private GameObject elementsParent;
        [SerializeField] private MeshRenderer screenRawImage;
        [SerializeField] private int screenRawIndex;
        [SerializeField] private Texture screenRawTextureEmpty;
        [SerializeField] private TMP_Text slotIdText;
        [SerializeField] private TMP_Text streamerNameText;

        private SlotMachineInteractable CurrentStreamSlot => GetCurrentStream(StreamSlotId.value);
        private SlotMachineInteractable _prevStreamSlot;

        public readonly SyncVar<int> StreamSlotId = new SyncVar<int>(1);

        public readonly SyncVar<string> StreamerUsername = new SyncVar<string>();

        protected void OnValidate()
        {
            if (screenRawImage != null) elementsParent ??= screenRawImage.transform.parent.gameObject;
        }

        private void OnEnable()
        {
            StreamSlotId.onChanged += OnStreamSlotIdChange;
            StreamerUsername.onChanged += StreamerUsernameOnOnChange;
        }

        private void OnDisable()
        {
            StreamSlotId.onChanged -= OnStreamSlotIdChange;
            StreamerUsername.onChanged -= StreamerUsernameOnOnChange;
            
            if (_prevStreamSlot != null && _prevStreamSlot.NetworkImageStream != null)
                _prevStreamSlot.NetworkImageStream.OnApplyTexture -= ApplyTexture;
        }

        public void RequestStream(int slotId, string username)
        {
            SetStream_ServerRpc(slotId, username);
        }

        public void RequestCancel() => SetStream_ServerRpc(-1, string.Empty);

        [ServerRpc(requireOwnership: false)]
        private void SetStream_ServerRpc(int slotId, string username) =>
            SetStream(slotId, username); //TODO: request mechanic, if its need

        private void SetStream(int slotId, string username)
        {
            if(CurrentStreamSlot != null)
            {
                CurrentStreamSlot.EndInteractCallback_Server -= OnTargetEndInteraction;
                SetConditionsEnable(true);
            }
            
            StreamSlotId.value = slotId;
            StreamerUsername.value = username;
            
            if (CurrentStreamSlot != null)
            {
                CurrentStreamSlot.EndInteractCallback_Server += OnTargetEndInteraction;
                SetConditionsEnable(false);
            }
        }

        private void OnStreamSlotIdChange(int next)
        {
            if (_prevStreamSlot != null && _prevStreamSlot.NetworkImageStream != null)
                _prevStreamSlot.NetworkImageStream.OnApplyTexture -= ApplyTexture;

            if (elementsParent != null) elementsParent.gameObject.SetActive(CurrentStreamSlot != null);

            _prevStreamSlot = CurrentStreamSlot;
            
            if (CurrentStreamSlot == null)
            {
                if (screenRawImage != null)
                {
                    screenRawImage.materials[screenRawIndex].mainTexture = screenRawTextureEmpty;
                    screenRawImage.materials[screenRawIndex].mainTextureScale = new Vector2(1, 1);
                }
                if (slotIdText != null) slotIdText.text = string.Empty;
                return;
            }

            if (CurrentStreamSlot.NetworkImageStream != null)
            {
                CurrentStreamSlot.NetworkImageStream.OnApplyTexture += ApplyTexture;
                if (CurrentStreamSlot.NetworkImageStream.RecvTexture != null)
                    ApplyTexture(CurrentStreamSlot.NetworkImageStream.RecvTexture);
            }

            if (slotIdText != null) slotIdText.text = $"Slot №{next}";
        }

        private void StreamerUsernameOnOnChange(string next)
        {
            if (streamerNameText != null) streamerNameText.text = $"{next}";
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

            //var observerCondition = CurrentStreamSlot.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            //observerCondition.SetIsEnabled(enable);
        }
        
        private void ApplyTexture(Texture texture)
        {
            if (screenRawImage != null)
            {
                screenRawImage.materials[screenRawIndex].mainTexture = texture;
                screenRawImage.materials[screenRawIndex].mainTextureScale = new Vector2(1, -1);
            }
        }

        private SlotMachineInteractable GetCurrentStream(int id) => SlotMachineInteractable.FindById(id);
    }
}