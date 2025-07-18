using System;
using Cinemachine;
using UnityEngine;
using FishNet.Object;
using Code.InteractionSystem;
using Code.Network.HostMigration;
using Code.Network.HostMigration.Components;
using Code.Network.Player;
using FishNet.Connection;
using SRF;
using Unity.VisualScripting;

namespace Code.Player
{
    public class PlayerInteraction : NetworkBehaviour, IMigratable<CharacterInteractableMigrateData>
    {
        [Header("Detection")] [SerializeField] private LayerMask interactableMask;
        [SerializeField] private float detectionDistance = 3f;

        private Interactable _hovered;
        private Interactable _active;
        private GameObject[] outlineGameObjects;

        private Cinemachine3rdPersonFollow virtualCamera;
        
        private void Awake()
        {
            virtualCamera ??= FindObjectOfType<CinemachineVirtualCamera>().GetCinemachineComponent<Cinemachine3rdPersonFollow>();;
        }

        private void Update()
        {
            if (!IsOwner) return;

            
            if (_active == null)
            {
                UpdateHover();
                if (_hovered != null && Input.GetKeyDown(KeyCode.E) && !_hovered.IsBusy)
                {
                    _hovered.RequestInteract();
                    if (_hovered.ManualRelease)
                        _active = _hovered;
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.E) && !_active.IsBusy)
                {
                    _active.RequestEndInteract();
                    _active = null;
                }
            }

            UpdateOutline();
            UpdateUI();
        }

        private void UpdateHover()
        {
            var cam = Camera.main;
            if (cam == null) return;

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, detectionDistance + virtualCamera.CameraDistance, interactableMask))
            {
                var interactable = hit.collider.GetComponent<Interactable>();
                if (interactable != null && interactable.IsEnabled && !interactable.IsOccupied && !interactable.IsBusy)
                {
                    _hovered = interactable;
                    return;
                }
            }

            _hovered = null;
        }

        private void UpdateOutline()
        {
            var target = _active != null ? _active : _hovered;
            
            if (target != null && !target.IsOccupied)
            {
                outlineGameObjects = target != null ? target.outlineGameObjects : null;
                if (outlineGameObjects != null)
                {
                    foreach (var go in outlineGameObjects)
                    {
                        var o = go.GetOrAddComponent<Outline>();
                        o.OutlineColor = Color.yellow;
                        o.OutlineWidth = 10;
                        o.OutlineMode = Outline.Mode.OutlineVisible;
                    }
                }
            }
            else
            {
                if (outlineGameObjects != null)
                {
                    foreach (var go in outlineGameObjects)
                    {
                        go.RemoveComponentIfExists<Outline>();
                    }
                }

            }
        }

        private void UpdateUI()
        {
            if (_active != null)
                InteractionUIHint.Instance.ShowPrompt(_active.InteractionPrompt);
            else if (_hovered != null)
                InteractionUIHint.Instance.ShowPrompt(_hovered.InteractionPrompt);
            else
                InteractionUIHint.Instance.HidePrompt();
        }

        #region IMigratable

        public void OnMigrateDataReceived(CharacterInteractableMigrateData data)
        {
            if (!NetworkManager.IsServerStarted || string.IsNullOrEmpty(data.activeId))
                return;

            SetInteractableOnMigrate(Owner, data);
        }

        [TargetRpc]
        public void SetInteractableOnMigrate(NetworkConnection conn, CharacterInteractableMigrateData data)
        {
            var sceneObject = SceneObject.GetObjectById(data.activeId);
            if (!sceneObject)
                return;
            if (!sceneObject.TryGetComponent(out Interactable interactable))
                return;

            _active = interactable;
            _active.RequestInteract();
        }

        public CharacterInteractableMigrateData GetMigrateData()
        {
            if (!_active || !_active.TryGetComponent<SceneObject>(out var sceneObject))
                return default;
            return new CharacterInteractableMigrateData
            {
                activeId = sceneObject.ObjectGuid.ToString()
            };
        }

        #endregion
    }
}