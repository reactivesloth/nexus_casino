// Assets/Editor/MobileMaterialGenerator.cs
using UnityEditor;
using UnityEngine;
using System.IO;

public static class MobileMaterialGenerator
{
    private const string k_GeneratedFolder = "Assets/Resources/GeneratedMaterials";
    private const string k_DesktopFolder   = k_GeneratedFolder + "/DesktopMaterials";
    private const string k_MobileFolder    = k_GeneratedFolder + "/MobileMaterials";

    private const string k_LitShader     = "Universal Render Pipeline/Lit";
    private const string k_SimpleLit     = "Universal Render Pipeline/Simple Lit";

    [MenuItem("Tools/Generate Material Variants (Desktop & Mobile)")]
    private static void GenerateMenu()
    {
        bool addNewOnly = EditorUtility.DisplayDialog(
            "Material Variants Generation",
            "Выберите режим генерации:\n\n" +
            "– «Добавить новые» (пропустить уже существующие)\n" +
            "– «Перегенерировать всё» (обновить все варианты)",
            "Добавить новые",
            "Перегенерировать всё"
        );

        GenerateVariants(addNewOnly);
    }

    private static void GenerateVariants(bool addNewOnly)
    {
        EnsureFolderExists(k_GeneratedFolder);
        EnsureFolderExists(k_DesktopFolder);
        EnsureFolderExists(k_MobileFolder);

        var guids = AssetDatabase.FindAssets("t:Material");
        int createdDesk = 0, updatedDesk = 0, skippedDesk = 0;
        int createdMob = 0,  updatedMob  = 0, skippedMob  = 0;

        foreach (var guid in guids)
        {
            string path   = AssetDatabase.GUIDToAssetPath(guid);
            var origMat   = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (origMat == null || origMat.shader.name != k_LitShader)
                continue;

            // ----- Desktop variant -----
            string deskFile   = origMat.name + ".mat";
            string deskPath   = $"{k_DesktopFolder}/{deskFile}";
            var    deskMat    = AssetDatabase.LoadAssetAtPath<Material>(deskPath);
            bool   deskExists = deskMat != null;

            if (deskExists)
            {
                if (addNewOnly)
                {
                    skippedDesk++;
                }
                else
                {
                    // Обновляем свойства на месте (сохраняем GUID и ссылки)
                    deskMat.shader = Shader.Find(k_LitShader);
                    deskMat.CopyPropertiesFromMaterial(origMat);
                    deskMat.shaderKeywords = origMat.shaderKeywords;
                    EditorUtility.SetDirty(deskMat);
                    updatedDesk++;
                }
            }
            else
            {
                // Создаём новый Desktop‑материал
                var newDesk = new Material(Shader.Find(k_LitShader));
                newDesk.CopyPropertiesFromMaterial(origMat);
                newDesk.shaderKeywords = origMat.shaderKeywords;
                AssetDatabase.CreateAsset(newDesk, deskPath);
                createdDesk++;
            }

            // ----- Mobile variant -----
            string mobFile   = origMat.name + "_Mobile.mat";
            string mobPath   = $"{k_MobileFolder}/{mobFile}";
            var    mobMat    = AssetDatabase.LoadAssetAtPath<Material>(mobPath);
            bool   mobExists = mobMat != null;

            if (mobExists)
            {
                if (addNewOnly)
                {
                    skippedMob++;
                }
                else
                {
                    // Обновляем свойства на месте
                    mobMat.shader = Shader.Find(k_SimpleLit);
                    mobMat.CopyPropertiesFromMaterial(origMat);
                    mobMat.shaderKeywords = origMat.shaderKeywords;
                    EditorUtility.SetDirty(mobMat);
                    updatedMob++;
                }
            }
            else
            {
                // Создаём новый Mobile‑материал
                var newMob = new Material(Shader.Find(k_SimpleLit));
                newMob.CopyPropertiesFromMaterial(origMat);
                newMob.shaderKeywords = origMat.shaderKeywords;
                AssetDatabase.CreateAsset(newMob, mobPath);
                createdMob++;
            }
        }

        AssetDatabase.SaveAssets();

        string summary =
            $"Desktop → создано: {createdDesk}, обновлено: {updatedDesk}, пропущено: {skippedDesk}\n" +
            $"Mobile  → создано: {createdMob},  обновлено: {updatedMob},  пропущено: {skippedMob}";
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
