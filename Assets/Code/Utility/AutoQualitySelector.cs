using UnityEngine;

public sealed class AutoQualitySelector : MonoBehaviour
{
    [SerializeField] private bool applyExpensiveChanges = true;

    private void Awake()
    {
        if (PlayerPrefs.HasKey("GraphicsQuality"))
        {
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("GraphicsQuality"), applyExpensiveChanges);
            return;
        }

        int level = ChooseQualityIndex();
        QualitySettings.SetQualityLevel(level, applyExpensiveChanges);
        PlayerPrefs.SetInt("GraphicsQuality", level);
    }

    private int ChooseQualityIndex()
    {
        int shaderLevel = SystemInfo.graphicsShaderLevel;
        int vRamMB      = SystemInfo.graphicsMemorySize;
        int cpuCores    = SystemInfo.processorCount;
        int width       = Screen.currentResolution.width;
        int height      = Screen.currentResolution.height;
        float megapixels = (width * height) / 1_000_000f;

        int score = 0;

        if (shaderLevel >= 45) score += 4;
        else if (shaderLevel >= 35) score += 3;
        else if (shaderLevel >= 30) score += 2;
        else if (shaderLevel >= 20) score += 1;

        if (vRamMB >= 6144) score += 4;
        else if (vRamMB >= 4096) score += 3;
        else if (vRamMB >= 2048) score += 2;
        else if (vRamMB >= 1024) score += 1;

        if (cpuCores >= 10) score += 3;
        else if (cpuCores >= 6) score += 2;
        else if (cpuCores >= 4) score += 1;

        if (megapixels > 5.0f) score -= 3;
        else if (megapixels > 3.0f) score -= 2;
        else if (megapixels > 2.0f) score -= 1;

        int levels = QualitySettings.names.Length;
        int index;
        if (score <= 1) index = Mathf.Min(0, levels - 1);
        else if (score <= 3) index = Mathf.Min(1, levels - 1);
        else if (score <= 5) index = Mathf.Min(2, levels - 1);
        else if (score <= 7) index = Mathf.Min(3, levels - 1);
        else index = Mathf.Min(4, levels - 1);

        index = Mathf.Clamp(index, 0, levels - 1);
        return index;
    }
}
