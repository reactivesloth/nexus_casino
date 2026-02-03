using System;
using Code.Network.InteractionSystem;
using Code.Network.Stream.Data;
using Code.Player;
using Code.Utility;
using PurrNet;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Code.Network.Stream
{
    public sealed class NetworkImageStream : MonoBehaviour
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

        [Header("Stream Connection"), SerializeField] private StreamingLiteNetLibPeer streamConnection;
        [SerializeField] private StreamingLiteNetLibServer streamServer;

        [Header("Auto Quality")]
        [SerializeField] private bool enableAutoQuality = true;
        [SerializeField] private int targetFrameSizeBytes = 12_000;   // целевой размер кадра
        [SerializeField] private int minJpgQuality = 20;
        [SerializeField] private int maxJpgQuality = 70;
        [SerializeField] private float minSendRate = 0.05f;           // максимум 20 FPS
        [SerializeField] private float maxSendRate = 0.3f;            // минимум ~3 FPS
        [SerializeField] private float qualityAdjustInterval = 5f;    // раз в N секунд

        [Header("Visible Settings")] 
        [SerializeField] private float visibleDistance = 5f;
        [SerializeField] private bool requireMainCameraVisible = true;
        
        private int _frameCountForStats;
        private long _bytesForStats;
        private float _nextQualityAdjustTime;
        
        [Header("Debug")] [SerializeField] private bool showDebugLogs = true;

        private float _nextTime;
        private bool _isCapturing;
        private RenderTexture _tempRT;
        private Texture2D _readTex;
        private Texture2D _recvTex;

        private int _lastSentFrameId = 0;
        private int _lastRecvFrameId = 0;
        // Frame change detection
        private uint _lastFrameHash;
        private int _lastFrameLength;
        private int _frameCheckCounter;

        private int _savedJPGQuality = 35;

        private bool _isVisible;
        
        private int SlotNumber => slotMachineInteractable.IDNumber;

        public event Action<Texture> OnApplyTexture;
        
        public Texture2D RecvTexture => _recvTex;
        public bool IsOwner => slotMachineInteractable.isOwner;
        public bool HasOwner => slotMachineInteractable.hasOwner;

        private void Start()
        {
            _savedJPGQuality = jpgQuality;
            streamConnection ??= GetComponent<StreamingLiteNetLibPeer>();
            streamServer = StreamingLiteNetLibServer.Instance;
            
            streamConnection.OnFrameReceived += StreamConnectionOnOnFrameReceived;
            
            
            if (streamLoadBalancer == null)
                streamLoadBalancer = FindAnyObjectByType<StreamLoadBalancer>();
            
            slotMachineInteractable.isOccupied.onChanged += OnOccupierChanged;
        }
        
        private void OnDestroy()
        {
            if (streamConnection != null)
                streamConnection.OnFrameReceived -= StreamConnectionOnOnFrameReceived;
            
            slotMachineInteractable.isOccupied.onChanged -= OnOccupierChanged;
        }
        
        private void StreamConnectionOnOnFrameReceived(StreamFrameData data)
        {
            if (data == null || data.SlotId != SlotNumber)
                return;

            if (data.FrameId < _lastRecvFrameId)
                return;
            
            _lastRecvFrameId = data.FrameId;
            ApplyReceivedTexture(data.Data);
        }

        // =================================================================================
        // ЛОГИКА СТРИМЕРА
        // =================================================================================

        private void Update()
        {
            UpdateVisible();
            UpdateStream();
        }

        private void UpdateVisible()
        {
            var prevIsVisible = _isVisible;
            
            var playerTransform = PlayerMovementController.LocalInstance?.transform;
            if(playerTransform == null)
            {
                _isVisible = false;
                return;
            }
            var playerDistance = Vector3.Distance(transform.position, playerTransform.position);
            _isVisible = playerDistance <= visibleDistance;
            
            if(prevIsVisible != _isVisible)
                OnVisibleChanged();
        }

        private void UpdateStream()
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
                    FrameId = _lastSentFrameId,
                    Data = data,
                    SlotId = SlotNumber
                });
                _lastSentFrameId++;
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
        // Network
        // =================================================================================
        
        private void OnOccupierChanged(bool isOccupied)
        {
            OnVisibleChanged();
        }

        private void OnVisibleChanged()
        {
            if(_isVisible)
                OnBecameVisible();
            else
                OnBecameInvisible();
        }
        
        private void OnBecameVisible()
        {
            _lastRecvFrameId = 0;
            
            // Настройка видимости
            if (IsOwner)
            {
                if (showDebugLogs) Debug.Log($"[Client] Я владелец. Начинаю стрим.");
                _isCapturing = false;
                streamLoadBalancer?.RegisterStream();
                _lastSentFrameId = 0;
                
                targetImage.gameObject.SetActive(false);
            }
            else
            {
                streamLoadBalancer?.UnregisterStream();
                
                targetImage.gameObject.SetActive(true);
            }
            
            if (!HasOwner)
            {
                streamLoadBalancer?.UnregisterStream();
                
                targetImage.gameObject.SetActive(false);
            }
            else
                streamConnection.Connect(SlotNumber);
        }
        
        private void OnBecameInvisible()
        {
            streamConnection.Disconnect();
            
            if (IsOwner)
                streamLoadBalancer?.UnregisterStream();
            
            if (_tempRT) RenderTexture.ReleaseTemporary(_tempRT);
            if (_readTex) Destroy(_readTex);
            if (_recvTex) Destroy(_recvTex);
            
            targetImage.gameObject.SetActive(false);

            _lastFrameHash = 0;
            _frameCheckCounter = 0;
        }

        /*
        protected override void OnOwnerChanged(PlayerID? oldOwner, PlayerID? newOwner, bool asServer)
        {
            base.OnOwnerChanged(oldOwner, newOwner, asServer);
            
            if(asServer)
                return;
            
            _lastRecvFrameId = 0;
            
            if (isOwner)
            {
                if (showDebugLogs) Debug.Log($"[Client] Я владелец ({objectId}). Начинаю стрим.");
                _isCapturing = false;
                streamLoadBalancer?.RegisterStream();
                _lastSentFrameId = 0;
            }
            else
            {
                targetImage.gameObject.SetActive(true);
                streamLoadBalancer?.UnregisterStream();
            }

            if (!hasOwner)
            {
                targetImage.gameObject.SetActive(false);
                streamLoadBalancer?.UnregisterStream();
            }
            else
                streamConnection.Connect(SlotNumber);
        }

        */
        
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