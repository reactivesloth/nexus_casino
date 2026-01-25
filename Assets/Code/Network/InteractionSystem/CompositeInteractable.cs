using System;
using UnityEngine;

namespace Code.Network.InteractionSystem
{
    public class CompositeInteractable : Interactable
    {
        [Header("Children to interact with")]
        [SerializeField, Tooltip("Все дочерние Interactable, которые запускаются одним нажатием.")]
        private Interactable[] children;
        
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
