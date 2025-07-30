using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MaterialVariantSwitcher : MonoBehaviour
{
    [Tooltip("Путь внутри Resources к Desktop‑вариантам (без суффикса)")]
    public string desktopResourcesPath = "GeneratedMaterials/DesktopMaterials";
    [Tooltip("Путь внутри Resources к Mobile‑вариантам (_Mobile суффикс)")]
    public string mobileResourcesPath  = "GeneratedMaterials/MobileMaterials";

    [Tooltip("Принудительно мобильный режим (для тестирования в редакторе)")]
    public bool forceMobileMode = false;

    // Кэш оригинальных sharedMaterials
    private Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();

    void Start()
    {
        CacheOriginals();
        bool useMobile = forceMobileMode || Application.isMobilePlatform;
        Apply(useMobile);
    }

    /// <summary>
    /// Ручной вызов в рантайме, чтобы переключиться
    /// </summary>
    public void SwitchMode(bool useMobile)
    {
        forceMobileMode = useMobile;
        Apply(useMobile);
    }

    /// <summary>
    /// Контекстное меню в инспекторе: применить Desktop‑варианты
    /// </summary>
    [ContextMenu("Apply Desktop Variants")]
    private void ContextApplyDesktop() => Apply(false);

    /// <summary>
    /// Контекстное меню в инспекторе: применить Mobile‑варианты
    /// </summary>
    [ContextMenu("Apply Mobile Variants")]
    private void ContextApplyMobile() => Apply(true);

    /// <summary>
    /// Контекстное меню в инспекторе: вернуть оригиналы
    /// </summary>
    [ContextMenu("Revert To Originals")]
    private void ContextRevert() => RevertToOriginals();

    /// <summary>
    /// Кэшируем на старте оригинальный массив sharedMaterials у каждого Renderer.
    /// </summary>
    private void CacheOriginals()
    {
        _originals.Clear();
        foreach (var rend in FindAllRenderers())
        {
            _originals[rend] = rend.sharedMaterials.Clone() as Material[];
        }
        Debug.Log($"[MaterialVariantSwitcher] Cached {_originals.Count} renderers’ originals");
    }

    /// <summary>
    /// Основная логика подмены: пытается загрузить из Resources,
    /// иначе возвращает оригинал из кэша.
    /// </summary>
    private void Apply(bool useMobile)
    {
        string basePath = useMobile ? mobileResourcesPath : desktopResourcesPath;
        string suffix   = useMobile ? "_Mobile" : "";

        Debug.Log($"[MaterialVariantSwitcher] Applying {(useMobile ? "Mobile" : "Desktop")} variants from Resources/{basePath}");

        foreach (var kv in _originals)
        {
            var rend     = kv.Key;
            var originals = kv.Value;
            var slots    = new Material[originals.Length];

            for (int i = 0; i < originals.Length; i++)
            {
                var orig = originals[i];
                if (orig == null)
                {
                    slots[i] = null;
                    continue;
                }

                string resPath = $"{basePath}/{orig.name}{suffix}";
                var variant = Resources.Load<Material>(resPath);
                if (variant != null)
                {
                    slots[i] = variant;
                    Debug.Log($"[MaterialVariantSwitcher] Loaded variant {resPath}");
                }
                else
                {
                    slots[i] = orig;
                    Debug.LogWarning($"[MaterialVariantSwitcher] Variant not found at {resPath}, using original {orig.name}");
                }
            }

            rend.sharedMaterials = slots;
        }
    }

    /// <summary>
    /// Возвращает всем renderers их оригинальные материалы
    /// </summary>
    public void RevertToOriginals()
    {
        foreach (var kv in _originals)
            kv.Key.sharedMaterials = kv.Value;
        Debug.Log("[MaterialVariantSwitcher] Reverted to original scene materials");
    }

    /// <summary>
    /// Собирает все Renderer из загруженных сцен
    /// </summary>
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
