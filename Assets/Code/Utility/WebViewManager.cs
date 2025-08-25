using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.API;
using CurvedUI;
using UnityEngine;
using UnityEngine.UI;
using Vuplex.WebView;

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

    private readonly Dictionary<int, CanvasWebViewPrefab> _worldViews = new Dictionary<int, CanvasWebViewPrefab>();
    private readonly Dictionary<int, RawImage> _worldViewRawImages = new Dictionary<int, RawImage>();
    [SerializeField] private bool refreshUrlOnHide;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);
    }

    private async void Start()
    {
        await Task.Delay(2000);
        await EnsureCreatedAsync();
        var token = string.IsNullOrEmpty(ClientDataStorage.AccessToken) ? "" : ClientDataStorage.AccessToken;
        await LoadWithTokenAsync(token);
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

    public async Task LoadWithTokenAsync(string jwt)
    {
        await EnsureCreatedAsync();
        if (WebView == null) return;
        string url = $"https://back.nexusmetaclub.com?jwt={jwt}";
        WebView.LoadUrl(url);
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

        var curved = WebViewPrefabInstance.GetComponentInChildren<CurvedUISettings>(true);
        if (curved != null) curved.enabled = false;

        RebindToCanvas(WebViewPrefabInstance, parkingCanvas, bringToFront: true, worldSpace: false);

        WebViewPrefabInstance.gameObject.SetActive(true);
        StartCoroutine(ForceCanvasRebuildNextFrame((RectTransform)WebViewPrefabInstance.transform));
    }

    public void Hide()
    {
        if (WebViewPrefabInstance == null) return;

        WebViewPrefabInstance.gameObject.SetActive(false);
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

        if (!_worldViews.TryGetValue(slotId, out var view) || view == null)
        {
            view = CanvasWebViewPrefab.Instantiate(WebView);
            _worldViews[slotId] = view;

            view.transform.SetParent(worldCanvas.transform, false);
            var rt = view.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            bool worldSpace = worldCanvas.renderMode == RenderMode.WorldSpace ||
                              worldCanvas.renderMode == RenderMode.ScreenSpaceCamera;
            if (worldSpace && worldCanvas.worldCamera == null)
                worldCanvas.worldCamera = Camera.main;
            if (!worldCanvas.TryGetComponent<GraphicRaycaster>(out _))
                worldCanvas.gameObject.AddComponent<GraphicRaycaster>();

            var curved = view.GetComponentInChildren<CurvedUISettings>(true);
            if (curved != null) curved.enabled = true;

            var raw = view.GetComponentInChildren<RawImage>(true);
            _worldViewRawImages[slotId] = raw;

            view.gameObject.SetActive(false);
        }
        else
        {
            bool worldSpace = worldCanvas.renderMode == RenderMode.WorldSpace ||
                              worldCanvas.renderMode == RenderMode.ScreenSpaceCamera;
            RebindToCanvas(view, worldCanvas, bringToFront: false, worldSpace: worldSpace);
        }

        return view;
    }

    public RawImage ShowWorldView(int slotId, Canvas worldCanvas)
    {
        var view = EnsureWorldView(slotId, worldCanvas);
        if (view == null) return null;

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

        view.gameObject.SetActive(true);
        StartCoroutine(ForceCanvasRebuildNextFrame((RectTransform)view.transform));

        return _worldViewRawImages.TryGetValue(slotId, out var raw) ? raw : null;
    }

    public void HideWorldView(int slotId)
    {
        if (refreshUrlOnHide)
        {
            var token = string.IsNullOrEmpty(ClientDataStorage.AccessToken) ? "" : ClientDataStorage.AccessToken;
            string url = $"https://back.nexusmetaclub.com?jwt={token}"; 
            WebView.LoadUrl(url);
        }
        
        if (_worldViews.TryGetValue(slotId, out var view) && view != null)
        {
            view.gameObject.SetActive(false);
        }
    }

    public void DestroyWorldView(int slotId)
    {
        if (_worldViews.TryGetValue(slotId, out var view) && view != null)
        {
            Destroy(view.gameObject);
        }

        _worldViews.Remove(slotId);
        _worldViewRawImages.Remove(slotId);
    }

    private void RebindToCanvas(CanvasWebViewPrefab prefab, Canvas canvas, bool bringToFront, bool worldSpace)
    {
        if (prefab == null || canvas == null) return;

        prefab.transform.SetParent(canvas.transform, false);
        if (bringToFront) prefab.transform.SetAsLastSibling();
        else prefab.transform.SetSiblingIndex(1);

        var rt = (RectTransform)prefab.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        if (worldSpace)
        {
            if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }
        else
        {
            canvas.worldCamera = null;
        }

        if (!canvas.TryGetComponent<GraphicRaycaster>(out _))
            canvas.gameObject.AddComponent<GraphicRaycaster>();
    }

    private IEnumerator ForceCanvasRebuildNextFrame(RectTransform rt)
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        Canvas.ForceUpdateCanvases();
    }

    public async Task ClearAllDataAsync(bool deepStandalone)
    {
#if UNITY_STANDALONE || UNITY_EDITOR
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
}