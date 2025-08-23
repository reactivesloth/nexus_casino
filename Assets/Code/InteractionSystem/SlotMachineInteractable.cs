using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Code.API;
using Code.Network;
using Code.Network.HostMigration;
using CurvedUI;
using FishNet.Connection;
using FishNet.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using Vuplex.WebView;

namespace Code.InteractionSystem
{
    public class SlotMachineInteractable : Interactable
    {
        [Header("UI Settings")]
        [Tooltip("Canvas для 3д режима (Screen Space / World Space)")]
        [SerializeField] private Canvas computer3dCanvas;
        [Tooltip("Полноэкранный Canvas (Screen Space - Overlay)")]
        [SerializeField] private Canvas computerFullScreenCanvas;
        [Tooltip("Canvas с остальным UI, если нужно включать/выключать вместе")]
        [SerializeField] private Canvas contentCanvas;
        [SerializeField] private TextMeshPro idNumberText;

        [Header("Vuplex")]
        [SerializeField] private CanvasWebViewPrefab webViewPrefab;
        [SerializeField, Tooltip("Терминировать Chromium-процесс при закрытии (Standalone).")]
        private bool deepCleanupStandalone = true;
        [SerializeField, Tooltip("Очищать кэши/Storage/cookies при закрытии (влияет на все WebView).")]
        private bool clearAllDataOnClose = true;

        [Header("Streaming")]
        [SerializeField] private NetworkImageStream networkImageStream;

        public int IDNumber;
        public bool IsUsing => _isUsing;
        public IWebView WebView => _sharedWebView != null ? _sharedWebView.WebView : null;

        private bool _isUsing;
        private static CanvasWebViewPrefab _sharedWebView; // общий для всех
        private static bool _webViewInitialized = false;
        private static bool _opening;
        private static bool _closing;
        private static bool _wasStarted;
        [SerializeField] private AudioMixer mixer;

        public NetworkImageStream NetworkImageStream => networkImageStream;
        
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
            if (networkImageStream == null) networkImageStream = GetComponentInChildren<NetworkImageStream>(true);
        }
#endif

        private void Awake()
        {
            if (idNumberText != null) idNumberText.text = IDNumber.ToString();
        }

        private void Start()
        {
            _wasStarted = true;
            if (computer3dCanvas) computer3dCanvas.gameObject.SetActive(false);
            if (computerFullScreenCanvas) computerFullScreenCanvas.gameObject.SetActive(false);
            //if (contentCanvas) contentCanvas.gameObject.SetActive(false);
        }

        // private void OnDisable()
        // {
        //     // Если объект выключили посреди сессии — корректно закроем UI и WebView
        //     if (_isUsing) _ = CloseAndCleanupAsync();
        // }
      
        public override string InteractionPrompt => !_isUsing ? "Use Computer" : "Exit Computer";

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            _isUsing = false;
            //_ = CloseAndCleanupAsync();
        }

        protected internal override void OnInteract(NetworkConnection conn, bool force)
        {
            if (_isUsing) return;
            base.OnInteract(conn, force);
            _isUsing = true;
            TargetToggleComputerUI(conn, true);
            ObserverActivation(true);
        }

        protected internal override void OnEndInteract(NetworkConnection conn)
        {
            if (!_isUsing) return;
            base.OnEndInteract(conn);
            _isUsing = false;
            TargetToggleComputerUI(conn, false);
            ObserverActivation(false);
        }

        public void SwitchFS()
        {
            var newFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0;
            PlayerPrefs.SetInt("PlayerSlotMachineIsFullscreen", newFS ? 1 : 0);
            PlayerPrefs.Save();

            var targetCanvas = newFS ? computerFullScreenCanvas : computer3dCanvas;
            var otherCanvas  = newFS ? computer3dCanvas : computerFullScreenCanvas;

            if (targetCanvas && !targetCanvas.gameObject.activeSelf)
                targetCanvas.gameObject.SetActive(true);
            if (otherCanvas && otherCanvas.gameObject.activeSelf)
                otherCanvas.gameObject.SetActive(false);

            if (newFS && CursorManager.Instance != null)
            {
                if (CursorManager.Instance != null) CursorManager.Instance.ShowCursor();
            }
            
            if (PlayerInput.Instance != null) PlayerInput.Instance.IsBusy = newFS;

            if (_sharedWebView != null)
            {
                var curvedUIComp = _sharedWebView.GetComponentInChildren<CurvedUIVertexEffect>();
                curvedUIComp.enabled = !newFS;
                
                RebindWebViewInput(_sharedWebView, targetCanvas, newFS);
            }
        }


        private void RebindWebViewInput(CanvasWebViewPrefab webView, Canvas canvas, bool newFS)
        {
            if (webView == null || canvas == null)
                return;

            webView.transform.SetParent(canvas.transform, false);
            if (newFS)
                webView.transform.SetAsLastSibling();
            else
                webView.transform.SetAsFirstSibling();
            
            webView.transform.localPosition = Vector3.zero;
            webView.transform.localRotation = Quaternion.identity;
            webView.transform.localScale    = Vector3.one;

            if (canvas.renderMode is RenderMode.WorldSpace or RenderMode.ScreenSpaceCamera)
            {
                if (canvas.worldCamera == null)
                    canvas.worldCamera = Camera.main;
            }
            else
            {
                canvas.worldCamera = null;
            }
            if (!canvas.TryGetComponent<GraphicRaycaster>(out _))
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            Canvas.ForceUpdateCanvases();
            var rt = (RectTransform)webView.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();

            var oldDetector = webView.GetComponentInChildren<CanvasPointerInputDetector>(true);
            if (oldDetector != null)
                Destroy(oldDetector);

            var newDetector = webView.gameObject.AddComponent<CanvasPointerInputDetector>();

            StartCoroutine(ReinitDetectorNextFrame(webView, newDetector));

            var defaultDetector = webView.GetComponentInChildren<DefaultPointerInputDetector>(true);
            if (defaultDetector != null)
                defaultDetector.enabled = false;
        }

        private IEnumerator ReinitDetectorNextFrame(CanvasWebViewPrefab webView, CanvasPointerInputDetector detector) {
            yield return new WaitForEndOfFrame();

            webView.SetPointerInputDetector(detector);
            detector.enabled = false;
            detector.enabled = true;
        }
        
        [TargetRpc]
        private void TargetToggleComputerUI(NetworkConnection conn, bool open)
        {
            if (!_wasStarted) return;

            var targetCanvas = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 1 ? computerFullScreenCanvas : computer3dCanvas;
            if (!targetCanvas)
            {
                Debug.LogError("[SlotMachineInteractable] No target canvas found for platform.");
                return;
            }

            targetCanvas.gameObject.SetActive(open);
            if (contentCanvas) contentCanvas.gameObject.SetActive(open);

            if (!open)
            {
                _ = CloseAndCleanupAsync();
                if (PlayerInput.Instance != null)
                {
                    PlayerInput.Instance.HideMobileFallback = false;
                    PlayerInput.Instance.IsBusy = false;
                }
            }
            else
            {
                if (PlayerInput.Instance != null) PlayerInput.Instance.HideMobileFallback = true;
                _ = OpenWebViewAsync(targetCanvas);
            }
        }

        
        [ObserversRpc(BufferLast = true)]
        private void ObserverActivation(bool open) 
        {
            Debug.Log($"ObserverActivation {open}");
            ActivateStoriesUI(open);
        }
        
        private void ActivateStoriesUI(bool open) => contentCanvas.gameObject.SetActive(open);

        private async Task OpenWebViewAsync(Canvas parentCanvas)
        {
            if (_opening) return;
            _opening = true;
            var newFS = PlayerPrefs.GetInt("PlayerSlotMachineIsFullscreen", 0) == 0;

            try
            {
                if (!_webViewInitialized)
                {
                    _sharedWebView = Instantiate(webViewPrefab, parentCanvas.transform);
                    await _sharedWebView.WaitUntilInitialized();
                    _sharedWebView.WebView.LoadUrl($"https://back.nexusmetaclub.com?jwt={ClientDataStorage.AccessToken}");
                    _webViewInitialized = true;

                    if (networkImageStream != null)
                        networkImageStream.SetTexture(_sharedWebView.GetComponentInChildren<RawImage>());
                }
                else
                {
                    RebindWebViewInput(_sharedWebView, parentCanvas, newFS);
                    _sharedWebView.gameObject.SetActive(true);

                    if (networkImageStream != null)
                        networkImageStream.SetTexture(_sharedWebView.GetComponentInChildren<RawImage>());
                }
            }
            finally
            {
                _opening = false;
            }
        }


        private async Task CloseAndCleanupAsync()
        {
            if (_closing) return;
            _closing = true;

            try
            {
                if (networkImageStream != null)
                    networkImageStream.ClearTexture();

                if (_sharedWebView != null)
                    _sharedWebView.gameObject.SetActive(false);

                if (clearAllDataOnClose) Web.ClearAllData();
            }
            finally
            {
                _closing = false;
            }
        }
        
        public static SlotMachineInteractable FindById(int id)
        {
            var all = FindObjectsByType<SlotMachineInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return all.FirstOrDefault(s => s.IDNumber == id);
        }
    }
}
