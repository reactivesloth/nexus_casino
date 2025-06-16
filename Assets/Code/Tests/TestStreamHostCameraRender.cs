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

        private void Update()
        {
            if(_isInitialized) 
                return;
            
            if(!imageStream.IsOwner)
                return;
            
            Camera.main.targetTexture = renderTexture;
            _isInitialized = true;
        }
    }
}
