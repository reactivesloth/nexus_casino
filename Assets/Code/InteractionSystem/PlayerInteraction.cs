using Cinemachine;
using UnityEngine;
using FishNet.Object;
using Code.InteractionSystem;
using Code.Network.HostMigration;
using Code.Network.HostMigration.Components;
using Code.Network.Player;
using FishNet.Connection;

namespace Code.Player
{
    public class PlayerInteraction : NetworkBehaviour, IMigratable<CharacterInteractableMigrateData>
    {
        [Header("Detection")]
        [SerializeField] private LayerMask interactableMask;
        [SerializeField] private float detectionDistance = 3f;

        private Interactable _hovered;
        private Interactable _selected;
        private Interactable _active;
        private GameObject[] outlineGameObjects;

        private Cinemachine3rdPersonFollow virtualCamera;
        private PlayerInput input;

        protected bool IsBusy = false;

        private bool _initedPlayerInteraction;

        private float _postEndCooldown;
        [SerializeField] private float postEndCooldownTime = 0.05f;

        private void EnsureInit()
        {
            if (_initedPlayerInteraction) return;
            _initedPlayerInteraction = true;

            var vcam = FindObjectOfType<CinemachineVirtualCamera>();
            if (vcam != null)
                virtualCamera = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

            input = PlayerInput.Instance;

            var cm = CursorManager.Instance;
            if (cm != null) cm.HideCursor();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureInit();
        }

        private void Update()
        {
            if (!IsOwner) return;
            EnsureInit();

            if (_postEndCooldown > 0f)
                _postEndCooldown -= Time.deltaTime;

            if (_active != null && (!_active || !_active.isActiveAndEnabled))
                _active = null;

            if (_active == null)
            {
                UpdateHover();

                bool cursorVisible = CursorManager.Instance != null && CursorManager.Instance.IsVisible();

                if (_hovered != null &&
                    input != null &&
                    input.InteractDown &&
                    !cursorVisible &&
                    !_hovered.IsBusy &&
                    !IsBusy &&
                    _postEndCooldown <= 0f)
                {
                    IsBusy = true;

                    _selected = _hovered;
                    _selected.InteractCallback += OnStartInteractCallback;
                    _selected.RequestInteract();
                }
            }
            else
            {
                if (input != null && input.InteractDown && !_active.IsBusy && !IsBusy)
                {
                    IsBusy = true;

                    _selected = _active;
                    _selected.InteractCallback += OnEndInteractCallback;
                    _selected.RequestEndInteract();
                }
            }

            UpdateOutline();
            UpdateUI();
        }

        private void UpdateHover()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                _hovered = null;
                return;
            }

            float extra = (virtualCamera != null) ? virtualCamera.CameraDistance : 0f;
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, detectionDistance + extra, interactableMask))
            {
                var interactable = hit.collider != null ? hit.collider.GetComponent<Interactable>() : null;

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
                outlineGameObjects = target.outlineGameObjects;
                if (outlineGameObjects != null)
                {
                    for (int i = 0; i < outlineGameObjects.Length; i++)
                    {
                        var go = outlineGameObjects[i];
                        if (go == null) continue;
                        var o = go.GetComponent<OutlineMesh>();
                        if (o == null) o = go.AddComponent<OutlineMesh>();
                        o.OutlineColor = Color.yellow;
                        o.OutlineWidth = 10;
                        o.OutlineMode = OutlineMesh.Mode.OutlineVisible;
                    }
                }
            }
            else
            {
                if (outlineGameObjects != null)
                {
                    for (int i = 0; i < outlineGameObjects.Length; i++)
                    {
                        var go = outlineGameObjects[i];
                        if (go == null) continue;
                        var o = go.GetComponent<OutlineMesh>();
                        if (o != null) Destroy(o);
                    }
                }
            }
        }

        private void UpdateUI()
        {
            if (InteractionUIHint.Instance == null) return;

            if (_active != null) InteractionUIHint.Instance.ShowPrompt(_active.InteractionPrompt);
            else if (_hovered != null) InteractionUIHint.Instance.ShowPrompt(_hovered.InteractionPrompt);
            else InteractionUIHint.Instance.HidePrompt();
        }

        private void OnStartInteractCallback(bool success)
        {
            IsBusy = false;
            if (_selected != null)
                _selected.InteractCallback -= OnStartInteractCallback;

            if (success)
            {
                if (_selected != null && _selected.ManualRelease)
                    _active = _selected;
            }

            _selected = null;
        }

        private void OnEndInteractCallback(bool success)
        {
            IsBusy = false;
            if (_selected != null)
                _selected.InteractCallback -= OnEndInteractCallback;

            if (success)
            {
                _active = null;
                _postEndCooldown = postEndCooldownTime;
            }

            _selected = null;
        }

        #region IMigratable
        public void OnMigrateDataReceived(CharacterInteractableMigrateData data)
        {
            if (!NetworkManager.IsServerStarted || string.IsNullOrEmpty(data.activeId))
                return;

            var sceneObject = SceneObject.GetObjectById(data.activeId);
            if (!sceneObject) return;
            if (!sceneObject.TryGetComponent(out Interactable interactable)) return;

            if (interactable.IsOccupied)
                return;

            interactable.ServerForceInteract(Owner);
            SetInteractableOnMigrate(Owner, data);
        }

        [TargetRpc]
        public void SetInteractableOnMigrate(NetworkConnection conn, CharacterInteractableMigrateData data)
        {
            var sceneObject = SceneObject.GetObjectById(data.activeId);
            if (!sceneObject) return;
            if (!sceneObject.TryGetComponent(out Interactable interactable)) return;

            _active = interactable;
        }

        public CharacterInteractableMigrateData GetMigrateData()
        {
            if (_active == null) return default;
            if (!_active.TryGetComponent<SceneObject>(out var sceneObject)) return default;
            return new CharacterInteractableMigrateData { activeId = sceneObject.ObjectGuid.ToString() };
        }
        #endregion
    }
}
