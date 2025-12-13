using System;
using Code.InteractionSystem;
using Code.Network.Stream.Data;
using Code.Utility;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Code.Network.Stream
{
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        [SerializeField] private StreamLoadBalancer streamLoadBalancer;

        [Header("UI")] [SerializeField] private RawImage rawImage;
        [SerializeField] private RawImage targetImage;

        [Header("Settings")]
        [SerializeField] private int maxResolution = 128;
        [SerializeField] private int jpgQuality = 35;
        [SerializeField] private float sendRate = 0.1f; // 10 раз в секунду

        [Header("Frame Change Detection")]
        [SerializeField] private bool enableFrameChangeDetection = true;
        [SerializeField] private int frameHashCheckInterval = 1; // Проверять каждый N-й кадр перед отправкой

        [Header("Stream Connection"), SerializeField]
        private StreamingLiteNetLibPeer streamConnection;
        [SerializeField] private StreamingLiteNetLibServer streamServer;

        [Header("Auto Quality")]
        [SerializeField] private bool enableAutoQuality = true;
        [SerializeField] private int targetFrameSizeBytes = 12_000;   // целевой размер кадра
        [SerializeField] private int minJpgQuality = 20;
        [SerializeField] private int maxJpgQuality = 70;
        [SerializeField] private float minSendRate = 0.05f;           // максимум 20 FPS
        [SerializeField] private float maxSendRate = 0.3f;            // минимум ~3 FPS
        [SerializeField] private float qualityAdjustInterval = 5f;    // раз в N секунд

        private int _frameCountForStats;
        private long _bytesForStats;
        private float _nextQualityAdjustTime;
        
        [Header("Debug")] [SerializeField] private bool showDebugLogs = true;

        private float _nextTime;
        private bool _isCapturing;
        private RenderTexture _tempRT;
        private Texture2D _readTex;
        private Texture2D _recvTex;

        private int _lastFrameId = 0;
        // Frame change detection
        private uint _lastFrameHash;
        private int _lastFrameLength;
        private int _frameCheckCounter;

        private int _savedJPGQuality = 35;
        
        private int SlotNumber => slotMachineInteractable.IDNumber;

        public event Action<Texture> OnApplyTexture;

        private void Start()
        {
            _savedJPGQuality = jpgQuality;
            streamConnection = StreamingLiteNetLibPeer.Instance;
            streamServer = StreamingLiteNetLibServer.Instance;
            
            streamConnection.OnFrameReceived += StreamConnectionOnOnFrameReceived;
            
            if (streamLoadBalancer == null)
                streamLoadBalancer = FindAnyObjectByType<StreamLoadBalancer>();
        }

        private void OnDestroy()
        {
            if (streamConnection != null)
                streamConnection.OnFrameReceived -= StreamConnectionOnOnFrameReceived;
        }
        
        private void StreamConnectionOnOnFrameReceived(StreamFrameData data)
        {
            if (data == null)
                return;

            if (data.SlotId == SlotNumber)
                ApplyReceivedTexture(data.Data);
        }

        // =================================================================================
        // ЛОГИКА СТРИМЕРА
        // =================================================================================

        private void Update()
        {
            if (!IsOwner) return;
            if (Time.time < _nextTime) return;
            if (_isCapturing) return;
            
            if (enableAutoQuality)
                TryAdjustQuality();
            
            if (rawImage == null || rawImage.texture == null || rawImage.texture.width < 16) return;

            _nextTime = Time.time + sendRate;
            Capture();
        }

        private void TryAdjustQuality()
        {
            if (Time.time < _nextQualityAdjustTime)
                return;

            _nextQualityAdjustTime = Time.time + qualityAdjustInterval;

            if (_frameCountForStats <= 0)
                return;

            float avgSize = (float)_bytesForStats / _frameCountForStats;

            // Сброс счётчиков
            _frameCountForStats = 0;
            _bytesForStats = 0;
            
            int dynamicTarget = targetFrameSizeBytes;
            if (enableAutoQuality && streamLoadBalancer != null)
                dynamicTarget = streamLoadBalancer.GetTargetFrameSize();
            
            // Отношение к целевому размеру
            float ratio = avgSize / dynamicTarget;

            // Немного "мёртвой зоны", чтобы не дёргалось
            if (ratio > 1.1f)
            {
                // Слишком жирные кадры -> режем качество и/или FPS
                jpgQuality = Mathf.Max(minJpgQuality, jpgQuality - 5);
                sendRate = Mathf.Min(maxSendRate, sendRate + 0.01f);
                maxResolution = Mathf.Max(128, Mathf.Clamp(maxResolution - 32, 128, 512));

                if (showDebugLogs)
                    Debug.Log($"[AutoQuality] Decrease quality: avg={avgSize:F0} bytes, jpg={jpgQuality}, sendRate={sendRate:F3}");
            }
            else if (ratio < 0.7f)
            {
                // Можно поднять качество / FPS
                jpgQuality = Mathf.Min(maxJpgQuality, jpgQuality + 5);
                sendRate = Mathf.Max(minSendRate, sendRate - 0.01f);
                maxResolution = Mathf.Max(128, Mathf.Clamp(maxResolution + 32, 128, 512));

                if (showDebugLogs)
                    Debug.Log($"[AutoQuality] Increase quality: avg={avgSize:F0} bytes, jpg={jpgQuality}, sendRate={sendRate:F3}");
            }
        }
        
        private void Capture()
        {
            _isCapturing = true;
            Texture src = rawImage.texture;
            OnApplyTexture?.Invoke(src);

            // Ресайз
            float aspect = (float)src.width / src.height;
            int w = (src.width > src.height) ? maxResolution : Mathf.RoundToInt(maxResolution * aspect);
            int h = (src.width > src.height) ? Mathf.RoundToInt(maxResolution / aspect) : maxResolution;

            if (_tempRT == null || _tempRT.width != w || _tempRT.height != h)
            {
                if (_tempRT) RenderTexture.ReleaseTemporary(_tempRT);
                _tempRT = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            }

            Graphics.Blit(src, _tempRT);

            // Асинхронное чтение (чтобы не лагало)
            if (SystemInfo.supportsAsyncGPUReadback)
                AsyncGPUReadback.Request(_tempRT, 0, r => OnReadback(r, w, h));
            else
                SyncReadback(w, h);
        }

        private void OnReadback(AsyncGPUReadbackRequest req, int w, int h)
        {
            if (this == null) return;
            if (req.hasError)
            {
                _isCapturing = false;
                return;
            }

            PrepareReadTex(w, h);
            _readTex.SetPixelData(req.GetData<byte>(), 0);
            _readTex.Apply(false, false);

            byte[] jpgData = _readTex.EncodeToJPG(jpgQuality);
            Send(jpgData);
        }

        private void SyncReadback(int w, int h)
        {
            PrepareReadTex(w, h);
            var prev = RenderTexture.active;
            RenderTexture.active = _tempRT;
            _readTex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            _readTex.Apply(false, false);
            RenderTexture.active = prev;

            byte[] jpgData = _readTex.EncodeToJPG(jpgQuality);
            Send(jpgData);
        }

        private void PrepareReadTex(int w, int h)
        {
            if (_readTex == null || _readTex.width != w || _readTex.height != h)
            {
                if (_readTex) Destroy(_readTex);
                _readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }
        }

        private void Send(byte[] data)
        {
            if (!enableFrameChangeDetection)
            {
                RegisterFrameStats(data.Length);
                SendFrameInternal(data);
                return;
            }

            if (IsFrameDuplicate(data))
            {
                if (showDebugLogs) Debug.Log($"[Client] Frame skipped (duplicate) - {data.Length} bytes");
                _isCapturing = false;
                return;
            }

            RegisterFrameStats(data.Length);
            SendFrameInternal(data);
        }

        private void RegisterFrameStats(int sizeBytes)
        {
            _frameCountForStats++;
            _bytesForStats += sizeBytes;
        }
        
        private bool IsFrameDuplicate(byte[] currentData)
        {
            if (currentData == null || currentData.Length == 0)
            {
                _lastFrameHash = 0;
                _lastFrameLength = 0;
                _frameCheckCounter = 0;
                return false;
            }

            // Быстрая проверка по длине
            if (currentData.Length != _lastFrameLength)
            {
                _lastFrameLength = currentData.Length;
                _lastFrameHash = CalculateSampleHash(currentData);
                _frameCheckCounter = 0;
                return false;
            }

            _frameCheckCounter++;

            // Только каждый N-й кадр считаем хеш
            if (_frameCheckCounter >= frameHashCheckInterval)
            {
                uint currentHash = CalculateSampleHash(currentData);
                bool isDuplicate = currentHash == _lastFrameHash;

                _lastFrameHash = currentHash;
                _frameCheckCounter = 0;

                return isDuplicate;
            }

            return false;
        }

        private uint CalculateSampleHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            const int sampleSize = 256;
            int len = Mathf.Min(sampleSize, data.Length);

            uint hash = 5381;
            for (int i = 0; i < len; i++)
                hash = ((hash << 5) + hash) ^ data[i];

            return hash;
        }

        private void SendFrameInternal(byte[] data)
        {
            if (showDebugLogs) Debug.Log($"[Client] Sending frame {data.Length} bytes");

            if (streamConnection != null && streamConnection.IsConnected)
            {
                streamConnection.SendStreamFrame(new StreamFrameData()
                {
                    FrameId = _lastFrameId,
                    Data = data,
                    SlotId = SlotNumber,
                    StreamerId = InstanceFinder.ClientManager.Connection.ClientId
                });
                _lastFrameId++;
            }

            _isCapturing = false;
        }

        // =================================================================================
        // ПРИЕМ НА КЛИЕНТЕ
        // =================================================================================

        private void ApplyReceivedTexture(byte[] data)
        {
            if (showDebugLogs) Debug.Log($"[Viewer] Frame Received! {data.Length} bytes");

            if (targetImage == null) return;
            if (_recvTex == null) _recvTex = new Texture2D(2, 2);

            if (_recvTex.LoadImage(data))
            {
                targetImage.texture = _recvTex;
                targetImage.color = Color.white;
                ImageUtility.AdjustAspect(targetImage);
                OnApplyTexture?.Invoke(_recvTex);
            }
        }

        // =================================================================================
        // СТАНДАРТНЫЕ МЕТОДЫ FISHNET
        // =================================================================================

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Настройка видимости
            if (IsOwner)
            {
                if (targetImage) targetImage.gameObject.SetActive(false);
            }
            else
            {
                if (targetImage)
                {
                    targetImage.gameObject.SetActive(true);
                    targetImage.color = Color.clear; // Прячем до первого кадра
                }
            }
            
            if (Owner.ClientId != -1)
            {
                targetImage.gameObject.SetActive(true);
                OnNewObserver(ClientManager.Connection);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void OnNewObserver(NetworkConnection newObserver)
        {
            if (newObserver == null) return;
            streamServer.OnPlayerObserverSlot(newObserver.ClientId, SlotNumber);
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (IsOwner)
            {
                if (showDebugLogs) Debug.Log($"[Client] Я владелец ({ObjectId}). Начинаю стрим.");
                _isCapturing = false;
                streamLoadBalancer?.RegisterStream();
                _lastFrameId = 0;
            }

            if (Owner.ClientId == -1)
            {
                targetImage.gameObject.SetActive(false);
                streamLoadBalancer?.UnregisterStream();
            }
            else if (!IsOwner)
            {
                targetImage.gameObject.SetActive(true);
                streamLoadBalancer?.UnregisterStream();
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            
            if (IsOwner)
                streamLoadBalancer?.UnregisterStream();
            
            if (_tempRT) RenderTexture.ReleaseTemporary(_tempRT);
            if (_readTex) Destroy(_readTex);
            if (_recvTex) Destroy(_recvTex);
            targetImage.gameObject.SetActive(false);

            _lastFrameHash = 0;
            _frameCheckCounter = 0;
        }
        
        // API
        public void SetQualitySettings(float res, int quality)
        {
            jpgQuality = quality;
        }

        public void ResetQualitySettings()
        {
            jpgQuality = _savedJPGQuality;
        }

        public void SetTexture(RawImage i)
        {
            rawImage = i;
        }

        public void ClearTexture()
        {
            rawImage = null;
        }
    }
}