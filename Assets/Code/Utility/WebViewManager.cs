using System.Collections.Generic;
using System.Threading.Tasks;
using Code.API;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Vuplex.WebView;

namespace Code.Utility
{
    public class WebViewManager : MonoBehaviour
    {
        public static WebViewManager Instance { get; private set; }

        [Header("Prefab and Parking")] [SerializeField]
        private CanvasWebViewPrefab webViewPrefab;

        [SerializeField] private Canvas parkingCanvas;
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Cleanup")] [SerializeField] private bool clearAllDataOnClose = false;
        [SerializeField] private bool deepCleanupStandalone = true;

        public CanvasWebViewPrefab WebViewPrefabInstance { get; private set; }
        public IWebView WebView => WebViewPrefabInstance != null ? WebViewPrefabInstance.WebView : null;
        public RawImage WebViewRawImage { get; private set; }
        public Canvas ParkingCanvas => parkingCanvas;

        private bool _initialized;
    
        [SerializeField] private bool refreshUrlOnHide;

        private CanvasWebViewPrefab _view;
        private RawImage _image;
        private string agregator;

        private List<CanvasWebViewPrefab> _webviews;
    
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            _webviews = new List<CanvasWebViewPrefab>();
        
            Web.SetAutoplayEnabled(true);
        }

        private async void Start()
        {
            await Task.Delay(2000);
            await EnsureCreatedAsync();
            var token = string.IsNullOrEmpty(ClientDataStorage.AccessToken) ? "" : ClientDataStorage.AccessToken;
            await LoadWithTokenAsync(agregator, token);
        }

        public async Task EnsureCreatedAsync()
        {
            if (WebViewPrefabInstance != null) return;
            if (webViewPrefab == null)
            {
                Debug.LogError("[WebViewManager] webViewPrefab is not assigned.");
                return;
            }

            Transform parent = parkingCanvas != null ? parkingCanvas.transform : transform;
            WebViewPrefabInstance = Instantiate(webViewPrefab, parent);
            var rt = WebViewPrefabInstance.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            WebViewPrefabInstance.transform.SetAsFirstSibling();

            await WebViewPrefabInstance.WaitUntilInitialized();
            _initialized = true;

            string fixAudioContextScript = @"
        (function() {
            // 1. Пытаемся восстановить AudioContext сразу
            var resumeAudio = function() {
                var contexts = [window.AudioContext, window.webkitAudioContext];
                contexts.forEach(function(Ctx) {
                    if (Ctx && Ctx.prototype.resume) {
                        // Перехватываем создание новых контекстов
                        var realCreate = Ctx.prototype.constructor;
                        // Пробуем возобновить существующие, если есть доступ к экземплярам (обычно нет, но для глобальных переменных поможет)
                    }
                });
            };

            // 2. Агрессивная симуляция клика для скрытия оверлея
            // Ищем элементы, похожие на оверлеи (обычно они на весь экран) и кликаем по центру
            setTimeout(function() {
                // Эмулируем клик по центру экрана
                var x = window.innerWidth / 2;
                var y = window.innerHeight / 2;
                var element = document.elementFromPoint(x, y);
                if (element) {
                    console.log('Auto-clicking element:', element);
                    element.click();
                    // Дополнительно шлем события мыши, так как некоторые фреймворки слушают их
                    var ev = new MouseEvent('click', {
                        'view': window,
                        'bubbles': true,
                        'cancelable': true,
                        'clientX': x,
                        'clientY': y
                    });
                    element.dispatchEvent(ev);
                }
            }, 500); // Небольшая задержка, чтобы сайт успел отрендерить оверлей
        })();
    ";
    
            WebViewPrefabInstance.WebView.PageLoadScripts.Add(fixAudioContextScript);
        
            WebViewRawImage = WebViewPrefabInstance.GetComponentInChildren<RawImage>(true);
            if (WebViewRawImage == null)
                Debug.LogWarning("[WebViewManager] RawImage inside CanvasWebViewPrefab (base) not found.");

            WebViewPrefabInstance.gameObject.SetActive(false);

            if (parkingCanvas != null)
            {
                if (!parkingCanvas.TryGetComponent<CanvasGroup>(out var cg))
                    cg = parkingCanvas.gameObject.AddComponent<CanvasGroup>();
                parkingCanvas.gameObject.SetActive(false);
                cg.blocksRaycasts = false;
                cg.interactable = false;
                cg.alpha = 0;
            }
        }

        public async Task LoadWithTokenAsync(string jwt, string agregator = "")
        {
            await EnsureCreatedAsync();
            LoadURL(agregator, jwt);
        }

        public void LoadURL(string jwt, string agregator = "")
        {
            if (WebView == null) return;
            var url = agregator != "" ? $"https://back.nexusmetaclub.com/games?agregator={agregator}&jwt={jwt}" : $"https://back.nexusmetaclub.com/games?jwt={jwt}";
            WebView.LoadUrl(url);
            Debug.Log($"[WebViewManager] {url}");
        }

        public void LoadURL(string link)
        {
            if (WebView == null) return;
            WebView.LoadUrl(link);
            Debug.Log($"[WebViewManager] {link}");
        }
    
        public void OpenFullscreen()
        {
            if (!_initialized || WebViewPrefabInstance == null || parkingCanvas == null) return;

            parkingCanvas.gameObject.SetActive(true);
            var canvasGroup = parkingCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = parkingCanvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            canvasGroup.alpha = 1;

            RebindToCanvas(WebViewPrefabInstance, parkingCanvas, bringToFront: true, worldSpace: false);
            
            WebViewPrefabInstance.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (WebViewPrefabInstance == null) return;

            WebViewPrefabInstance.gameObject.SetActive(false);
            // TODO: Find a way to destroy WebViewPrefabInstance without disposing WebView
            //Destroy(WebViewPrefabInstance.gameObject);
        
            if (parkingCanvas != null)
            {
                RebindToCanvas(WebViewPrefabInstance, parkingCanvas, bringToFront: false, worldSpace: false);
                parkingCanvas.gameObject.SetActive(false);
                var cg = parkingCanvas.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    cg.alpha = 0;
                }
            }
        }

        public CanvasWebViewPrefab EnsureWorldView(int slotId, Canvas worldCanvas)
        {
            if (!_initialized || WebView == null || worldCanvas == null) return null;

            if (_view == null)
            {
                _view = CanvasWebViewPrefab.Instantiate(WebView);
                _view.transform.SetParent(worldCanvas.transform, false);;
            }

            if (_view != null)
            {
                _view.gameObject.SetActive(true);
                _view.Resolution = Application.isMobilePlatform ? 0.75f : 1.0f;

                bool worldSpace = worldCanvas.renderMode == RenderMode.WorldSpace ||
                                  worldCanvas.renderMode == RenderMode.ScreenSpaceCamera;
                if (worldSpace && worldCanvas.worldCamera == null) worldCanvas.worldCamera = Camera.main;
                if (!worldCanvas.TryGetComponent<GraphicRaycaster>(out _))
                    worldCanvas.gameObject.AddComponent<GraphicRaycaster>();

                RebindToCanvas(_view, worldCanvas, bringToFront: false, worldSpace: worldSpace);

                return _view;
            }

            return null;
        }

        public RawImage ShowWorldView(int slotId, Canvas worldCanvas)
        {
            var view = EnsureWorldView(slotId, worldCanvas);

            if (parkingCanvas != null)
            {
                parkingCanvas.gameObject.SetActive(false);
                var cg = parkingCanvas.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.blocksRaycasts = false;
                    cg.interactable = false;
                    cg.alpha = 0;
                }
            }

            if (view != null)
            {
                view.gameObject.SetActive(true);

                _image = view.GetComponentInChildren<RawImage>(true);
            }

            return _image;
        }

        public void HideWorldView(int slotId, string agregator)
        {
            if (refreshUrlOnHide)
            {
                LoadURL(string.IsNullOrEmpty(ClientDataStorage.AccessToken) ? "" : ClientDataStorage.AccessToken, agregator);
            }

            if (_view != null) _view.gameObject.SetActive(false);
        }

        private void RebindToCanvas(CanvasWebViewPrefab prefab, Canvas canvas, bool bringToFront, bool worldSpace)
        {
            if (prefab == null || canvas == null) return;

            prefab.transform.SetParent(canvas.transform, false);

            if (bringToFront) prefab.transform.SetAsLastSibling();
            else prefab.transform.SetSiblingIndex(1);

            var rt = (RectTransform)prefab.transform;

            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            rt.anchoredPosition = Vector2.zero;
            var localPos = rt.localPosition;
            rt.localPosition = new Vector3(localPos.x, localPos.y, 0f);
        }

        public async Task ClearAllDataAsync(bool deepStandalone)
        {
#if (UNITY_STANDALONE || UNITY_EDITOR) && !UNITY_SERVER
            if (deepStandalone && WebViewPrefabInstance != null)
            {
                await StandaloneWebView.TerminateBrowserProcess();
            }
#endif
            Web.ClearAllData();
        }
    
        private void OnDestroy()
        {
            if (clearAllDataOnClose) _ = ClearAllDataAsync(deepCleanupStandalone);
        }
    
        public void OnPointerClick(PointerEventData eventData)
        {
            // Получаем точку клика в локальных координатах WebView (0..1)
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                webViewPrefab.transform as RectTransform, 
                eventData.position, 
                eventData.pressEventCamera, 
                out localPoint
            );

            // Нормализуем координаты (от 0 до 1)
            Rect rect = ((RectTransform)webViewPrefab.transform).rect;
            float x = (localPoint.x - rect.x) / rect.width;
            float y = (localPoint.y - rect.y) / rect.height;

            // Принудительно отправляем клик в браузер
            webViewPrefab.WebView.Click(new Vector2(x, y));
            Debug.Log($"Forced Click at {x}, {y}");
        }
    }
}