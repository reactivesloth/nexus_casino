using System;
using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using K4os.Compression.LZ4;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Экран игрового автомата:
    /// • Без владельца — локальная Idle‑текстура.
    /// • С владельцем — стриминг экрана с даунскейлом.
    /// Дополнительно: пропуск одинаковых кадров и опциональное LZ4‑сжатие.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        /* ─────────────────────────── Настройки ─────────────────────────── */

        [Tooltip("RawImage‑компонент, который показывает картинку у всех игроков.")]
        [SerializeField] private RawImage rawImage;

        [Header("Idle State")]
        [Tooltip("Текстура, отображаемая когда автомат свободен (нет владельца).")]
        [SerializeField] private Texture2D idleTexture;

        [Header("Stream Quality")]
        [SerializeField, Min(0.1f)] private float fps = 24f;
        [Tooltip("Снижение разрешения перед кодированием (0.1‑1). 0.5 = половина ширины/высоты → 4× меньше пикселей.")]
        [SerializeField, Range(0.1f, 1f)] private float downscale = 0.5f;
        [SerializeField] private bool useJpg = true;
        [SerializeField, Range(10, 100)] private int jpgQuality = 70;
        [Tooltip("Пропускать ли кадры, идентичные предыдущему.")]
        [SerializeField] private bool skipDuplicateFrames = true;

        [Header("Networking")]
        [Tooltip("Размер одного чанка (< MTU транспорта). 1150 байт обычно безопасно для UDP IPv4.")]
        [SerializeField, Min(256)] private int chunkSize = 1150;
        [Tooltip("Сжимать полезную нагрузку LZ4Pickler‑ом (быстрое, ~2‑3× экономия на PNG / 10‑20 % на JPEG).")]
        [SerializeField] private bool lz4Compress = true;
        [Tooltip("Назначить ли хоста владельцем объекта при запуске клиента.")]
        [SerializeField] private bool hostIsOwnerOnStart = true;

        /* ───────────────────────── Runtime ───────────────────────── */

        private Coroutine _sendLoop;
        private FrameAssembler _assembler;
        private Hash128 _lastHash;

        /* ===================================================================== */
        #region Public API
        /* ===================================================================== */

        [Server] public void SetOwner(NetworkConnection connection) => GiveOwnership(connection ?? throw new ArgumentNullException(nameof(connection)));
        [Server] public void ClearOwner() => RemoveOwnership();

        #endregion
        /* ===================================================================== */

        #region Ownership callbacks
        /* ===================================================================== */

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            NetworkConnection newOwner = Owner;
            bool iWasOwner = prevOwner == NetworkManager.ClientManager.Connection;
            bool iAmOwner  = newOwner == NetworkManager.ClientManager.Connection;

            if (iWasOwner && !iAmOwner && _sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }
            else if (iAmOwner && _sendLoop == null && rawImage != null)
            {
                _sendLoop = StartCoroutine(SendLoop());
            }

            if (newOwner == null && idleTexture != null)
                ShowIdleTexture();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (hostIsOwnerOnStart)
            {
                const int hostId = 0;
                if (OwnerId != hostId)
                {
                    var hostConn = NetworkManager.ClientManager.Connection;
                    if (hostConn != null)
                        GiveOwnership(hostConn);
                }
            }

            if (IsOwner && rawImage != null)
                _sendLoop = StartCoroutine(SendLoop());
            else if (!IsOwner && Owner == null && idleTexture != null)
                ShowIdleTexture();
        }

        #endregion
        /* ===================================================================== */

        #region Unity Lifecycle
        /* ===================================================================== */

        private void OnEnable()
        {
            if (IsOwner && rawImage != null)
                _sendLoop = StartCoroutine(SendLoop());
            else if (!IsOwner && Owner == null && idleTexture != null)
                ShowIdleTexture();
        }

        private void OnDisable()
        {
            if (_sendLoop != null) { StopCoroutine(_sendLoop); _sendLoop = null; }
            _assembler = null;
        }

        #endregion
        /* ===================================================================== */

        #region Idle‑local logic
        /* ===================================================================== */

        private void ShowIdleTexture() => rawImage.texture = idleTexture;

        #endregion
        /* ===================================================================== */

        #region Sending side (owner)
        /* ===================================================================== */

        private IEnumerator SendLoop()
        {
            var wait = new WaitForSeconds(1f / fps);
            while (true)
            {
                yield return wait;
                CaptureAndSend();
            }
        }

        private void CaptureAndSend()
        {
            if (rawImage.texture == null) return;

            // 1) Даунскейл
            int w = Mathf.RoundToInt(rawImage.texture.width * downscale);
            int h = Mathf.RoundToInt(rawImage.texture.height * downscale);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(rawImage.texture, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // 2) Пропуск дубликатов
            if (skipDuplicateFrames)
            {
                Hash128 hash = Hash128.Compute(tex.GetRawTextureData());
                if (hash == _lastHash) { UnityEngine.Object.Destroy(tex); return; }
                _lastHash = hash;
            }

            // 3) Кодирование
            byte[] payload = useJpg ? tex.EncodeToJPG(jpgQuality) : tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);

            if (lz4Compress)
                payload = LZ4Pickler.Pickle(payload);

            // 4) Разбивка на чанки
            int total = payload.Length;
            for (int offset = 0; offset < total; offset += chunkSize)
            {
                int len = Math.Min(chunkSize, total - offset);
                var part = new byte[len];
                Buffer.BlockCopy(payload, offset, part, 0, len);
                UploadChunk(part, offset, total, w, h);
            }
        }

        #endregion
        /* ===================================================================== */

        #region Network RPCs
        /* ===================================================================== */

        [ServerRpc(RequireOwnership = false)]
        private void UploadChunk(byte[] chunk, int offset, int total, int width, int height) =>
            RelayChunk(chunk, offset, total, width, height);

        [ObserversRpc(ExcludeOwner = true)]
        private void RelayChunk(byte[] chunk, int offset, int total, int width, int height)
        {
            _assembler ??= new FrameAssembler(total);
            _assembler.Add(chunk, offset);
            if (_assembler.IsComplete)
            {
                byte[] data = _assembler.Data;
                if (lz4Compress)
                    data = LZ4Pickler.Unpickle(data);

                ApplyImage(data);
                _assembler = null;
            }
        }

        #endregion
        /* ===================================================================== */

        #region Receiving side
        /* ===================================================================== */

        private void ApplyImage(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false);
            tex.LoadImage(bytes, false);
            rawImage.texture = tex;
        }

        #endregion
        /* ===================================================================== */

        #region Helpers
        /* ===================================================================== */

        private sealed class FrameAssembler
        {
            private readonly byte[] _buffer;
            private int _received;
            public bool IsComplete => _received >= _buffer.Length;
            public byte[] Data => _buffer;
            public FrameAssembler(int totalBytes) => _buffer = new byte[totalBytes];
            public void Add(byte[] chunk, int offset)
            {
                Buffer.BlockCopy(chunk, 0, _buffer, offset, chunk.Length);
                _received += chunk.Length;
            }
        }

        #endregion
    }
}
