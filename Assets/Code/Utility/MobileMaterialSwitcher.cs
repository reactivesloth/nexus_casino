// Assets/Scripts/MaterialVariantSwitcher.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Code.Utility;

[DisallowMultipleComponent]
public class MaterialVariantSwitcher : MonoBehaviour
{
    [Tooltip("Путь внутри Resources к High-Quality-вариантам (Lit) без суффикса")]
    public string highQualityPath   = "GeneratedMaterials/DesktopMaterials";
    [Tooltip("Путь внутри Resources к Medium-Quality-вариантам (Baked Lit) без суффикса")]
    public string mediumQualityPath = "GeneratedMaterials/BakedMaterials";
    [Tooltip("Путь внутри Resources к Low-Quality-вариантам (Simple Lit) без суффикса")]
    public string lowQualityPath    = "GeneratedMaterials/MobileMaterials";

    [Tooltip("Принудительно низкий режим (для тестирования)")]
    public bool forceLowQuality = false;

    private Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();
    private int savedQualityLevel      = -1;
    private bool lastForceLowQuality   = false;

    void Start()
    {
        CacheOriginals();
    }

    void Update()
    {
        int level = SettingsManager.Instance.QualityLevel;
        if (level != savedQualityLevel || forceLowQuality != lastForceLowQuality)
        {
            savedQualityLevel    = level;
            lastForceLowQuality  = forceLowQuality;
            ApplyByLevel(level);
        }
    }

    private void ApplyByLevel(int level)
    {
        if (forceLowQuality)
        {
            // Принудительно самый низкий → Baked Lit
            ApplyVariant(mediumQualityPath, "_BakedLit");
            return;
        }

        if (level <= 0)
        {
            // Низкий → URP/Baked Lit
            ApplyVariant(mediumQualityPath, "_BakedLit");
        }
        else if (level == 1)
        {
            // Средний → URP/Simple Lit
            ApplyVariant(lowQualityPath, "_Mobile");
        }
        else
        {
            // Высокий → URP/Lit
            ApplyVariant(highQualityPath, "");
        }
    }

    private void ApplyVariant(string basePath, string suffix)
    {
        Debug.Log($"[MaterialVariantSwitcher] Applying variants from Resources/{basePath} (suffix '{suffix}')");
        foreach (var kv in _originals)
        {
            var rend      = kv.Key;
            var originals = kv.Value;
            var slots     = new Material[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                var orig = originals[i];
                if (orig == null)
                {
                    Debug.LogWarning($"[{rend.name}] Slot {i}: оригинальный материал = null");
                    slots[i] = null;
                    continue;
                }

                string resPath = $"{basePath}/{orig.name}{suffix}";
                var variant = Resources.Load<Material>(resPath);

                if (variant != null)
                {
                    Debug.Log($"[{rend.name}] Slot {i}: '{orig.name}' → загружен вариант '{resPath}'");
                    slots[i] = variant;
                }
                else
                {
                    Debug.LogWarning($"[{rend.name}] Slot {i}: вариант не найден по пути Resources/{resPath}, использую оригинал '{orig.name}'");
                    slots[i] = orig;
                }
            }

            rend.sharedMaterials = slots;
        }
    }

    public void RevertToOriginals()
    {
        foreach (var kv in _originals)
            kv.Key.sharedMaterials = kv.Value;
        Debug.Log("[MaterialVariantSwitcher] Reverted to original materials");
    }

    private void CacheOriginals()
    {
        _originals.Clear();
        foreach (var rend in FindAllRenderers())
            _originals[rend] = rend.sharedMaterials.Clone() as Material[];
        Debug.Log($"[MaterialVariantSwitcher] Cached {_originals.Count} renderer originals");
    }

    private static List<Renderer> FindAllRenderers()
    {
        var list = new List<Renderer>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded) continue;
            foreach (var go in scene.GetRootGameObjects())
                list.AddRange(go.GetComponentsInChildren<Renderer>(true));
        }
        return list;
    }
}
