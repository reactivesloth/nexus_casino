using System;
using System.Collections;
using System.Buffers;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Оптимизированная версия передачи Texture2D-стрима.
    /// • Пул RenderTexture и Texture2D для уменьшения аллокаций.
    /// • Один RPC с фрагментацией под MTU (FragmentationPipelineStage).
    /// • GPU-базированный флип через UV Scale.
    /// • Возможность адаптивного FPS и downscale.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [SerializeField] private RawImage rawImage;

        [Header("Render to Settings")] [SerializeField]
        private MeshRenderer computerMeshRenderer;

        [SerializeField] private int materialIndex;

        [Header("Stream Quality")] [SerializeField, Min(0.1f)]
        private float sendMaxFps = 12f;

        [SerializeField, Range(0, 1)] float sendMaxFramePercent = 0.1f;
        [SerializeField, Min(0.1f)] private float receiveMaxFps = 12f;
        [SerializeField, Range(0f, 1f)] private float receiveMaxFramePercent = 0.1f;

        [SerializeField, Range(0.1f, 1f)] private float downscale = 0.5f;
        [SerializeField] private bool useJpg = true;
        [SerializeField, Range(10, 100)] private int jpgQuality = 70;
        [SerializeField] private bool skipDuplicateFrames = true;

        [Header("LZ4")] [SerializeField] private bool lz4Compress = false;
        [SerializeField] private LZ4Level lz4Level = LZ4Level.L00_FAST;

        [Header("Networking")] [SerializeField]
        private bool hostIsOwnerOnStart = true;

        // Пулы и буферы
        private RenderTexture _rt;
        private Texture2D _readTex;
        private Texture2D _recvTex;
        private Hash128 _lastHash;

        private Coroutine _sendLoop;

        private Color _savedColor;
        private Texture _savedTexture;

        private float _currentResiveInterval = 0;

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

            if (iWasOwner && !iAmOwner)
            {
                StopSendLoop();
            }
            else if (iAmOwner)
            {
                StartSendLoop();
            }

            if (Owner == null || OwnerId == -1)
            {
                ShowIdleTexture();
                StopSendLoop();
            }
        }

        private void OnEnable()
        {
            _currentResiveInterval = 0;
            var mat = computerMeshRenderer.materials[materialIndex];
            _savedTexture = mat.GetTexture("_BaseMap");
            _savedColor = mat.GetColor("_BaseColor");

            if (IsOwner) StartSendLoop();
            else if (Owner == null) ShowIdleTexture();
        }

        private void Update()
        {
            _currentResiveInterval += Time.deltaTime;
        }

        private void OnDisable()
        {
            StopSendLoop();
            ReleaseResources();
            _currentResiveInterval = 0;
        }

        private void StartSendLoop()
        {
            if (_sendLoop == null)
                _sendLoop = StartCoroutine(SendLoop());
        }

        private void StopSendLoop()
        {
            if (_sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }
        }

        private void ReleaseResources()
        {
            if (_rt != null)
            {
                _rt.Release();
                _rt = null;
            }

            if (_readTex != null)
            {
                Destroy(_readTex);
                _readTex = null;
            }

            if (_recvTex != null)
            {
                Destroy(_recvTex);
                _recvTex = null;
            }
        }


        private void ShowIdleTexture()
        {
            if (computerMeshRenderer == null ||
                computerMeshRenderer.materials == null ||
                materialIndex < 0 ||
                materialIndex >= computerMeshRenderer.materials.Length) return;

            var mat = computerMeshRenderer.materials[materialIndex];
            mat.SetTexture("_BaseMap", _savedTexture);
            mat.SetColor("_BaseColor", _savedColor);
        }
        
        private IEnumerator SendLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(GetWait(sendMaxFps, sendMaxFramePercent));
                CaptureAndSend();
            }
        }


        private void CaptureAndSend()
        {
            if (rawImage == null || rawImage.texture == null)
                return;

            int w = Mathf.RoundToInt(rawImage.texture.width * downscale);
            int h = Mathf.RoundToInt(rawImage.texture.height * downscale);

            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);

                if (_readTex == null || _readTex.width != w || _readTex.height != h)
                    _readTex = new Texture2D(w, h, TextureFormat.RGB24, false);
            }

            Graphics.Blit(rawImage.texture, _rt);
            RenderTexture.active = _rt;
            _readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            RenderTexture.active = null;

            byte[] encoded = useJpg
                ? _readTex.EncodeToJPG(jpgQuality)
                : _readTex.EncodeToPNG();

            if (skipDuplicateFrames)
            {
                var hsh = Hash128.Compute(encoded);
                if (hsh == _lastHash) return;
                _lastHash = hsh;
            }

            if (lz4Compress)
                encoded = LZ4Pickler.Pickle(encoded, lz4Level);

            UploadFrame(encoded, w, h);
        }

        [ServerRpc(RequireOwnership = false, DataLength = 10_000)]
        private void UploadFrame(byte[] data, int width, int height)
        {
            RelayFrame(data, width, height);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true, DataLength = 10_000)]
        private void RelayFrame(byte[] data, int width, int height)
        {
            var wait = GetWait(receiveMaxFps, receiveMaxFramePercent);

            if (IsOwner || _currentResiveInterval < wait)
                return;

            if (Owner == null || OwnerId == -1)
            {
                ShowIdleTexture();
                return;
            }

            _currentResiveInterval = 0;
            byte[] raw = data;
            if (lz4Compress)
                raw = LZ4Pickler.Unpickle(raw);

            ApplyImage(raw, width, height);
        }

        private void ApplyImage(byte[] bytes, int width, int height)
        {
            if (bytes == null || bytes.Length == 0)
                return;

            // Инициализация приёма
            if (_recvTex == null || _recvTex.width != width || _recvTex.height != height)
            {
                if (_recvTex != null) Destroy(_recvTex);
                _recvTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            }

            // Загрузка JPEG/PNG
            if (!_recvTex.LoadImage(bytes, false))
                return;

            // Назначение текстуры и flip через UV
            var mat = computerMeshRenderer.materials[materialIndex];
            mat.SetTexture("_BaseMap", _recvTex);
            mat.SetTextureScale("_BaseMap", new Vector2(1, -1));
            mat.SetTextureOffset("_BaseMap", new Vector2(0, 1));
            mat.SetColor("_BaseColor", Color.white);
        }

        public void SetTexture()
        {
            rawImage = gameObject.GetComponentInChildren<RawImage>(true);
        }

        public void ClearTexture()
        {
            if (rawImage == null) return;

            Destroy(rawImage.texture);
            rawImage.texture = null;
            rawImage = null;
        }

        public float GetWait(float maxFrameRate, float percent)
        {
            // Определяем текущий FPS игры
            var currentGameFps = 1f / Time.deltaTime;

            // Рассчитываем максимально допустимое количество кадров для стрима
            var maxAllowedFps = Mathf.Min(currentGameFps, currentGameFps * percent);

            // Обновляем задержку между кадрами стрима в зависимости от FPS игры
            var targetStreamFps = Mathf.Min(maxFrameRate, maxAllowedFps);
            return 1f / targetStreamFps;
        }
    }
}