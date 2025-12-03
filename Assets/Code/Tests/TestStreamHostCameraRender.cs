using Code.Network;
using Code.Network.Stream;
using FishNet.Object;
using UnityEngine;

namespace Code.Tests
{
    public class TestStreamHostCameraRender : NetworkBehaviour
    {
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private NetworkImageStream imageStream;

        private Camera _cam;
        private Transform _camTf;
        private bool _initialized;

        private void OnDisable()
        {
            CleanupCamera();
            _initialized = false;
        }

        private void OnDestroy()
        {
            CleanupCamera();
            _initialized = false;
        }

        private void CleanupCamera()
        {
            if (_cam != null)
            {
                if (_cam.targetTexture == renderTexture) _cam.targetTexture = null;
                Destroy(_cam.gameObject);
                _cam = null;
                _camTf = null;
            }
        }

        private void Update()
        {
            if (!_initialized)
            {
                if (imageStream == null || !imageStream.IsOwner) return;

                var main = Camera.main;
                if (main == null || renderTexture == null) return;

                _cam = new GameObject("HostStreamCamera").AddComponent<Camera>();
                _cam.CopyFrom(main);
                _cam.targetTexture = renderTexture;
                _camTf = _cam.transform;

                _initialized = true;
            }
            else
            {
                var main = Camera.main;
                if (main != null && _camTf != null)
                    _camTf.SetPositionAndRotation(main.transform.position, main.transform.rotation);
            }
        }
    }
}