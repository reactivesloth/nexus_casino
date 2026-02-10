using System;
using Code.Player;
using UnityEngine;
using UnityEngine.Events;

namespace Code.Utility
{
    public class LocalDistanceObserverCondition: MonoBehaviour
    {
        [SerializeField] private float visibleDistance = 5f;
        [SerializeField] private bool requireMainCameraVisible = true;

        public UnityEvent onVisible;
        public UnityEvent onNotVisible;
        
        private bool _isVisible;

        private void Update()
        {
            UpdateVisible();
        }

        private void UpdateVisible()
        {
            var prevIsVisible = _isVisible;

            if (PlayerMovementController.LocalInstance == null)
            {
                _isVisible = false;
                return;
            }
            
            var playerTransform = PlayerMovementController.LocalInstance.transform;
            if(playerTransform == null)
            {
                _isVisible = false;
                return;
            }
            var playerDistance = Vector3.Distance(transform.position, playerTransform.position);
            _isVisible = playerDistance <= visibleDistance;
            
            if(prevIsVisible != _isVisible)
                OnVisibleChanged();
        }
        
        private void OnVisibleChanged()
        {
            if(_isVisible)
                OnBecameLocalVisible();
            else
                OnBecameLocalInvisible();
        }
        
        private void OnBecameLocalVisible()
        {
            onVisible?.Invoke();
        }
        
        private void OnBecameLocalInvisible()
        {
            onNotVisible?.Invoke();
        }
    }
}