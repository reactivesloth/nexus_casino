using System;
using System.Collections;
using Code.InteractionSystem;
using Code.Utility;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network.Stream
{
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        
        [Header("Source UI")]
        [SerializeField] private RawImage rawImage;

        [Header("Render target")]
        [SerializeField] private RawImage targetImage;
        
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

        [Header("UDP Streaming")]
        [SerializeField] private bool useUdpStreaming = true;
        [SerializeField] private StreamingLiteNetLibPeer streamingPeer; // 🎯 Один класс!

        [Header("Networking")]
        [SerializeField] private bool hostIsOwnerOnStart = true;

        private float _currentDownscale;
        private int _currentJpgQuality;
        
        private RenderTexture _rt;
        private Texture2D _readTex;
        private Texture2D _recvTex;
        private Hash128 _lastHash;

        private Coroutine _sendLoop;
        private float _currentReceiveInterval;
        
        private int SlotNumber => slotMachineInteractable.IDNumber;
        
        public event Action<Texture> OnApplyTexture;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            slotMachineInteractable = GetComponent<SlotMachineInteractable>();
        }
#endif
        
        private void Awake()
        {
            ResetQualitySettings();
            streamingPeer = FindAnyObjectByType<StreamingLiteNetLibPeer>();
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
            
            // 📥 Если используем UDP, периодически проверяем новые фреймы
            if (!IsOwner && useUdpStreaming && streamingPeer != null && streamingPeer.IsConnected)
            {
                CheckUdpFrameReceived();
            }
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

            Graphics.Blit(rawImage.texture, _rt);

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
            
            if (Owner != null && OwnerId != -1)
            {
                // 📤 Отправляем через UDP
                if (useUdpStreaming && streamingPeer != null && streamingPeer.IsConnected)
                {
                    Debug.Log("[NetworkImageStream] Sending to server");
                    streamingPeer.SendStreamFrame((byte)SlotNumber, encoded);
                }
                else
                {
                    // ❌ Fallback больше не нужен, только UDP
                }
                
                OnApplyTexture?.Invoke(rawImage.texture);
            }
        }

        /// <summary>
        /// Проверить получение фрейма из UDP буфера (вызывается из Update).
        /// </summary>
        private void CheckUdpFrameReceived()
        {
            if (Owner == null)
                return;

            int streamClientId = Owner.ClientId;

            if (!streamingPeer.TryGetStreamFrame(streamClientId, (byte)SlotNumber, out var frameData))
                return;

            float wait = GetWait(receiveMaxFps, receiveMaxFramePercent);
            if (_currentReceiveInterval < wait)
                return;

            _currentReceiveInterval = 0f;

            targetImage.gameObject.SetActive(true);

            byte[] raw = frameData;
            if (lz4Compress)
            {
                try
                {
                    raw = LZ4Pickler.Unpickle(raw);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[NetworkImageStream] LZ4 unpickle error: {ex.Message}");
                    return;
                }
            }

            // Обработаем фрейм — нужно декодировать размеры из первых 8 байт
            // Но UDP отправляет только сырые данные изображения!
            // Нужно получить размеры из других источников или отправить их в пакете
            
            // Для простоты берём последний известный размер
            if (_recvTex != null)
            {
                ApplyImage(raw, _recvTex.width, _recvTex.height);
            }
            else
            {
                // Первый фрейм — пробуем загрузить как есть
                ApplyImageAutoSize(raw);
            }
        }

        private void ApplyImage(byte[] bytes, int width, int height)
        {
            if (targetImage == null || bytes == null || bytes.Length == 0)
                return;

            if (_recvTex == null || _recvTex.width != width || _recvTex.height != height)
            {
                if (_recvTex != null) Destroy(_recvTex);
                _recvTex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                _recvTex.filterMode = FilterMode.Bilinear;
                _recvTex.wrapMode = TextureWrapMode.Clamp;
            }
            
            if (!_recvTex.LoadImage(bytes, false))
                return;

            targetImage.texture = _recvTex;
            ImageUtility.AdjustAspect(targetImage);
            
            OnApplyTexture?.Invoke(_recvTex);
        }

        private void ApplyImageAutoSize(byte[] bytes)
        {
            if (targetImage == null || bytes == null || bytes.Length == 0)
                return;

            // LoadImage автоматически определит размер
            if (_recvTex != null) Destroy(_recvTex);
            _recvTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _recvTex.filterMode = FilterMode.Bilinear;
            _recvTex.wrapMode = TextureWrapMode.Clamp;
            
            if (!_recvTex.LoadImage(bytes, false))
                return;

            targetImage.texture = _recvTex;
            ImageUtility.AdjustAspect(targetImage);
            
            OnApplyTexture?.Invoke(_recvTex);
        }

        public void SetTexture(RawImage image)
        {
            rawImage = image;
        }

        public void ClearTexture()
        {
            rawImage = null;
        }

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