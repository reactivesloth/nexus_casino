using System;
using UnityEngine;
using FishNet.Connection;
using FishNet.Object;

namespace Code.InteractionSystem
{
    public class CompositeInteractable : Interactable
    {
        [Header("Children to interact with")]
        [SerializeField, Tooltip("Все дочерние Interactable, которые запускаются одним нажатием.")]
        private Interactable[] children;

        [SerializeField] private bool generateColliderFromChildren = true;

        private BoxCollider _compositeCollider;

        private void Awake()
        {
            if (_compositeCollider == null)
            {
                _compositeCollider = GetComponent<BoxCollider>();
                if (_compositeCollider != null)
                    _compositeCollider.isTrigger = true;
            }
            if (children == null) children = Array.Empty<Interactable>();
            
            if (generateColliderFromChildren && _compositeCollider != null)
                UpdateCompositeColliderBounds();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_compositeCollider == null) return;
            Gizmos.color = Color.yellow;
            Vector3 worldCenter = transform.TransformPoint(_compositeCollider.center);
            Vector3 worldSize = Vector3.Scale(_compositeCollider.size, transform.lossyScale);
            Gizmos.DrawWireCube(worldCenter, worldSize);
        }
#endif

        private void UpdateCompositeColliderBounds()
        {
            var all = GetComponentsInChildren<Collider>(true);
            if (all == null || all.Length == 0 || _compositeCollider == null) return;

            Bounds? b = null;
            for (int i = 0; i < all.Length; i++)
            {
                var c = all[i];
                if (c == null || c == _compositeCollider) continue;
                if (!b.HasValue) b = c.bounds;
                else
                {
                    var bb = b.Value;
                    bb.Encapsulate(c.bounds);
                    b = bb;
                }
            }

            if (!b.HasValue) return;
            var bounds = b.Value;

            _compositeCollider.center = transform.InverseTransformPoint(bounds.center);

            var ls = transform.lossyScale;
            Vector3 worldSize = bounds.size;
            Vector3 localSize = new Vector3(
                ls.x != 0f ? worldSize.x / ls.x : 0f,
                ls.y != 0f ? worldSize.y / ls.y : 0f,
                ls.z != 0f ? worldSize.z / ls.z : 0f
            );
            _compositeCollider.size = localSize;
        }
        
        
        protected override void OnInteractCallback_Client(bool success, bool force = false)
        {
            base.OnInteractCallback_Client(success, force);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            if (children == null) return;

            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null) continue;
                child.RequestInteract(force);
            }
        }

        protected override void OnInteractEndCallback_Client(bool success)
        {
            base.OnInteractEndCallback_Client(success);
            
            if(!success)
            {
                // none sucsess action
                return;
            }
            
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child == null) continue;

                    if (child.ManualRelease)
                    {
                        child.RequestEndInteract();
                    }
                }
            }
        }

        private void Update()
        {
            IsBusy = false;
            if (children == null) return;
            for (int i = 0; i < children.Length; i++)
            {
                var c = children[i];
                if (c != null && c.IsBusy) { IsBusy = true; break; }
            }
        }
    }
}
