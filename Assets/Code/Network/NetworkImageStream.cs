using System;
using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    /// Отправляет изображение из <see cref="RawImage"/> у владельца
    /// и выводит его в тот же RawImage у всех других клиентов.
    /// При старте сервера назначает владельцем объекта хоста (Client Id 0).
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour
    {
        /* ─────────────────────────── Настройки ─────────────────────────── */

        [Tooltip("RawImage-компонент, который показывает картинку у всех игроков.")]
        [SerializeField] private RawImage rawImage;

        [Header("Stream")]
        [SerializeField, Min(0.1f)] private float fps = 2f;
        [SerializeField] private bool  useJpg    = true;
        [SerializeField, Range(10, 100)]
        private int jpgQuality = 70;

        [Header("Networking")]
        [Tooltip("Размер одного чанка (< MTU транспорта).")]
        [SerializeField, Min(128)] private int chunkSize = 950;

        /* ───────────────────────── Runtime ───────────────────────── */

        private Coroutine _sendLoop;
        private FrameAssembler _assembler;

        /* ===================================================================== */
        #region Ownership
        /* ===================================================================== */

        /// <summary>
        /// При запуске сервера отдаём владение объектом хосту (Client Id 0),
        /// чтобы именно он рассылал кадры.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();
            
            const int hostId = 0;                    // FishNet: сервер-клиент всегда 0
            if (OwnerId != hostId) 
                GiveOwnership(NetworkManager.ClientManager.Connection); 
            
            if (IsOwner && rawImage != null)
                _sendLoop = StartCoroutine(SendLoop());
        }

        #endregion
        /* ===================================================================== */

        #region Unity Lifecycle
        /* ===================================================================== */

        private void OnEnable()
        {
            if (IsOwner && rawImage != null)
                _sendLoop = StartCoroutine(SendLoop());
        }

        private void OnDisable()
        {
            if (_sendLoop != null)
                StopCoroutine(_sendLoop);

            _assembler = null;
        }

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

        /// <summary>
        /// Берёт Texture из RawImage, сериализует и шлёт чанками.
        /// </summary>
        private void CaptureAndSend()
        {
            if (rawImage.texture == null) return;

            Texture2D srcTex = rawImage.texture as Texture2D
                               ?? CopyIntoTexture2D(rawImage.texture);
            if (srcTex == null) return;

            byte[] data = useJpg ? srcTex.EncodeToJPG(jpgQuality)
                                 : srcTex.EncodeToPNG();

            int total = data.Length;
            for (int offset = 0; offset < total; offset += chunkSize)
            {
                int len = Math.Min(chunkSize, total - offset);
                var part = new byte[len];
                Buffer.BlockCopy(data, offset, part, 0, len);

                UploadChunk(part, offset, total, srcTex.width, srcTex.height);
            }

            if (srcTex != rawImage.texture)          // мы создавали временную Texture2D
                Destroy(srcTex);
        }

        /// <summary>
        /// Копирует любую Texture (RenderTexture, VideoPlayer и т.д.) в «читаемую» Texture2D.
        /// </summary>
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
        private void UploadChunk(byte[] chunk,
                                 int offset,
                                 int total,
                                 int width,
                                 int height)
        {
            RelayChunk(chunk, offset, total, width, height);
        }

        [ObserversRpc()]
        private void RelayChunk(byte[] chunk,
                                int offset,
                                int total,
                                int width,
                                int height)
        {
            _assembler ??= new FrameAssembler(total);
            _assembler.Add(chunk, offset);

            if (_assembler.IsComplete)
            {
                ApplyImage(_assembler.Data);
                _assembler = null;                   // под следующий кадр
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

        /// <summary>
        /// Сборщик чанков одного кадра.
        /// </summary>
        private sealed class FrameAssembler
        {
            private readonly byte[] _buffer;
            private int _received;

            public bool  IsComplete => _received >= _buffer.Length;
            public byte[] Data       => _buffer;

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
