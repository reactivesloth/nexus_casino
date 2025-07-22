using System;
using UnityEngine;

namespace Code.Utility
{
    public class LookAtCamera : MonoBehaviour
    {
        private Transform _transform;

        private void Awake()
        {
            _transform = GetComponent<Transform>();
        }

        private void Update()
        {
            _transform.LookAt(Camera.main.transform.position);
        }
    }
}