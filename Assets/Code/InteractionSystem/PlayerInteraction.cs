using System;
using UnityEngine;
using FishNet.Object;
using Code.InteractionSystem;
using SRF;
using Unity.VisualScripting;

namespace Code.Player
{
    public class PlayerInteraction : NetworkBehaviour
    {
        [Header("Detection")]
        [SerializeField] private LayerMask interactableMask;
        [SerializeField] private float detectionDistance = 3f;

        private Interactable _hovered;
        private Interactable _active;
        private GameObject[] outlineGameObjects;
        
        private void Update()
        {
            if (!IsOwner) return;

            if (_active == null)
            {
                UpdateHover();
                if (_hovered != null && Input.GetKeyDown(KeyCode.E))
                {
                    _hovered.RequestInteract();
                    if (_hovered.ManualRelease)
                        _active = _hovered;
                }
            }
            else
            {
                if (Input.GetKeyDown(KeyCode.E))
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
            if (Physics.Raycast(ray, out RaycastHit hit, detectionDistance, interactableMask))
            {
                var interactable = hit.collider.GetComponent<Interactable>();
                if (interactable != null && interactable.IsEnabled && !interactable.IsOccupied)
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
            
            if (outlineGameObjects != (target != null ? target.outlineGameObjects : null))
            {
                if (outlineGameObjects != null)
                {
                    foreach (var go in outlineGameObjects)
                    {
                        go.RemoveComponentIfExists<Outline>();
                    }
                }

                outlineGameObjects = target != null ? target.outlineGameObjects : null;
                if (outlineGameObjects != null)
                {
                    foreach (var go in outlineGameObjects)
                    {
                        var o = go.GetOrAddComponent<Outline>();
                        o.OutlineColor = Color.yellow;
                        o.OutlineWidth = 10;
                        o.OutlineMode = Outline.Mode.OutlineAll;
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
    }
}