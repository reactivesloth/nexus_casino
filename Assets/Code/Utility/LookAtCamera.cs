using UnityEngine;

namespace Code.Utility
{
    public sealed class LookAtCamera : MonoBehaviour
    {
        private Transform _tr;
        private Transform _cam;

        private void Awake()
        {
            _tr = transform;
            var cam = Camera.main;
            _cam = cam != null ? cam.transform : null;
        }

        private void LateUpdate()
        {
            if (_cam == null)
            {
                var cam = Camera.main;
                _cam = cam != null ? cam.transform : null;
                if (_cam == null) return;
            }

            _tr.LookAt(_cam.position, Vector3.up);
        }
    }
}