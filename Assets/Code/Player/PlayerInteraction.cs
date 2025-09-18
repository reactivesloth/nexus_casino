using Cinemachine;
using UnityEngine;
using Code.InteractionSystem;
using Code.UI;
using Code.Utility;

namespace Code.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Detection")] [SerializeField] private LayerMask interactableMask;
        [SerializeField] private float detectionDistance = 3f;

        private Interactable _hovered;
        private Interactable _selected;
        public Interactable Active;
        private GameObject[] _outlineGameObjects;

        private Cinemachine3rdPersonFollow virtualCamera;
        private PlayerInput input;
        private PlayerMovementController playerController;

        protected bool IsBusy = false;

        private bool _initedPlayerInteraction;
        private Interactable _prevHovered;

        private float _postEndCooldown;
        [SerializeField] private float postEndCooldownTime = 0.05f;

        private void Awake()
        {
            if (_initedPlayerInteraction) return;
            _initedPlayerInteraction = true;

            var vcam = FindAnyObjectByType<CinemachineVirtualCamera>();
            if (vcam != null)
                virtualCamera = vcam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

            input = PlayerInput.Instance;
            playerController = gameObject.GetComponent<PlayerMovementController>();
        }

        private void Update()
        {
            if (!playerController.IsOwner) return;

            if (_postEndCooldown > 0f)
                _postEndCooldown -= Time.deltaTime;

            if (Active != null && (!Active || !Active.isActiveAndEnabled))
                Active = null;

            if (Active == null)
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
                    RequestInteractWith(_hovered);
                }
            }
            else
            {
                if (input != null && input.InteractEndDown && !Active.IsBusy && !IsBusy)
                {
                    IsBusy = true;

                    _selected = Active;
                    _selected.RequestEndInteract();
                }
            }

            UpdateOutline();
            UpdateUI();
        }

        public void RequestInteractWith(Interactable interactable, bool force = false)
        {
            IsBusy = true;
            
            _selected = interactable;
            _selected.InteractCallback_Client += OnStartInteractCallbackClient;
            _selected.RequestInteract(force);
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
            if (_hovered != null && _prevHovered == null)
            {
                _outlineGameObjects = _hovered.outlineGameObjects;
                if (_outlineGameObjects != null)
                {
                    foreach (var go in _outlineGameObjects)
                    {
                        if (go == null) continue;
                        var o = go.GetComponent<OutlineMesh>();
                        if (o == null) o = go.AddComponent<OutlineMesh>();
                        o.OutlineColor = Color.yellow;
                        o.OutlineWidth = 10;
                        o.OutlineMode = OutlineMesh.Mode.OutlineVisible;
                    }

                    _prevHovered = _hovered;
                }
            }
            else if (_prevHovered != _hovered && _prevHovered != null)
            {
                if (_outlineGameObjects != null)
                {
                    foreach (var go in _outlineGameObjects)
                    {
                        if (go == null) continue;
                        var o = go.GetComponent<OutlineMesh>();
                        if (o != null) Destroy(o);
                    }
                }

                _prevHovered = null;
            }
        }

        private void UpdateUI()
        {
            if (InteractionUIHint.Instance == null) return;

            var interactText = PlayerInput.Instance.IsUsingMobileFallback
                ? "Press Interact button to use"
                : "Press E to use";
            var endInteractText = PlayerInput.Instance.IsUsingMobileFallback
                ? string.Empty
                : "Press E to stand up";
            
            if (Active != null) InteractionUIHint.Instance.ShowPrompt(endInteractText);
            else if (_hovered != null) InteractionUIHint.Instance.ShowPrompt(interactText);
            else InteractionUIHint.Instance.HidePrompt();
        }

        private void OnStartInteractCallbackClient(bool success)
        {
            IsBusy = false;
            if (_selected != null)
            {
                _selected.InteractCallback_Client -= OnStartInteractCallbackClient;
                _selected.InteractCallback_Client += OnEndInteractCallbackClient;
            }

            if (success)
            {
                if (_selected != null && _selected.ManualRelease)
                    Active = _selected;

                _hovered = null;

                if (Active.GetComponentInChildren<SlotMachineInteractable>(true))
                {
                    PlayerInput.Instance.ShowInteractUI(true, "Slots");
                }
                else
                {
                    PlayerInput.Instance.ShowInteractUI(true, "Base");
                }
            }

            _selected = null;
        }

        private void OnEndInteractCallbackClient(bool success)
        {
            IsBusy = false;
            if (Active != null)
                Active.InteractCallback_Client -= OnEndInteractCallbackClient;

            if (success)
            {
                Active = null;
                _postEndCooldown = postEndCooldownTime;
                PlayerInput.Instance.ShowInteractUI(false);
            }

            _selected = null;
        }
    }
}