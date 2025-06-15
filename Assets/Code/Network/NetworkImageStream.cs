// NetworkImageStream.cs
// Streams RawImage from the host (client‑server) to all other players
// using FishNet + TurtlePass for chunked delivery.

using System.Collections;
using FishNet.Connection;
using FishNet.Object;
using Plugins.FishNet.TurtlePass;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Network
{
    /// <summary>
    ///   • Хост (clientId = 0) становится владельцем объекта и каждые <see cref="fps"/> секунд
    ///     рассылает снимок своего <see cref="RawImage"/> всем игрокам.
    ///   • Turtle Pass автоматически дробит большие массивы и собирает их на клиенте.
    ///   • Клиенты, не являющиеся владельцем, лишь принимают PNG/JPG и выводят в RawImage.
    /// </summary>
    public sealed class NetworkImageStream : NetworkBehaviour, ITurtlePassReceiver
    {
        /* ─────────── Inspector ─────────── */

        [SerializeField] private RawImage rawImage;

        [Header("Stream")]
        [Min(0.1f)]      [SerializeField] private float fps        = 2f;
        [SerializeField] private bool  useJpg    = true;
        [Range(10,100)]  [SerializeField] private int   jpgQuality = 70;

        /* ─────────── Constants ─────────── */

        private const TurtlePassDataType DATA_TYPE = TurtlePassDataType.GameState;

        /* ─────────── Internals ─────────── */

        private Coroutine _sendLoop;

        /* =================================================================== */
        #region ▸ Ownership
        /* =================================================================== */

        /// <summary>
        /// На сервере сразу передаём объект во владение хост‑клиенту (Id 0),
        /// чтобы именно он стримил картинку.
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            const int HOST_ID = 0;                       // у FishNet сервер‑клиент всегда Id 0
            if (OwnerId == HOST_ID)                      // уже владелец
                return;

            if (NetworkManager.ServerManager.Clients.TryGetValue(HOST_ID, out NetworkConnection hostConn))
                NetworkObject.GiveOwnership(hostConn);  // требуемый сигнатурой NetworkConnection
        }

        #endregion
        /* =================================================================== */
        #region ▸ Unity lifecycle
        /* =================================================================== */

        private void Awake()
        {
            // регистрируемся как приёмник в TurtlePassManager
            if (!TurtlePassManager.Receivers.Contains(this))
                TurtlePassManager.Receivers.Add(this);
        }

        private void OnDestroy() => TurtlePassManager.Receivers.Remove(this);

        public override void OnStartClient()
        {
            base.OnStartClient();
            TryBeginStreaming();
        }

        private void OnEnable()  => TryBeginStreaming();
        private void OnDisable() => EndStreaming();

        private void TryBeginStreaming()
        {
            if (!IsOwner || !rawImage || _sendLoop != null)
                return;                                  // стримит только владелец (хост)

            _sendLoop = StartCoroutine(SendLoop());
        }

        private void EndStreaming()
        {
            if (_sendLoop == null) return;
            StopCoroutine(_sendLoop);
            _sendLoop = null;
        }

        #endregion
        /* =================================================================== */
        #region ▸ Sending side (owner / host)
        /* =================================================================== */

        private IEnumerator SendLoop()
        {
            var wait = new WaitForSeconds(1f / fps);
            while (true)
            {
                yield return wait;
                SendFrame();
            }
        }

        private void SendFrame()
        {
            if (rawImage.texture == null) return;

            Texture2D tex = rawImage.texture as Texture2D ?? CopyIntoTexture2D(rawImage.texture);
            if (tex == null) return;

            byte[] bytes = useJpg ? tex.EncodeToJPG(jpgQuality) : tex.EncodeToPNG();

            // хост является сервером, поэтому можем сразу бросать «-1» (всем клиентам)
            TurtlePassManager.QueueSendBytes(-1, DATA_TYPE, bytes, bytes.Length, false);

            if (tex != rawImage.texture)
                Object.Destroy(tex);
        }

        private static Texture2D CopyIntoTexture2D(Texture src)
        {
            var rt   = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        #endregion
        /* =================================================================== */
        #region ▸ TurtlePass receiver
        /* =================================================================== */

        /// <inheritdoc />
        public void ReceiveTurtlePassMessage(byte[] data, int packedSize, int senderId, TurtlePassDataType dataType)
        {
            if (dataType != DATA_TYPE)
                return;                                   // не наш тип данных

            if(senderId == NetworkObject.OwnerId)
                return;
            
            Debug.Log($"Packed size is {packedSize}");
            
            ApplyImage(data);
        }

        private void ApplyImage(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(bytes, false);
            tex.name = "NetStream";
            rawImage.texture = tex;
        }

        #endregion
    }
}
