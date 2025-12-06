using System;
using System.Linq;
using Code.InteractionSystem;
using Code.Network.Stream.Data;
using Code.Utility;
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

        [Header("UI")] [SerializeField] private RawImage rawImage;
        [SerializeField] private RawImage targetImage;

        [Header("Settings")]
        // 128px = ~2-3 КБ. Это пролетит мгновенно даже через Reliable.
        [SerializeField]
        private int maxResolution = 128;

        [SerializeField] private int jpgQuality = 35;
        [SerializeField] private float sendRate = 0.1f; // 10 раз в секунду

        [Header("Frame Change Detection")]
        [SerializeField] private bool enableFrameChangeDetection = true;
        [SerializeField] private int frameHashCheckInterval = 1; // Проверять каждый N-й кадр перед отправкой
        [SerializeField] private float hashSimilarityThreshold = 0.95f; // 95% одинаковости = не отправляем

        [Header("Stream Connection"), SerializeField]
        private StreamingLiteNetLibPeer streamConnection;

        [Header("Debug")] [SerializeField] private bool showDebugLogs = true;

        private float _nextTime;
        private bool _isCapturing;
        private RenderTexture _tempRT;
        private Texture2D _readTex;
        private Texture2D _recvTex;

        // Frame change detection
        private byte[] _lastFrameData;
        private uint _lastFrameHash;
        private int _frameCheckCounter;

        private int _savedJPGQuality = 35;
        
        private int SlotNumber => slotMachineInteractable.IDNumber;

        public event Action<Texture> OnApplyTexture;

        private void Awake()
        {
            _savedJPGQuality = jpgQuality;
            streamConnection = FindAnyObjectByType<StreamingLiteNetLibPeer>();
            streamConnection.OnFrameReceived += StreamConnectionOnOnFrameReceived;
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
            // Стримим только если владелец
            if (!IsOwner) return;

            // Лимит частоты
            if (Time.time < _nextTime) return;

            // Защита от наложения
            if (_isCapturing) return;

            // Валидация источника
            if (rawImage == null || rawImage.texture == null || rawImage.texture.width < 16) return;

            _nextTime = Time.time + sendRate;
            Capture();
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
                SendFrameInternal(data);
                return;
            }

            // Проверка на дубликат кадра
            if (IsFrameDuplicate(data))
            {
                if (showDebugLogs) Debug.Log($"[Client] Frame skipped (duplicate) - {data.Length} bytes");
                _isCapturing = false;
                return;
            }

            SendFrameInternal(data);
        }

        private bool IsFrameDuplicate(byte[] currentData)
        {
            if (_lastFrameData == null)
            {
                _lastFrameData = currentData;
                _lastFrameHash = CalculateHash(currentData);
                _frameCheckCounter = 0;
                return false;
            }

            // Быстрая проверка: размер должен быть одинаковым
            if (currentData.Length != _lastFrameData.Length)
            {
                _lastFrameData = currentData;
                _lastFrameHash = CalculateHash(currentData);
                _frameCheckCounter = 0;
                return false;
            }

            _frameCheckCounter++;

            // Каждый N-й кадр проверяем полное совпадение
            if (_frameCheckCounter >= frameHashCheckInterval)
            {
                uint currentHash = CalculateHash(currentData);
                
                // Полное совпадение хешей = дубликат
                if (currentHash == _lastFrameHash)
                {
                    return true;
                }

                _lastFrameData = currentData;
                _lastFrameHash = currentHash;
                _frameCheckCounter = 0;
                return false;
            }

            // Между проверками - быстрое сравнение первых N байт
            int sampleSize = Mathf.Min(256, currentData.Length);
            bool isSimilar = ArraysAreEqual(currentData, _lastFrameData, sampleSize);

            if (!isSimilar)
            {
                _lastFrameData = currentData;
                _lastFrameHash = CalculateHash(currentData);
                _frameCheckCounter = 0;
            }

            return isSimilar;
        }

        /// <summary>
        /// Быстрое сравнение первых N байт массивов
        /// </summary>
        private bool ArraysAreEqual(byte[] arr1, byte[] arr2, int length)
        {
            if (arr1 == null || arr2 == null || arr1.Length < length || arr2.Length < length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (arr1[i] != arr2[i])
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Быстрый хеш всего массива (DJB2 алгоритм)
        /// </summary>
        private uint CalculateHash(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            uint hash = 5381;
            
            for (int i = 0; i < data.Length; i++)
            {
                hash = ((hash << 5) + hash) ^ data[i];
            }

            return hash;
        }

        private void SendFrameInternal(byte[] data)
        {
            if (showDebugLogs) Debug.Log($"[Client] Sending frame {data.Length} bytes");

            if (streamConnection != null && streamConnection.IsConnected)
                streamConnection.SendStreamFrame(SlotNumber, data);

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
                if (IsOwner)
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
            
            if(showDebugLogs)
                Debug.Log($"[NetworkImageStreamClient] OnStartClient slot №{SlotNumber} owner: {Owner.ClientId}");
            
            if (Owner.ClientId == -1)
            {
                targetImage.gameObject.SetActive(true);
            }
        }
        
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            if (IsOwner)
            {
                if (showDebugLogs) Debug.Log($"[Client] Я владелец ({ObjectId}). Начинаю стрим.");
                _isCapturing = false;
            }

            if (Owner.ClientId == -1)
            {
                targetImage.gameObject.SetActive(false);
            }
            else if (!IsOwner)
            {
                targetImage.gameObject.SetActive(true);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_tempRT) RenderTexture.ReleaseTemporary(_tempRT);
            if (_readTex) Destroy(_readTex);
            if (_recvTex) Destroy(_recvTex);
            targetImage.gameObject.SetActive(false);

            // Очистка данных дублирования
            _lastFrameData = null;
            _lastFrameHash = 0;
            _frameCheckCounter = 0;
            
            if(showDebugLogs)
                Debug.Log($"[NetworkImageStreamClient] OnStopClient slot №{SlotNumber} owner: {Owner.ClientId}");
        }
        
        // API
        public void SetQualitySettings(float d, int j)
        {
            jpgQuality = j;
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