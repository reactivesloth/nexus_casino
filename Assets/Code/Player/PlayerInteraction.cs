using Cinemachine;
using UnityEngine;
using Code.InteractionSystem;
using Code.UI;
using Code.Utility;

namespace Code.Player
{
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask interactableMask;
        [SerializeField] private float detectionDistance = 3f;

        private Interactable _hovered;
        private Interactable _selected;
        private Interactable _active;
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
            playerController =  gameObject.GetComponent<PlayerMovementController>();
            
            var cm = CursorManager.Instance;
            if (cm != null) cm.HideCursor();
        }
        
        private void Update()
        {
            if (!playerController.IsOwner) return;

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
                    _selected.InteractCallback_Client += OnStartInteractCallbackClient;
                    _selected.RequestInteract();
                }
            }
            else
            {
                if (input != null && input.InteractDown && !_active.IsBusy && !IsBusy)
                {
                    IsBusy = true;

                    _selected = _active;
                    _selected.InteractCallback_Client += OnEndInteractCallbackClient;
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

            if (_active != null) InteractionUIHint.Instance.ShowPrompt(_active.InteractionPrompt);
            else if (_hovered != null) InteractionUIHint.Instance.ShowPrompt(_hovered.InteractionPrompt);
            else InteractionUIHint.Instance.HidePrompt();
        }

        private void OnStartInteractCallbackClient(bool success)
        {
            IsBusy = false;
            if (_selected != null)
                _selected.InteractCallback_Client -= OnStartInteractCallbackClient;

            if (success)
            {
                if (_selected != null && _selected.ManualRelease)
                    _active = _selected;
            }

            _selected = null;
        }

        private void OnEndInteractCallbackClient(bool success)
        {
            IsBusy = false;
            if (_selected != null)
                _selected.InteractCallback_Client -= OnEndInteractCallbackClient;

            if (success)
            {
                _active = null;
                _postEndCooldown = postEndCooldownTime;
            }

            _selected = null;
        }
    }
}
