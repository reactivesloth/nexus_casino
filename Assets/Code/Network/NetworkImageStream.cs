using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Utility;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using K4os.Compression.LZ4;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Передача Texture2D-стрима.
    /// • Без утечек материалов (MaterialPropertyBlock).
    /// • Один RPC с фрагментацией.
    /// • Адаптивная частота отправки/приёма.
    /// • Осторожно с ресурсами и null'ами.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [Header("Source UI")]
        [SerializeField] private RawImage rawImage;

        [Header("Render target")]
        [SerializeField] private RawImage targetImage;
        
        //[SerializeField] private MeshRenderer computerMeshRenderer;
        //[SerializeField] private int materialIndex = 0;

        [Header("Stream Quality")]
        [SerializeField, Min(0.1f)] private float sendMaxFps = 12f;
        [SerializeField, Range(0f, 1f)] private float sendMaxFramePercent = 0.1f;
        [SerializeField, Min(0.1f)] private float receiveMaxFps = 12f;
        [SerializeField, Range(0f, 1f)] private float receiveMaxFramePercent = 0.1f;

        [SerializeField, Range(0.1f, 1f)] private float downscale = 0.5f;
        [SerializeField] private bool useJpg = true;
        [SerializeField, Range(10, 100)] private int jpgQuality = 70;
        [SerializeField] private bool skipDuplicateFrames = true;

        [Header("LZ4")]
        [SerializeField] private bool lz4Compress = false;
        [SerializeField] private LZ4Level lz4Level = LZ4Level.L00_FAST;

        [Header("Networking")]
        [SerializeField] private bool hostIsOwnerOnStart = true;

        private float _currentDownscale;
        private int _currentJpgQuality;
        
        // GPU/CPU ресурсы
        private RenderTexture _rt;
        private Texture2D _readTex;      // CPU readback для отправки
        private Texture2D _recvTex;      // CPU decode для приёма
        private Hash128 _lastHash;

        private Coroutine _sendLoop;

        private float _currentReceiveInterval;
        
        public event Action<Texture> OnApplyTexture;

        private void Awake()
        {
            ResetQualitySettings();
        }

        public void SetQualitySettings(float down, int jpg)
        {
            _currentDownscale = down;
            _currentJpgQuality = jpg;
        }
        
        public void ResetQualitySettings()
        {
            _currentDownscale = downscale;
            _currentJpgQuality = jpgQuality;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (hostIsOwnerOnStart && InstanceFinder.SceneManager != null)
                InstanceFinder.SceneManager.OnClientLoadedStartScenes += OnClientReady;
        }

        private void OnClientReady(NetworkConnection conn, bool asServer)
        {
            if (!asServer)
                return;

            // Передаём владение первому подключившемуся (как было задумано).
            GiveOwnership(conn);

            if (InstanceFinder.SceneManager != null)
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
            var clientConn = (NetworkManager != null && NetworkManager.ClientManager != null)
                ? NetworkManager.ClientManager.Connection
                : null;

            bool iAmOwner = (Owner == clientConn);
            bool iWasOwner = (prev == clientConn);

            if (iWasOwner && !iAmOwner)
                StopSendLoop();
            else if (iAmOwner)
                StartSendLoop();

            if (Owner == null || OwnerId == -1)
            {
                StopSendLoop();
                ShowIdleTexture();
            }
        }

        private void OnEnable()
        {
            _currentReceiveInterval = 0f;

            if (targetImage == null)
                return;

            if (IsOwner)
                StartSendLoop();
            else if (Owner == null)
                ShowIdleTexture();
        }

        private void Update()
        {
            _currentReceiveInterval += Time.deltaTime;
        }

        private void OnDisable()
        {
            StopSendLoop();
            ReleaseResources();
            _currentReceiveInterval = 0f;
        }

        private void StartSendLoop()
        {
            if (_sendLoop == null && isActiveAndEnabled)
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
            if (targetImage == null)
                return;
            
            targetImage.gameObject.SetActive(false);
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
            if (!IsSpawned || !IsOwner)
                return;

            if (rawImage == null || rawImage.texture == null)
                return;

            int srcW = rawImage.texture.width;
            int srcH = rawImage.texture.height;
            if (srcW <= 0 || srcH <= 0) return;

            int w = Mathf.Max(1, Mathf.RoundToInt(srcW * _currentDownscale));
            int h = Mathf.Max(1, Mathf.RoundToInt(srcH * _currentDownscale));

            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                if (_rt != null) _rt.Release();
                _rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);

                if (_readTex == null || _readTex.width != w || _readTex.height != h)
                {
                    if (_readTex != null) Destroy(_readTex);
                    _readTex = new Texture2D(w, h, TextureFormat.RGB24, false);
                }
            }

            // Блит источника в RT
            Graphics.Blit(rawImage.texture, _rt);

            // CPU readback
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _readTex.Apply(false, false);
            RenderTexture.active = prev;

            byte[] encoded = useJpg
                ? _readTex.EncodeToJPG(_currentJpgQuality)
                : _readTex.EncodeToPNG();

            if (encoded == null || encoded.Length == 0)
                return;

            if (skipDuplicateFrames)
            {
                var hsh = Hash128.Compute(encoded);
                if (hsh == _lastHash) return;
                _lastHash = hsh;
            }

            if (lz4Compress)
                encoded = LZ4Pickler.Pickle(encoded, lz4Level);
            
            // Защита: объект может ещё не быть заспавнен/владельцем на этот кадр
            if (Owner != null && OwnerId != -1)
            {
                SendInChunks(encoded, w, h);
                //UploadFrame(encoded, w, h);
                OnApplyTexture?.Invoke(rawImage.texture);
            }
        }
        
        
        private int _currentFrameId = 0;
        private readonly Dictionary<int, List<byte[]>> _chunkBuffer = new();
        
        /// <summary>
        /// Разбиение на чанки по 1000 байт и отправка.
        /// </summary>
        private void SendInChunks(byte[] data, int width, int height)
        {
            const int CHUNK_SIZE = 1000;
            int totalChunks = Mathf.CeilToInt(data.Length / (float)CHUNK_SIZE);

            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * CHUNK_SIZE;
                int size = Mathf.Min(CHUNK_SIZE, data.Length - offset);
                byte[] chunk = new byte[size];
                Buffer.BlockCopy(data, offset, chunk, 0, size);

                UploadFrameChunk(chunk, width, height, i, totalChunks);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void UploadFrameChunk(byte[] chunk, int width, int height, int index, int total, Channel channel = Channel.Unreliable)
        {
            RelayFrameChunk(chunk, width, height, index, total);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = false)]
        private void RelayFrameChunk(byte[] chunk, int width, int height, int index, int total, Channel channel = Channel.Unreliable)
        {
            if (IsOwner) return;

            // накапливаем чанки
            if (!_chunkBuffer.TryGetValue(_currentFrameId, out List<byte[]> list))
            {
                list = new List<byte[]>(total);
                _chunkBuffer[_currentFrameId] = list;
            }

            // гарантируем порядок хранения
            if (list.Count <= index)
                list.Add(new byte[index - list.Count + 1]);

            list[index] = chunk;

            // когда все чанки получены — собрать
            if (list.Count == total && list.All(c => c != null))
            {
                int totalBytes = list.Sum(c => c.Length);
                byte[] full = new byte[totalBytes];
                int pos = 0;
                foreach (var c in list)
                {
                    Buffer.BlockCopy(c, 0, full, pos, c.Length);
                    pos += c.Length;
                }

                _chunkBuffer.Remove(_currentFrameId);

                byte[] raw = lz4Compress ? LZ4Pickler.Unpickle(full) : full;
                ApplyImage(raw, width, height);

                _currentFrameId++;
            }
        }
        
        /*[ServerRpc(RequireOwnership = false, DataLength = 15_000)]
        private void UploadFrame(byte[] data, int width, int height)
        {
            RelayFrame(data, width, height);
        }

        [ObserversRpc(ExcludeOwner = true, BufferLast = true, DataLength = 15_000)]
        private void RelayFrame(byte[] data, int width, int height)
        {
            if (IsOwner) // владелец не принимает свои же кадры
                return;

            float wait = GetWait(receiveMaxFps, receiveMaxFramePercent);
            if (_currentReceiveInterval < wait)
                return;

            if (Owner == null || OwnerId == -1)
            {
                ShowIdleTexture();
                return;
            }
            
            targetImage.gameObject.SetActive(true);

            _currentReceiveInterval = 0f;

            byte[] raw = data;
            if (lz4Compress)
                raw = K4os.Compression.LZ4.LZ4Pickler.Unpickle(raw);

            ApplyImage(raw, width, height);
        }*/

        private void ApplyImage(byte[] bytes, int width, int height)
        {
            if (targetImage == null || bytes == null || bytes.Length == 0)
                return;

            if (_recvTex == null || _recvTex.width != width || _recvTex.height != height)
            {
                if (_recvTex != null) Destroy(_recvTex);
                _recvTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _recvTex.filterMode = FilterMode.Bilinear; // или Trilinear
                _recvTex.wrapMode = TextureWrapMode.Clamp;
            }

            if (!_recvTex.LoadImage(bytes, false))
                return;

            targetImage.texture = _recvTex;
            ImageUtility.AdjustAspect(targetImage);
            
            OnApplyTexture?.Invoke(_recvTex);
            
            // На большинстве шейдеров Screen/Unlit можно флипать через матрицу/UV.
            // Если нужен явный флип: используйте шейдер с инверсией V, либо Mesh UV.
            // (В старом коде флип делался SetTextureScale/Offset — на PropertyBlock это не везде доступно.)
        }

        /// <summary>Переинициализирует ссылку на RawImage-источник.</summary>
        public void SetTexture(RawImage image)
        {
            rawImage = image;
        }

        /// <summary>Отключает стрим: не трогаем чужую Texture, просто убираем ссылку.</summary>
        public void ClearTexture()
        {
            rawImage = null;
        }

        /// <summary>Рассчитать задержку между кадрами под заданный лимит FPS и долю от игрового FPS.</summary>
        public float GetWait(float maxFrameRate, float percent)
        {
            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.0001f;
            float gameFps = 1f / dt;
            float allowedByPercent = gameFps * Mathf.Clamp01(percent);
            float targetFps = Mathf.Clamp(maxFrameRate, 0.1f, 240f);
            float finalFps = Mathf.Min(targetFps, allowedByPercent > 0.1f ? allowedByPercent : targetFps);
            return 1f / finalFps;
        }
    }
}
