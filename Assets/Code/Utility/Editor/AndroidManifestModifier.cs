#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEditor.Android;

public class AndroidManifestModifier : IPostGenerateGradleAndroidProject
{
    public int callbackOrder => 999; // вызываем в конце

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // Путь к манифесту в итоговом проекте
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            UnityEngine.Debug.LogWarning("AndroidManifest.xml не найден: " + manifestPath);
            return;
        }

        var xmlDoc = new XmlDocument();
        xmlDoc.Load(manifestPath);

        // Пространство имён android
        var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsMgr.AddNamespace("android", "http://schemas.android.com/apk/res/android");

        // Находим тег <application>
        var applicationNode = xmlDoc.SelectSingleNode("/manifest/application");
        if (applicationNode == null)
        {
            UnityEngine.Debug.LogError("Не найден тег <application> в манифесте");
            return;
        }

        // Проверяем наличие атрибута
        var allowBackupAttr = applicationNode.Attributes["android:allowBackup", nsMgr.LookupNamespace("android")];
        if (allowBackupAttr == null)
        {
            // Если нет — добавляем
            allowBackupAttr = xmlDoc.CreateAttribute("android", "allowBackup", nsMgr.LookupNamespace("android"));
            allowBackupAttr.Value = "false"; // или "true", смени под свой кейс
            applicationNode.Attributes.Append(allowBackupAttr);
        }
        else
        {
            // Если есть — можно переопределить
            allowBackupAttr.Value = "false";
        }

        xmlDoc.Save(manifestPath);
        UnityEngine.Debug.Log("✅ AndroidManifest.xml успешно обновлён (allowBackup=false)");
    }
}
#endif