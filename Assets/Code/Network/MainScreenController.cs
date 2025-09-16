using System;
using Code.InteractionSystem;
using Code.Utility;
using FishNet.Component.Observing;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    public class MainScreenController : NetworkBehaviour
    {
        [SerializeField] private int currentSlotId = -1;
        [Space] [SerializeField] private GameObject elementsParent;
        [SerializeField] private RawImage screenRawImage;
        [SerializeField] private TMP_Text slotIdText;
        [SerializeField] private TMP_Text streamerNameText;

        private SlotMachineInteractable _currentStreamOnClient;
        private SlotMachineInteractable _currentStreamOnServer;

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
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            // Проверяем, есть ли актуальный объект стрима
            if (StreamSlotId.Value < 0) 
                return;
            
            _currentStreamOnServer = GetCurrentStream(StreamSlotId.Value);

            // Если объект не найден (старый хост ушёл), то сбрасываем
            if (_currentStreamOnServer == null)
            {
                SetStream(-1, -1, string.Empty);
            }
            else
            {
                SetStream(StreamSlotId.Value, -1, StreamerUsername.Value);
                _currentStreamOnServer.InteractCallback_Server += OnEndTargetInteraction;
                SetConditionsEnable(false);
            }
        }
        
        public void RequestStream(int slotId, int connectionId, string username) =>
            SetStream_ServerRpc(slotId, connectionId, username);

        public void RequestCancel() => SetStream_ServerRpc(-1, -1, string.Empty);

        public void ApplyTexture(Texture texture)
        {
            screenRawImage.texture = texture;
            ImageUtility.AdjustAspect(screenRawImage);
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetStream_ServerRpc(int slotId, int connectionId, string username) =>
            SetStream(slotId, connectionId, username);

        [Server]
        public void SetStream(int slotId, int connectionId, string username)
        {
            if (_currentStreamOnServer != null)
                _currentStreamOnServer.InteractCallback_Server -= OnEndTargetInteraction;
            
            ServerReset();

            StreamSlotId.Value = slotId;
            StreamerUsername.Value = username;

            _currentStreamOnServer = GetCurrentStream(slotId);
            if (_currentStreamOnServer != null)
                _currentStreamOnServer.InteractCallback_Server += OnEndTargetInteraction;

            SetConditionsEnable(false);
        }

        private void OnStreamSlotIdChange(int prev, int next, bool asServer)
        {
            if (prev == next) return;

            currentSlotId = next;
            ClientReset();
            _currentStreamOnClient = GetCurrentStream(next);
            elementsParent.gameObject.SetActive(_currentStreamOnClient != null);
            if (_currentStreamOnClient == null) return;
            
            _currentStreamOnClient.NetworkImageStream.OnApplyTexture += ApplyTexture;
            slotIdText.text = $"Slot №{next}";
        }

        private void StreamerUsernameOnOnChange(string prev, string next, bool asServer)
        {
            streamerNameText.text = $"{next}";
        }

        [Client]
        private void ClientReset()
        {
            if (_currentStreamOnClient == null) return;
            _currentStreamOnClient.NetworkImageStream.OnApplyTexture -= ApplyTexture;
            _currentStreamOnClient = null;
            elementsParent.gameObject.SetActive(false);
        }

        [Server]
        private void ServerReset()
        {
            if (_currentStreamOnServer == null) return;
            SetConditionsEnable(true);
            _currentStreamOnServer = null;
        }

        [Server]
        private void SetConditionsEnable(bool enable)
        {
            if (_currentStreamOnServer == null)
                return;

            var observerCondition =
                _currentStreamOnServer.NetworkObject.NetworkObserver.GetObserverCondition<DistanceCondition>();
            observerCondition.SetIsEnabled(enable);
        }

        [Server]
        private void OnEndTargetInteraction(bool success)
        {
            SetStream(-1, -1, string.Empty);
        }

        private SlotMachineInteractable GetCurrentStream(int id) => SlotMachineInteractable.FindById(id);
    }
}