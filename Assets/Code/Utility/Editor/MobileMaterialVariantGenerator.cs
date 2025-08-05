// Assets/Editor/MobileMaterialVariantGenerator.cs
using UnityEditor;
using UnityEngine;
using System.IO;

public static class MobileMaterialGenerator
{
    private const string k_GeneratedFolder = "Assets/Resources/GeneratedMaterials";
    private const string k_DesktopFolder   = k_GeneratedFolder + "/DesktopMaterials";
    private const string k_MobileFolder    = k_GeneratedFolder + "/MobileMaterials";   // теперь для Simple Lit
    private const string k_BakedFolder     = k_GeneratedFolder + "/BakedMaterials";    // теперь для Baked Lit

    private const string k_LitShader     = "Universal Render Pipeline/Lit";
    private const string k_SimpleLit     = "Universal Render Pipeline/Simple Lit";      // среднее
    private const string k_BakedShader   = "Universal Render Pipeline/Baked Lit";       // низкое

    [MenuItem("Tools/Generate Material Variants (All Quality Levels)")]
    private static void GenerateMenu()
    {
        bool addNewOnly = EditorUtility.DisplayDialog(
            "Генерация вариантов материалов",
            "Добавить только новые или обновить все существующие?",
            "Добавить новые",
            "Обновить всё");
        GenerateVariants(addNewOnly);
    }

    private static void GenerateVariants(bool addNewOnly)
    {
        // Создаём папки, если нужно
        EnsureFolderExists(k_GeneratedFolder);
        EnsureFolderExists(k_DesktopFolder);
        EnsureFolderExists(k_MobileFolder);
        EnsureFolderExists(k_BakedFolder);

        var guids = AssetDatabase.FindAssets("t:Material");
        int createdHigh = 0, updatedHigh = 0, skippedHigh = 0;
        int createdMed  = 0, updatedMed  = 0, skippedMed  = 0;
        int createdLow  = 0, updatedLow  = 0, skippedLow  = 0;

        foreach (var guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            var origMat   = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (origMat == null || origMat.shader.name != k_LitShader)
                continue;

            // === High-Quality (Lit) ===
            string highFile = origMat.name + ".mat";
            string highPath = $"{k_DesktopFolder}/{highFile}";
            var    highMat  = AssetDatabase.LoadAssetAtPath<Material>(highPath);
            if (highMat != null)
            {
                if (addNewOnly) skippedHigh++;
                else
                {
                    highMat.shader = Shader.Find(k_LitShader);
                    highMat.CopyPropertiesFromMaterial(origMat);
                    highMat.shaderKeywords = origMat.shaderKeywords;
                    EditorUtility.SetDirty(highMat);
                    updatedHigh++;
                }
            }
            else
            {
                var newHigh = new Material(Shader.Find(k_LitShader));
                newHigh.CopyPropertiesFromMaterial(origMat);
                newHigh.shaderKeywords = origMat.shaderKeywords;
                AssetDatabase.CreateAsset(newHigh, highPath);
                createdHigh++;
            }

            // === Medium-Quality (Simple Lit) ===
            string medFile = origMat.name + "_Mobile.mat";
            string medPath = $"{k_MobileFolder}/{medFile}";
            var    medMat  = AssetDatabase.LoadAssetAtPath<Material>(medPath);
            if (medMat != null)
            {
                if (addNewOnly) skippedMed++;
                else
                {
                    medMat.shader = Shader.Find(k_SimpleLit);
                    medMat.CopyPropertiesFromMaterial(origMat);
                    medMat.shaderKeywords = origMat.shaderKeywords;
                    EditorUtility.SetDirty(medMat);
                    updatedMed++;
                }
            }
            else
            {
                var newMed = new Material(Shader.Find(k_SimpleLit));
                newMed.CopyPropertiesFromMaterial(origMat);
                newMed.shaderKeywords = origMat.shaderKeywords;
                AssetDatabase.CreateAsset(newMed, medPath);
                createdMed++;
            }

            // === Low-Quality (Baked Lit) ===
            string lowFile = origMat.name + "_BakedLit.mat";
            string lowPath = $"{k_BakedFolder}/{lowFile}";
            var    lowMat  = AssetDatabase.LoadAssetAtPath<Material>(lowPath);
            if (lowMat != null)
            {
                if (addNewOnly) skippedLow++;
                else
                {
                    lowMat.shader = Shader.Find(k_BakedShader);
                    lowMat.CopyPropertiesFromMaterial(origMat);
                    lowMat.shaderKeywords = origMat.shaderKeywords;
                    EditorUtility.SetDirty(lowMat);
                    updatedLow++;
                }
            }
            else
            {
                var newLow = new Material(Shader.Find(k_BakedShader));
                newLow.CopyPropertiesFromMaterial(origMat);
                newLow.shaderKeywords = origMat.shaderKeywords;
                AssetDatabase.CreateAsset(newLow, lowPath);
                createdLow++;
            }
        }

        AssetDatabase.SaveAssets();

        string summary =
            $"High   (Lit)      → создано: {createdHigh}, обновлено: {updatedHigh}, пропущено: {skippedHigh}\n" +
            $"Medium (SimpleLit)→ создано: {createdMed}, обновлено: {updatedMed}, пропущено: {skippedMed}\n" +
            $"Low    (BakedLit) → создано: {createdLow}, обновлено: {updatedLow}, пропущено: {skippedLow}";
        Debug.Log($"[MobileMaterialGenerator]\n{summary}");
        EditorUtility.DisplayDialog("Генерация завершена", summary, "OK");
    }

    private static void EnsureFolderExists(string folderPath)
    {
        if (AssetDatabase.IsValidFolder(folderPath)) return;
        string parent = Path.GetDirectoryName(folderPath).Replace("\\", "/");
        string name   = Path.GetFileName(folderPath);
        AssetDatabase.CreateFolder(parent, name);
    }
}
