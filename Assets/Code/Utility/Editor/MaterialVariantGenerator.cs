// Assets/Editor/MobileMaterialVariantGenerator.cs
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

public static class MaterialVariantGenerator
{
    private const string k_GeneratedFolder = "Assets/Resources/GeneratedMaterials";
    private const string k_DesktopFolder   = k_GeneratedFolder + "/DesktopMaterials";
    private const string k_MobileFolder    = k_GeneratedFolder + "/MobileMaterials";    // Simple Lit
    private const string k_BakedFolder     = k_GeneratedFolder + "/BakedMaterials";     // Baked Lit
    private const string k_UltraLowFolder  = k_GeneratedFolder + "/UltraLowMaterials";  // Unlit  ← NEW

    private const string k_LitShader     = "Universal Render Pipeline/Lit";
    private const string k_SimpleLit     = "Universal Render Pipeline/Simple Lit";
    private const string k_BakedShader   = "Universal Render Pipeline/Baked Lit";
    private const string k_UnlitShader   = "Universal Render Pipeline/Unlit";           // ← NEW

    [MenuItem("Tools/Materials/Generate Variants (All Quality Levels)")]
    private static void GenerateMenu()
    {
        bool addNewOnly = EditorUtility.DisplayDialog(
            "Генерация вариантов материалов",
            "Добавить только новые или обновить все существующие?",
            "Добавить новые",
            "Обновить всё");

        GenerateVariants(addNewOnly);
    }

    [MenuItem("Tools/Materials/Clear Generated Variants")]
    private static void ClearGenerated()
    {
        if (!AssetDatabase.IsValidFolder(k_GeneratedFolder))
        {
            EditorUtility.DisplayDialog("Очистка", "Папка с вариантами ещё не создавалась.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Удалить сгенерированные материалы?",
                $"Будет удалено содержимое:\n{k_DesktopFolder}\n{k_MobileFolder}\n{k_BakedFolder}\n{k_UltraLowFolder}", "Удалить", "Отмена"))
            return;

        void SafeDeleteFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                FileUtil.DeleteFileOrDirectory(path);
        }

        SafeDeleteFolder(k_DesktopFolder);
        SafeDeleteFolder(k_MobileFolder);
        SafeDeleteFolder(k_BakedFolder);
        SafeDeleteFolder(k_UltraLowFolder); // ← NEW
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Готово", "Генерированные варианты удалены.", "OK");
    }

    private static void GenerateVariants(bool addNewOnly)
    {
        EnsureFolderExists(k_GeneratedFolder);
        EnsureFolderExists(k_DesktopFolder);
        EnsureFolderExists(k_MobileFolder);
        EnsureFolderExists(k_BakedFolder);
        EnsureFolderExists(k_UltraLowFolder); // ← NEW

        var guids = AssetDatabase.FindAssets("t:Material");
        int total = guids.Length;

        int createdHigh = 0, updatedHigh = 0, skippedHigh = 0;
        int createdMed  = 0, updatedMed  = 0, skippedMed  = 0;
        int createdLow  = 0, updatedLow  = 0, skippedLow  = 0;
        int createdUltra= 0, updatedUltra= 0, skippedUltra= 0; // ← NEW

        var litShader   = Shader.Find(k_LitShader);
        var simpleLit   = Shader.Find(k_SimpleLit);
        var bakedLit    = Shader.Find(k_BakedShader);
        var unlit       = Shader.Find(k_UnlitShader); // ← NEW

        if (litShader == null || simpleLit == null || bakedLit == null || unlit == null)
        {
            EditorUtility.DisplayDialog("Ошибка",
                "Не найдены один или несколько шейдеров URP:\n- " +
                k_LitShader + "\n- " + k_SimpleLit + "\n- " + k_BakedShader + "\n- " + k_UnlitShader,
                "OK");
            return;
        }

        bool IsInGenerated(string path) => path.StartsWith(k_GeneratedFolder);

        try
        {
            for (int index = 0; index < total; index++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Генерация материалов",
                        $"Обработка {index + 1}/{total}", (float)index / total))
                    break;

                string path = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (string.IsNullOrEmpty(path) || IsInGenerated(path)) continue;

                var origMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (origMat == null) continue;

                // Генерим только для URP/Lit (оригинал)
                if (origMat.shader == null || origMat.shader.name != k_LitShader)
                    continue;

                // === Desktop (Lit) ===
                {
                    string file = $"{origMat.name}.mat";
                    string outPath = $"{k_DesktopFolder}/{file}";
                    var dst = AssetDatabase.LoadAssetAtPath<Material>(outPath);

                    if (dst != null)
                    {
                        if (addNewOnly) skippedHigh++;
                        else
                        {
                            ApplyBaseCopy(dst, litShader, origMat);
                            SanitizeForShader(dst, k_LitShader);
                            EditorUtility.SetDirty(dst);
                            updatedHigh++;
                        }
                    }
                    else
                    {
                        var newMat = new Material(litShader);
                        ApplyBaseCopy(newMat, litShader, origMat);
                        SanitizeForShader(newMat, k_LitShader);
                        AssetDatabase.CreateAsset(newMat, outPath);
                        createdHigh++;
                    }
                }

                // === Mobile (Simple Lit) ===
                {
                    string file = $"{origMat.name}_Mobile.mat";
                    string outPath = $"{k_MobileFolder}/{file}";
                    var dst = AssetDatabase.LoadAssetAtPath<Material>(outPath);

                    if (dst != null)
                    {
                        if (addNewOnly) skippedMed++;
                        else
                        {
                            ApplyBaseCopy(dst, simpleLit, origMat);
                            SanitizeForShader(dst, k_SimpleLit);
                            EditorUtility.SetDirty(dst);
                            updatedMed++;
                        }
                    }
                    else
                    {
                        var newMat = new Material(simpleLit);
                        ApplyBaseCopy(newMat, simpleLit, origMat);
                        SanitizeForShader(newMat, k_SimpleLit);
                        AssetDatabase.CreateAsset(newMat, outPath);
                        createdMed++;
                    }
                }

                // === Baked (Baked Lit) ===
                {
                    string file = $"{origMat.name}_BakedLit.mat";
                    string outPath = $"{k_BakedFolder}/{file}";
                    var dst = AssetDatabase.LoadAssetAtPath<Material>(outPath);

                    if (dst != null)
                    {
                        if (addNewOnly) skippedLow++;
                        else
                        {
                            ApplyBaseCopy(dst, bakedLit, origMat);
                            SanitizeForShader(dst, k_BakedShader);
                            EditorUtility.SetDirty(dst);
                            updatedLow++;
                        }
                    }
                    else
                    {
                        var newMat = new Material(bakedLit);
                        ApplyBaseCopy(newMat, bakedLit, origMat);
                        SanitizeForShader(newMat, k_BakedShader);
                        AssetDatabase.CreateAsset(newMat, outPath);
                        createdLow++;
                    }
                }

                // === UltraLow (Unlit) ===  ← NEW
                {
                    string file = $"{origMat.name}_Unlit.mat";
                    string outPath = $"{k_UltraLowFolder}/{file}";
                    var dst = AssetDatabase.LoadAssetAtPath<Material>(outPath);

                    if (dst != null)
                    {
                        if (addNewOnly) skippedUltra++;
                        else
                        {
                            ApplyBaseCopy(dst, unlit, origMat);
                            SanitizeForShader(dst, k_UnlitShader);
                            EditorUtility.SetDirty(dst);
                            updatedUltra++;
                        }
                    }
                    else
                    {
                        var newMat = new Material(unlit);
                        ApplyBaseCopy(newMat, unlit, origMat);
                        SanitizeForShader(newMat, k_UnlitShader);
                        AssetDatabase.CreateAsset(newMat, outPath);
                        createdUltra++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();

        string summary =
            $"High     (Lit)        → создано: {createdHigh}, обновлено: {updatedHigh}, пропущено: {skippedHigh}\n" +
            $"Medium   (SimpleLit)  → создано: {createdMed},  обновлено: {updatedMed},  пропущено: {skippedMed}\n" +
            $"Low      (BakedLit)   → создано: {createdLow},  обновлено: {updatedLow},  пропущено: {skippedLow}\n" +
            $"UltraLow (Unlit)      → создано: {createdUltra},обновлено: {updatedUltra},пропущено: {skippedUltra}";
        Debug.Log($"[MobileMaterialGenerator]\n{summary}");
        EditorUtility.DisplayDialog("Генерация завершена", summary, "OK");
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;

        string parent = Path.GetDirectoryName(folderPath)?.Replace("\\", "/");
        string name   = Path.GetFileName(folderPath);

        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolderExists(parent);

        AssetDatabase.CreateFolder(parent, name);
    }

    private static void ApplyBaseCopy(Material dst, Shader targetShader, Material srcLit)
    {
        if (dst == null || srcLit == null) return;

        dst.shader = targetShader;

        dst.renderQueue = srcLit.renderQueue;
        dst.enableInstancing = srcLit.enableInstancing;
        dst.doubleSidedGI = srcLit.doubleSidedGI;

        dst.CopyPropertiesFromMaterial(srcLit);

        dst.shaderKeywords = srcLit.shaderKeywords;

        CopyTextureIfExists(srcLit, dst, "_BaseMap");
        CopyTextureIfExists(srcLit, dst, "_MainTex");
        CopyTextureIfExists(srcLit, dst, "_BumpMap");
        CopyTextureIfExists(srcLit, dst, "_NormalMap");
        CopyTextureIfExists(srcLit, dst, "_MetallicGlossMap");
        CopyTextureIfExists(srcLit, dst, "_SpecGlossMap");
        CopyTextureIfExists(srcLit, dst, "_EmissionMap");
        CopyTextureIfExists(srcLit, dst, "_OcclusionMap");
        CopyTextureIfExists(srcLit, dst, "_DetailAlbedoMap");

        CopyColorIfExists(srcLit, dst, "_BaseColor");
        CopyColorIfExists(srcLit, dst, "_Color");
        CopyColorIfExists(srcLit, dst, "_EmissionColor");
    }

    private static void SanitizeForShader(Material m, string shaderName)
    {
        if (m == null) return;

        TrySetFloat(m, "_Surface", m.HasProperty("_Surface") ? m.GetFloat("_Surface") : 0f);
        TrySetFloat(m, "_Cutoff",  m.HasProperty("_Cutoff")  ? m.GetFloat("_Cutoff")  : 0.5f);

        if (m.HasProperty("_BumpMap"))
        {
            var nm = m.GetTexture("_BumpMap");
            TrySetFloat(m, "_BumpScale", nm != null ? m.GetFloat("_BumpScale") : 0f);
        }

        if (m.HasProperty("_EmissionColor"))
        {
            Color ec = m.GetColor("_EmissionColor");
            if (ec.maxColorComponent <= 0.0001f)
                m.DisableKeyword("_EMISSION");
            else
                m.EnableKeyword("_EMISSION");
        }
    }

    private static void CopyTextureIfExists(Material src, Material dst, string name)
    {
        if (src.HasProperty(name) && dst.HasProperty(name))
        {
            var tex = src.GetTexture(name);
            if (tex != null) dst.SetTexture(name, tex);
        }
    }

    private static void CopyColorIfExists(Material src, Material dst, string name)
    {
        if (src.HasProperty(name) && dst.HasProperty(name))
            dst.SetColor(name, src.GetColor(name));
    }

    private static void TrySetFloat(Material m, string name, float value)
    {
        if (m.HasProperty(name)) m.SetFloat(name, value);
    }
}
