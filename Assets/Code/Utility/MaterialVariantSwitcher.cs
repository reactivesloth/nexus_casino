using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Code.Utility;

[DisallowMultipleComponent]
public sealed class MobileMaterialSwitcher : MonoBehaviour
{
    [Tooltip("Resources/GeneratedMaterials/DesktopMaterials")]
    public string highQualityPath   = "GeneratedMaterials/DesktopMaterials";
    [Tooltip("Resources/GeneratedMaterials/BakedMaterials")]
    public string mediumQualityPath = "GeneratedMaterials/BakedMaterials";
    [Tooltip("Resources/GeneratedMaterials/MobileMaterials")]
    public string lowQualityPath    = "GeneratedMaterials/MobileMaterials";

    [Tooltip("Принудительный низкий режим (для тестов)")]
    public bool forceLowQuality;

    [Header("Debug")]
    public bool verboseLogs = false;

    private readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>(128);
    private readonly Dictionary<string, Material> _cache = new Dictionary<string, Material>(256);

    private bool _subscribedToSettings;
    private bool _waitingForSettings;

    private void OnEnable()
    {
        CacheOriginals();
        SceneManager.sceneLoaded += OnSceneLoaded;

        TrySubscribeToSettingsManager();         // подпишемся, если уже есть Instance
        if (!_subscribedToSettings && !_waitingForSettings)
            StartCoroutine(WaitAndSubscribe());  // иначе — дождёмся появления

        // Применяем сразу (на случай старта в середине сцены)
        ApplyByLevel(CurrentQualityLevel());
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromSettingsManager();
        _waitingForSettings = false;

        RevertToOriginals();
        _cache.Clear();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CacheOriginals();
        ApplyByLevel(CurrentQualityLevel());
    }

    private System.Collections.IEnumerator WaitAndSubscribe()
    {
        _waitingForSettings = true;
        // ждём, пока SettingsManager поднимется (или сменится сцена)
        while (SettingsManager.Instance == null)
            yield return null;

        TrySubscribeToSettingsManager();
        _waitingForSettings = false;

        // сразу применим после подписки (полезно при переходах)
        ApplyByLevel(CurrentQualityLevel());
    }

    private void TrySubscribeToSettingsManager()
    {
        if (_subscribedToSettings) return;
        var sm = SettingsManager.Instance;
        if (sm == null) return;

        sm.OnSettingsApplied += OnSettingsApplied;
        _subscribedToSettings = true;
        if (verboseLogs) Debug.Log("[MobileMaterialSwitcher] Subscribed to SettingsManager.OnSettingsApplied");
    }

    private void UnsubscribeFromSettingsManager()
    {
        if (!_subscribedToSettings) return;
        var sm = SettingsManager.Instance;
        if (sm != null) sm.OnSettingsApplied -= OnSettingsApplied;
        _subscribedToSettings = false;
        if (verboseLogs) Debug.Log("[MobileMaterialSwitcher] Unsubscribed from SettingsManager.OnSettingsApplied");
    }

    private int CurrentQualityLevel()
    {
        return SettingsManager.Instance != null
            ? SettingsManager.Instance.QualityLevel
            : QualitySettings.GetQualityLevel();
    }

    private void OnSettingsApplied()
    {
        if (verboseLogs) Debug.Log("[MobileMaterialSwitcher] OnSettingsApplied → applying variant");
        ApplyByLevel(CurrentQualityLevel());
    }

    private void ApplyByLevel(int level)
    {
        if (verboseLogs) Debug.Log($"[MobileMaterialSwitcher] Apply level={level}, forceLow={forceLowQuality}");

        if (forceLowQuality) { ApplyVariant(mediumQualityPath, "_BakedLit"); return; }

        // Подстрой под свою шкалу качества:
        //   0 → baked, 1 → mobile, 2+ → desktop
        if (level <= 0)      ApplyVariant(mediumQualityPath, "_BakedLit");
        else if (level == 1) ApplyVariant(lowQualityPath,    "_Mobile");
        else                 ApplyVariant(highQualityPath,   "");
    }

    private void ApplyVariant(string basePath, string suffix)
    {
        foreach (var kv in _originals)
        {
            var rend      = kv.Key;
            var originals = kv.Value;
            if (rend == null || originals == null) continue;

            var slots = new Material[originals.Length];
            for (int i = 0; i < originals.Length; i++)
            {
                var orig = originals[i];
                if (orig == null) { slots[i] = null; continue; }

                string resPath = $"{basePath}/{orig.name}{suffix}";
                if (!_cache.TryGetValue(resPath, out var variant) || variant == null)
                {
                    variant = Resources.Load<Material>(resPath);
                    _cache[resPath] = variant;
                    if (verboseLogs && variant == null)
                        Debug.LogWarning($"[MobileMaterialSwitcher] Variant NOT FOUND: Resources/{resPath}.mat");
                }
                slots[i] = variant != null ? variant : orig;
            }
            rend.sharedMaterials = slots;
        }
    }

    public void RevertToOriginals()
    {
        foreach (var kv in _originals)
        {
            var r = kv.Key; var mats = kv.Value;
            if (r != null && mats != null) r.sharedMaterials = mats;
        }
    }

    private void CacheOriginals()
    {
        _originals.Clear();

        // Соберём все Renderer из загруженных сцен
        var renderers = new List<Renderer>(256);
        int sc = SceneManager.sceneCount;
        for (int i = 0; i < sc; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;

            var roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var arr = roots[r].GetComponentsInChildren<Renderer>(true);
                for (int k = 0; k < arr.Length; k++)
                {
                    var rr = arr[k];
                    if (rr == null) continue;
                    _originals[rr] = (Material[])rr.sharedMaterials.Clone();
                }
            }
        }

        if (verboseLogs) Debug.Log($"[MobileMaterialSwitcher] Cached {_originals.Count} renderers");
    }

    /// <summary>Можно вызвать вручную для форс-применения.</summary>
    public void RefreshNow() => ApplyByLevel(CurrentQualityLevel());
}
