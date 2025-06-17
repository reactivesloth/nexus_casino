using System;
using Code.Network;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Tests
{
    public class TestStreamHostCameraRender : NetworkBehaviour
    {
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private NetworkImageStream imageStream;
        
        private bool _isInitialized = false;
        private Transform _cameraTransform;

        private void Update()
        {
            if(!_isInitialized)
            {
                if (!imageStream.IsOwner)
                    return;

                var newCamera = new GameObject("Camera").AddComponent<Camera>();
                newCamera.CopyFrom(Camera.main);
                newCamera.targetTexture = renderTexture;
                _cameraTransform = newCamera.transform;

                _isInitialized = true;
            }
            else
            {
                _cameraTransform.SetPositionAndRotation(Camera.main.transform.position, Camera.main.transform.rotation);
            }
        }
    }
}
