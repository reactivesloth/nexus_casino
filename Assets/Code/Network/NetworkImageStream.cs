using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [SerializeField] private RawImage rawImage;
        [Header("Render to Settings")]
        [SerializeField] private MeshRenderer computerMeshRenderer;
        [SerializeField] private int materialIndex;

        [Header("Stream Quality")]
        [SerializeField, Min(0.1f)] private float fps = 24f;
        [SerializeField, Range(0.1f, 1f)] private float downscale = 0.5f;
        [SerializeField] private bool useJpg = true;
        [SerializeField, Range(10, 100)] private int jpgQuality = 70;
        [SerializeField] private bool skipDuplicateFrames = true;

        [Header("LZ4")]
        [SerializeField] private bool lz4Compress = true;
        [SerializeField] private LZ4Level lz4Level = LZ4Level.L00_FAST;

        [Header("Networking")]
        [SerializeField] private bool hostIsOwnerOnStart = true;

        private Coroutine _sendLoop;
        private Hash128 _lastHash;
        private Texture2D _flippedTex;

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (hostIsOwnerOnStart)
                SceneManager.OnClientLoadedStartScenes += OnClientReady;
        }

        private void OnClientReady(NetworkConnection conn, bool asServer)
        {
            if (!asServer) return;
            GiveOwnership(conn);
            InstanceFinder.SceneManager.OnClientLoadedStartScenes -= OnClientReady;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ApplyOwnerState(null);
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            ApplyOwnerState(prevOwner);
        }

        private void ApplyOwnerState(NetworkConnection prev)
        {
            bool iAmOwner = Owner == NetworkManager.ClientManager.Connection;
            bool iWasOwner = prev == NetworkManager.ClientManager.Connection;

            if (iWasOwner && !iAmOwner && _sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }
            else if (iAmOwner && _sendLoop == null)
            {
                _sendLoop = StartCoroutine(SendLoop());
            }

            if (Owner == null || OwnerId == -1)
            {
                ShowIdleTexture();
                if (_sendLoop != null)
                {
                    StopCoroutine(_sendLoop);
                    _sendLoop = null;
                }
            }
        }

        private void OnEnable()
        {
            if (IsOwner) _sendLoop = StartCoroutine(SendLoop());
            else if (Owner == null) ShowIdleTexture();
        }

        private void OnDisable()
        {
            if (_sendLoop != null) StopCoroutine(_sendLoop);
            _sendLoop = null;

            if (_flippedTex != null)
            {
                Destroy(_flippedTex);
                _flippedTex = null;
            }
        }

        private void ShowIdleTexture()
        {
            if (computerMeshRenderer == null ||
                computerMeshRenderer.materials == null ||
                materialIndex < 0 ||
                materialIndex >= computerMeshRenderer.materials.Length)
            {
                Debug.LogWarning($"[NetworkImageStream] Invalid material index {materialIndex} or null renderer");
                return;
            }

            computerMeshRenderer.materials[materialIndex].SetTexture("_BaseMap", null);
            computerMeshRenderer.materials[materialIndex].SetColor("_BaseColor", Color.black);
        }

        private IEnumerator SendLoop()
        {
            var wait = new WaitForSeconds(1f / fps);
            while (true) { yield return wait; CaptureAndSend(); }
        }

        private void CaptureAndSend()
        {
            if (rawImage == null || rawImage.texture == null) return;

            int w = Mathf.RoundToInt(rawImage.texture.width * downscale);
            int h = Mathf.RoundToInt(rawImage.texture.height * downscale);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rawImage.texture, rt);
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(false);
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);

            if (skipDuplicateFrames)
            {
                var hsh = Hash128.Compute(tex.GetRawTextureData());
                if (hsh == _lastHash)
                {
                    Destroy(tex);
                    return;
                }
                _lastHash = hsh;
            }

            byte[] data = useJpg ? tex.EncodeToJPG(jpgQuality) : tex.EncodeToPNG();
            Destroy(tex);
            if (lz4Compress) data = LZ4Pickler.Pickle(data, lz4Level);

            UploadFrame(data);
        }

        [ServerRpc(RequireOwnership = false, DataLength = 10_000)]
        private void UploadFrame(byte[] data)
        {
            RelayFrame(data);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true, DataLength = 10_000)]
        private void RelayFrame(byte[] data)
        {
            if (Owner == null || OwnerId == -1)
            {
                ShowIdleTexture();
                return;
            }

            if (lz4Compress) data = LZ4Pickler.Unpickle(data);
            ApplyImage(data);
        }

        private void ApplyImage(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogWarning("[NetworkImageStream] Received null or empty image data");
                return;
            }

            var originalTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!originalTex.LoadImage(bytes, false))
            {
                Debug.LogWarning("[NetworkImageStream] Failed to load image data");
                Destroy(originalTex);
                return;
            }

            var width = originalTex.width;
            var height = originalTex.height;

            if (!_flippedTex || _flippedTex.width != width || _flippedTex.height != height)
            {
                if (_flippedTex)
                    Destroy(_flippedTex);
                _flippedTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            Color[] row = new Color[width];
            for (int y = 0; y < height; y++)
            {
                row = originalTex.GetPixels(0, y, width, 1);
                _flippedTex.SetPixels(0, height - y - 1, width, 1, row);
            }

            _flippedTex.Apply();

            if (computerMeshRenderer == null ||
                computerMeshRenderer.materials == null ||
                materialIndex < 0 ||
                materialIndex >= computerMeshRenderer.materials.Length)
            {
                Debug.LogWarning($"[NetworkImageStream] Invalid material index {materialIndex} or null renderer");
                Destroy(originalTex);
                return;
            }

            var mat = computerMeshRenderer.materials[materialIndex];
            mat.SetTexture("_BaseMap", _flippedTex);
            mat.SetColor("_BaseColor", Color.white);

            Destroy(originalTex);
        }
    }
}