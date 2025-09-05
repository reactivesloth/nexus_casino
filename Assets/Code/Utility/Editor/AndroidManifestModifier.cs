#if UNITY_EDITOR
using System.IO;
using System.Xml;
using UnityEngine;
using UnityEditor.Android;

public class AndroidManifestModifier : IPostGenerateGradleAndroidProject
{ 
    public int callbackOrder => 999;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
        if (!File.Exists(manifestPath))
        {
            Debug.LogWarning("AndroidManifest.xml не найден: " + manifestPath);
            return;
        }

        var xmlDoc = new XmlDocument();
        xmlDoc.Load(manifestPath);

        var nsMgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsMgr.AddNamespace("android", "http://schemas.android.com/apk/res/android");

        var applicationNode = xmlDoc.SelectSingleNode("/manifest/application");
        if (applicationNode == null)
        {
            Debug.LogError("Не найден тег <application> в манифесте");
            return;
        }

        var allowBackupAttr = applicationNode.Attributes["android:allowBackup", nsMgr.LookupNamespace("android")];
        if (allowBackupAttr == null)
        {
            allowBackupAttr = xmlDoc.CreateAttribute("android", "allowBackup", nsMgr.LookupNamespace("android"));
            allowBackupAttr.Value = "false";
            applicationNode.Attributes.Append(allowBackupAttr);
        }
        else
        {
            allowBackupAttr.Value = "false";
        }

        var activityNodes = xmlDoc.SelectNodes("/manifest/application/activity") ?? throw new InvalidDataException("Illegal xml.");
        for (var i = 0; i < activityNodes.Count; i++)
        {
            var activityNode = activityNodes[i];;
            var nameAttribute = activityNode.Attributes!["android:name"];
            if (nameAttribute == null) continue;
            if (nameAttribute.Value != "com.unity3d.player.UnityPlayerGameActivity") continue;

            var launchAttribute = activityNode.Attributes!["android:launchMode"];
            if (launchAttribute != null)
            {
                launchAttribute.Value = "standard";
                Debug.Log("✅ Применен android:launchMode");
            }

            break;
        }

        xmlDoc.Save(manifestPath);
        Debug.Log("✅ AndroidManifest.xml успешно обновлён (allowBackup=false)");
    }
}
#endif