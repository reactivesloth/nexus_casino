using System;
using System.Collections;
using Epic.OnlineServices.P2P;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Экран игрового автомата: 
    /// • Когда автомат свободен (нет владельца) — показывает локальную текстуру‑застойку.
    /// • Когда автомат занят — владелец стримит изображение всем клиентам.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        /* ─────────────────────────── Настройки ─────────────────────────── */

        [Tooltip("RawImage‑компонент, который показывает картинку у всех игроков.")]
        [SerializeField] private RawImage rawImage;

        [Header("Idle State")]
        [Tooltip("Текстура, отображаемая когда автомат свободен (нет владельца).")]
        [SerializeField] private Texture2D idleTexture;

        [Header("Stream")]
        [SerializeField, Min(0.1f)] private float fps = 24f;
        [SerializeField] private bool  useJpg    = true;
        [SerializeField, Range(10, 100)] private int jpgQuality = 70;

        [Header("Networking")]
        [Tooltip("Размер одного чанка (< MTU транспорта).")]
        [SerializeField, Min(128)] private int chunkSize = P2PInterface.MaxPacketSize - 170;

        [Tooltip("Назначить ли хоста владельцем объекта при запуске клиента.")]
        [SerializeField] private bool hostIsOwnerOnStart = true;

        /* ───────────────────────── Runtime ───────────────────────── */

        private Coroutine _sendLoop;
        private FrameAssembler _assembler;

        /* ===================================================================== */
        #region Public API
        /* ===================================================================== */

        /// <summary>
        /// Назначает указанного клиента владельцем объекта.
        /// Вызывать только на сервере.
        /// </summary>
        [Server]
        public void SetOwner(NetworkConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            GiveOwnership(connection);
        }

        /// <summary>
        /// Сбрасывает владельца, делая объект бесхозным.
        /// Вызывать только на сервере.
        /// </summary>
        [Server]
        public void ClearOwner() => RemoveOwnership();

        #endregion
        /* ===================================================================== */

        #region Ownership callbacks
        /* ===================================================================== */

        /// <summary>
        /// Вызывается у клиента при смене владения.
        /// (FishNet 5.x: передаётся только предыдущий владелец.)
        /// </summary>
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);

            NetworkConnection newOwner = Owner;                // текущий владелец после смены (может быть null)

            bool iWasOwner   = prevOwner == NetworkManager.ClientManager.Connection;
            bool iAmOwner    = newOwner == NetworkManager.ClientManager.Connection;

            // Теряем владение — выключаем отправку
            if (iWasOwner && !iAmOwner && _sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }
            // Получаем владение — включаем отправку
            else if (iAmOwner && _sendLoop == null && rawImage != null)
            {
                _sendLoop = StartCoroutine(SendLoop());
            }

            // Нет владельца → показываем idle
            if (newOwner == null && idleTexture != null)
                ShowIdleTexture();
        }

        /// <summary>
        /// При запуске клиента: по желанию назначаем хоста владельцем.
        /// Если объект остаётся бесхозным — показываем idle‑текстуру.
        /// </summary>
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
            {
                _sendLoop = StartCoroutine(SendLoop());
            }
            else if (!IsOwner && Owner == null && idleTexture != null)
            {
                ShowIdleTexture();
            }
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
            if (_sendLoop != null)
            {
                StopCoroutine(_sendLoop);
                _sendLoop = null;
            }

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

            Texture2D srcTex = rawImage.texture as Texture2D ?? CopyIntoTexture2D(rawImage.texture);
            if (srcTex == null) return;

            byte[] data = useJpg ? srcTex.EncodeToJPG(jpgQuality) : srcTex.EncodeToPNG();
            int total = data.Length;
            for (int offset = 0; offset < total; offset += chunkSize)
            {
                int len = Math.Min(chunkSize, total - offset);
                var part = new byte[len];
                Buffer.BlockCopy(data, offset, part, 0, len);
                UploadChunk(part, offset, total, srcTex.width, srcTex.height);
            }
            if (srcTex != rawImage.texture) Destroy(srcTex);
        }

        private static Texture2D CopyIntoTexture2D(Texture src)
        {
            if (src == null) return null;
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        #endregion
        /* ===================================================================== */

        #region Network RPCs
        /* ===================================================================== */

        [ServerRpc(RequireOwnership = false)]
        private void UploadChunk(byte[] chunk, int offset, int total, int width, int height)
            => RelayChunk(chunk, offset, total, width, height);

        [ObserversRpc]
        private void RelayChunk(byte[] chunk, int offset, int total, int width, int height)
        {
            _assembler ??= new FrameAssembler(total);
            _assembler.Add(chunk, offset);
            if (_assembler.IsComplete)
            {
                ApplyImage(_assembler.Data);
                _assembler = null;
            }
        }

        #endregion
        /* ===================================================================== */

        #region Receiving side (all clients)
        /* ===================================================================== */

        private void ApplyImage(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes, false);
            tex.name = "NetStream";
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
