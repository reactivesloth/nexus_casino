using System;
using System.Net;
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
        [SerializeField] private int currentSlotId = -1;
        [Space]
        [SerializeField] private RawImage screenRawImage;

        private NetworkImageStream _currentStream;

        public readonly SyncVar<int> StreamSlotId = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission = ReadPermission.Observers
        });

        private NetworkImageStream GetCurrentStream =>
            SlotMachineInteractable.FindById(StreamSlotId.Value)?.NetworkImageStream;

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
            _currentStream = GetCurrentStream;
            SetConditionsEnable(true);
        }

        [Client]
        private void OnStreamSlotIdChange(int prev, int next, bool asServer)
        {
            if (prev == next)
                return;
            
            currentSlotId = next;
            Debug.Log($"Reset for id {prev}, new id is {next}");
            ClientReset();
            _currentStream = GetCurrentStream;
            if(_currentStream == null)
                return;
            
            _currentStream.OnApplyTexture += ApplyTexture;
            screenRawImage.gameObject.SetActive(true);
        }

        [Client]
        private void ClientReset()
        {
            if(_currentStream == null)
                return;
            
            _currentStream.OnApplyTexture -= ApplyTexture;
            _currentStream = null;
            screenRawImage.gameObject.SetActive(false);
        }

        [Server]
        private void ServerReset()
        {
            if(_currentStream == null)
                return;
            
            SetConditionsEnable(false);

            _currentStream = null;
        }

        [Server]
        private void SetConditionsEnable(bool enable)
        {
            if (_currentStream == null)
                return;

            var observerCondition =
                _currentStream.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(enable);
        }
    }
}