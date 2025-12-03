using System;
using Code.InteractionSystem;
using Code.Network.Stream;
using Code.Network.Stream.Data;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

namespace Code.Network
{
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        [SerializeField] private SlotMachineInteractable slotMachineInteractable;
        
        [Header("UI")]
        [SerializeField] private RawImage rawImage;
        [SerializeField] private RawImage targetImage;

        [Header("Settings")]
        // 128px = ~2-3 КБ. Это пролетит мгновенно даже через Reliable.
        [SerializeField] private int maxResolution = 128; 
        [SerializeField] private int jpgQuality = 35;
        [SerializeField] private float sendRate = 0.1f; // 10 раз в секунду
        
        [Header("Stream Connection"), SerializeField] private StreamingLiteNetLibPeer streamConnection;
        
        [Header("Debug")]
        [SerializeField] private bool showDebugLogs = true;

        private float _nextTime;
        private bool _isCapturing;
        private RenderTexture _tempRT;
        private Texture2D _readTex;
        private Texture2D _recvTex;

        private int SlotNumber => slotMachineInteractable.IDNumber;
        
        public event Action<Texture> OnApplyTexture;

        private void Awake()
        {
            streamConnection = FindAnyObjectByType<StreamingLiteNetLibPeer>();
            streamConnection.OnFrameReceived += StreamConnectionOnOnFrameReceived;
        }

        private void StreamConnectionOnOnFrameReceived(StreamFrameData data)
        {
            if(data == null || data.SlotId != SlotNumber)
                return;
            
            ApplyReceivedTexture(data.Data);
        }

        // =================================================================================
        // ЛОГИКА СТРИМЕРА
        // =================================================================================

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner)
            {
                if (showDebugLogs) Debug.Log($"[Client] Я владелец ({ObjectId}). Начинаю стрим.");
                _isCapturing = false;
            }
        }

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
            if (req.hasError) { _isCapturing = false; return; }

            PrepareReadTex(w, h);
            _readTex.SetPixelData(req.GetData<byte>(), 0);
            _readTex.Apply(false, false);
            
            SendRPC(_readTex.EncodeToJPG(jpgQuality));
        }

        private void SyncReadback(int w, int h)
        {
            PrepareReadTex(w, h);
            var prev = RenderTexture.active;
            RenderTexture.active = _tempRT;
            _readTex.ReadPixels(new Rect(0,0,w,h), 0, 0);
            _readTex.Apply(false, false);
            RenderTexture.active = prev;
            
            SendRPC(_readTex.EncodeToJPG(jpgQuality));
        }

        private void PrepareReadTex(int w, int h)
        {
            if (_readTex == null || _readTex.width != w || _readTex.height != h)
            {
                if (_readTex) Destroy(_readTex);
                _readTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            }
        }

        private void SendRPC(byte[] data)
        {
            if (showDebugLogs) Debug.Log($"[Client] Sending RPC {data.Length} bytes...");
            
            // Вызываем ServerRpc. FishNet сам знает, какому объекту это принадлежит.
            // По умолчанию это RELIABLE (Гарантированная доставка).
            //Server_UploadFrame(data);
            if (streamConnection != null && streamConnection.IsConnected) 
                streamConnection.SendStreamFrame(SlotNumber, data);
            
            _isCapturing = false;
        }

        // =================================================================================
        // СЕТЕВАЯ ЧАСТЬ (RPC)
        // =================================================================================

        // 1. Client -> Server (Upload)
        [ServerRpc] 
        private void Server_UploadFrame(byte[] data)
        {
            // Сервер получил данные.
            // Отправляем подтверждение владельцу (Debug)
            Target_DebugReply(Owner, data.Length);

            // Рассылаем всем наблюдателям (Observers)
            Observers_DownloadFrame(data);
        }

        // 2. Server -> Clients (Download)
        // ExcludeOwner = true -> Чтобы не слать картинку обратно стримеру
        // BufferLast = false -> Чтобы новые игроки не получали старый кадр, а ждали новый
        [ObserversRpc(ExcludeOwner = true, BufferLast = false)]
        private void Observers_DownloadFrame(byte[] data)
        {
            // Этот код выполняется у ЗРИТЕЛЕЙ
            ApplyReceivedTexture(data);
        }

        // 3. Debug Reply (Server -> Owner)
        [TargetRpc]
        private void Target_DebugReply(NetworkConnection conn, int bytes)
        {
            if (showDebugLogs)
                Debug.Log($"<color=green>[Server Reply] RPC дошел! Размер: {bytes}</color>");
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
                if(targetImage) targetImage.gameObject.SetActive(false);
            } 
            else 
            {
                if(targetImage) 
                {
                    targetImage.gameObject.SetActive(true);
                    targetImage.color = Color.clear; // Прячем до первого кадра
                }
            }
        }
        
        public override void OnStopClient()
        {
            base.OnStopClient();
            if (_tempRT) RenderTexture.ReleaseTemporary(_tempRT);
            if (_readTex) Destroy(_readTex);
            if (_recvTex) Destroy(_recvTex);
        }

        // API
        public void SetQualitySettings(float d, int j) {}
        public void ResetQualitySettings() {}
        public void SetTexture(RawImage i) { rawImage = i; }
        public void ClearTexture() { rawImage = null; }
    }
}