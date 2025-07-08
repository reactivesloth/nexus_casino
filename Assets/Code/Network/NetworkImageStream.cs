using System;
using System.Collections;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Экран игрового автомата.
    /// • Без владельца — idle‑текстура.
    /// • С владельцем — стрим потоком.
    /// Исправлено назначение владельца: теперь оно выполняется чуть позже,
    /// чтобы клиент уже получил сценовый объект и ошибка SceneId not found не появлялась.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        /* ──────────── Инспектор ──────────── */

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
        [SerializeField, Min(256)] private int chunkSize = 1150;
        [SerializeField] private bool hostIsOwnerOnStart = true;

        /* ──────────── Runtime ──────────── */

        private Coroutine _sendLoop;
        private FrameAssembler _assembler;
        private Hash128 _lastHash;

        /* ========= Server ========= */

        public override void OnStartServer()
        {
            base.OnStartServer();
            
            if(hostIsOwnerOnStart)
                SceneManager.OnClientLoadedStartScenes += OnClientReady;
        }
        
        private void OnClientReady(NetworkConnection conn, bool asServer)
        {
            // Даём владение хосту по готовности
            if (!asServer) return;          
            GiveOwnership(conn);
            InstanceFinder.SceneManager.OnClientLoadedStartScenes -= OnClientReady;
        }

        /* ========= Client ========= */

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
            bool iAmOwner   = Owner == NetworkManager.ClientManager.Connection;
            bool iWasOwner  = prev == NetworkManager.ClientManager.Connection;

            if (iWasOwner && !iAmOwner && _sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }
            else if (iAmOwner && _sendLoop == null)
            {
                _sendLoop = StartCoroutine(SendLoop());
            }

            if (Owner == null)
                ShowIdleTexture();
        }

        /* ========= Unity ========= */

        private void OnEnable()
        {
            if (IsOwner) _sendLoop = StartCoroutine(SendLoop());
            else if (Owner == null) ShowIdleTexture();
        }

        private void OnDisable()
        {
            if (_sendLoop != null) StopCoroutine(_sendLoop);
            _sendLoop = null;
            _assembler = null;
        }

        /* ========= Idle ========= */

        private void ShowIdleTexture()
        {
            computerMeshRenderer.materials[materialIndex].SetTexture("_BaseMap", null);
            computerMeshRenderer.materials[materialIndex].SetColor("_BaseColor", Color.black);
        }

        /* ========= Send ========= */

        private IEnumerator SendLoop()
        {
            var wait = new WaitForSeconds(1f / fps);
            while (true) { yield return wait; CaptureAndSend(); }
        }

        private void CaptureAndSend()
        {
            if (rawImage.texture == null) return;
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
                if (hsh == _lastHash) { Destroy(tex); return; }
                _lastHash = hsh;
            }

            byte[] data = useJpg ? tex.EncodeToJPG(jpgQuality) : tex.EncodeToPNG();
            Destroy(tex);
            if (lz4Compress) data = LZ4Pickler.Pickle(data, lz4Level);
            int total = data.Length;
            for (int off = 0; off < total; off += chunkSize)
            {
                int len = Math.Min(chunkSize, total - off);
                var chunk = new byte[len];
                Buffer.BlockCopy(data, off, chunk, 0, len);
                UploadChunk(chunk, off, total, w, h);
            }
        }

        /* ========= RPCs ========= */

        [ServerRpc(RequireOwnership = false)]
        private void UploadChunk(byte[] chunk, int offset, int total, int width, int height) =>
            RelayChunk(chunk, offset, total, width, height);

        [ObserversRpc(ExcludeOwner = true)]
        private void RelayChunk(byte[] chunk, int offset, int total, int width, int height)
        {
            _assembler ??= new FrameAssembler(total);
            _assembler.Add(chunk, offset);
            if (!_assembler.IsComplete) return;
            var data = _assembler.Data;
            if (lz4Compress) data = LZ4Pickler.Unpickle(data);
            ApplyImage(data);
            _assembler = null;
        }

        /* ========= Receive ========= */

        private void ApplyImage(byte[] bytes)
        {
            // Загружаем изображение в исходную текстуру
            var originalTex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            originalTex.LoadImage(bytes, false);

            // Устанавливаем перевёрнутое изображение в материал
            var mat = computerMeshRenderer.materials[materialIndex];
            mat.SetTexture("_BaseMap", originalTex);
            mat.SetColor("_BaseColor", Color.white);
        }

        /* ========= Helper ========= */

        private sealed class FrameAssembler
        {
            private readonly byte[] _buffer;
            private int _received;
            public bool IsComplete => _received >= _buffer.Length;
            public byte[] Data => _buffer;
            public FrameAssembler(int size) => _buffer = new byte[size];
            public void Add(byte[] c, int off) { Buffer.BlockCopy(c, 0, _buffer, off, c.Length); _received += c.Length; }
        }
    }
}
