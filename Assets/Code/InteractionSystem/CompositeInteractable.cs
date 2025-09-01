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

        public override string InteractionPrompt
        {
            get
            {
                if (!IsEnabled) return "Disabled";
                if (IsOccupied) return ManualRelease ? "Press E to end" : "Occupied";

                if (children == null || children.Length == 0)
                    return base.InteractionPrompt;

                int available = 0;
                for (int i = 0; i < children.Length; i++)
                {
                    var c = children[i];
                    if (c != null && c.IsEnabled && !c.IsOccupied) available++;
                }
                if (available == 0) return base.InteractionPrompt;

                string result = string.Empty;
                int appended = 0;
                for (int i = 0; i < children.Length; i++)
                {
                    var c = children[i];
                    if (c == null || !c.IsEnabled || c.IsOccupied) continue;
                    if (appended > 0) result += " + ";
                    result += c.InteractionPrompt;
                    appended++;
                }
                return appended > 0 ? result : base.InteractionPrompt;
            }
        }

        protected internal override void OnInteract_Server(NetworkConnection conn, bool force)
        {
            if (children == null) return;

            for (int i = 0; i < children.Length; i++)
            {
                var child = children[i];
                if (child == null) continue;
                if (force)
                    child.ServerForceInteract(conn);
                else
                    child.OnInteract_Server(conn, false);
            }
        }

        protected internal override void OnEndInteract_Server(NetworkConnection conn)
        {
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child == null) continue;

                    if (child.ManualRelease)
                    {
                        child.OnEndInteract_Server(conn);
                    }
                }
            }

            base.OnEndInteract_Server(conn);

            bool anyChildOccupied = false;
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child != null && child.IsOccupied) { anyChildOccupied = true; break; }
                }
            }
            if (IsOccupied || anyChildOccupied)
                ReleaseAll();
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

        [Server]
        public void ReleaseAll()
        {
            if (children != null)
            {
                for (int i = 0; i < children.Length; i++)
                {
                    var child = children[i];
                    if (child != null)
                        child.ReleaseInteractable();
                }
            }
            ReleaseInteractable();
        }
    }
}
