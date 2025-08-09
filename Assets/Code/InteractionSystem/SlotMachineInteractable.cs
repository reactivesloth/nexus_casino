using System.Threading.Tasks;
using Code.API;
using Code.Network;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;
using Vuplex.WebView;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [Tooltip("Canvas для десктопа (Screen Space / World Space)")]
        [SerializeField] private Canvas computerCanvas;

        [Tooltip("Полноэкранный Canvas для iOS/Android (Screen Space - Overlay)")]
        [SerializeField] private Canvas computerFSCanvas;

        [Tooltip("Canvas с вашим остальным UI, если нужно включать/выключать вместе")]
        [SerializeField] private Canvas contentCanvas;

        [SerializeField] private TextMeshPro idNumberText;

        [Header("Vuplex")]
        [Tooltip("Префаб CanvasWebViewPrefab с нужными настройками (резолюшн, курсоры и т.д.)")]
        [SerializeField] private CanvasWebViewPrefab webViewPrefab;

        [Tooltip("Отключить ВСЕ WebView и убить Chromium-процесс при закрытии (Win/Mac). " +
                 "Внимание: затронет другие окна WebView, если они есть.")]
        [SerializeField] private bool deepCleanupStandalone = true;

        [Tooltip("Очищать кэши/Storage/cookies при закрытии (влияет на все WebView).")]
        [SerializeField] private bool clearAllDataOnClose = true;

        [Header("Streaming (как было)")]
        [SerializeField] private NetworkImageStream networkImageStream;

        private bool _isUsing;
        public bool IsUsing => _isUsing;

        public int IDNumber;

        private CanvasWebViewPrefab _webView; // активный инстанс
        public IWebView WebView => _webView ? _webView.WebView : null;

        public override string InteractionPrompt => !_isUsing ? "Use Computer" : "Exit Computer";

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            networkImageStream ??= GetComponentInChildren<NetworkImageStream>(true);
            if (idNumberText != null)
                idNumberText.text = IDNumber.ToString();
        }
#endif

        private void Awake()
        {
            if (idNumberText != null)
                idNumberText.text = IDNumber.ToString();
        }

        private void Start()
        {
            if (computerCanvas) computerCanvas.gameObject.SetActive(false);
            if (computerFSCanvas) computerFSCanvas.gameObject.SetActive(false);
            if (contentCanvas) contentCanvas.gameObject.SetActive(false);
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isUsing = false;
            // На всякий случай, если сетевая остановка случилась посреди интеракции
            _ = CloseAndCleanupAsync();
        }

        protected internal override void OnInteract(NetworkConnection conn)
        {
            if (_isUsing) return;
            base.OnInteract(conn);
            _isUsing = true;
            TargetToggleComputerUI(conn, true);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            if (!_isUsing) return;
            base.OnEndInteract(conn);
            _isUsing = false;
            TargetToggleComputerUI(conn, false);
        }

        [TargetRpc]
        private void TargetToggleComputerUI(NetworkConnection conn, bool open)
        {
            if (!computerCanvas && !computerFSCanvas)
            {
                Debug.LogError("[SlotMachineInteractable] Assign canvases in Inspector!");
                return;
            }

            var targetCanvas = GetTargetCanvas();
            if (!targetCanvas)
            {
                Debug.LogError("[SlotMachineInteractable] No target canvas found for platform.");
                return;
            }

            // Включаем/выключаем UI
            targetCanvas.gameObject.SetActive(open);
            if (contentCanvas) contentCanvas.gameObject.SetActive(open);

            if (!open)
            {
                _ = CloseAndCleanupAsync();
                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = false;
            }
            else
            {
                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;
                _ = OpenWebViewAsync(targetCanvas);
            }
        }

        private Canvas GetTargetCanvas()
        {
#if UNITY_IOS || UNITY_ANDROID
            return computerFSCanvas ? computerFSCanvas : computerCanvas;
#else
            return computerCanvas;
#endif
        }

        private async Task OpenWebViewAsync(Canvas parentCanvas)
        {
            // Защита от двойного вызова
            if (_webView) return;

            // Инстанцируем настроенный префаб как дочерний элемент нужного Canvas
            _webView = Instantiate(webViewPrefab, parentCanvas.transform);

            // Растягиваем на весь родительский RectTransform
            var rt = (RectTransform)_webView.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _webView.transform.SetAsFirstSibling();

            // Ждём реальной инициализации вместо Invoke/таймеров
            await _webView.WaitUntilInitialized();

            // Загружаем URL
            string url = $"https://back.nexusmetaclub.com?jwt={ClientDataStorage.AccessToken}";
            _webView.WebView.LoadUrl(url);

            // Твой стрим может освежить цель после появления webview
            if (networkImageStream != null)
                networkImageStream.SetTexture();
        }

        private async Task CloseAndCleanupAsync()
        {
            // Выключаем стрим-картинку
            if (networkImageStream != null)
                networkImageStream.ClearTexture();

            // Закрываем конкретно наш webview-инстанс
            if (_webView)
            {
                // Важно: уничтожаем именно префаб (внутри он корректно освобождает IWebView)
                _webView.Destroy();
                _webView = null;
            }

            // Опционально чистим персистентные данные
            if (clearAllDataOnClose)
            {
#if UNITY_STANDALONE || UNITY_EDITOR
                if (deepCleanupStandalone)
                {
                    // Терминируем Chromium-процесс, затем чистим данные
                    await StandaloneWebView.TerminateBrowserProcess();
                    Web.ClearAllData();
                }
                else
                {
                    // Без терминации: некоторые операции недоступны — но ClearAllData в новых версиях работает корректно.
                    Web.ClearAllData();
                }
#else
                Web.ClearAllData();
#endif
            }
        }
    }
}