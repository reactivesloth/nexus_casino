#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Code.Server
{
    public class ServerBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(UnityEngine.SceneManagement.Scene scene, BuildReport report)
        {
            if (!BuildPipeline.isBuildingPlayer) return;

            if (report.summary.platform != BuildTarget.StandaloneLinux64) 
            {
                return;
            }

            Debug.Log($"[ServerStripping] Обработка сцены: {scene.name}...");

            var manifests = Object.FindObjectsByType<ServerStrippingList>(FindObjectsSortMode.None);

            if (manifests.Length == 0) return;

            int totalRemoved = 0;

            foreach (var manifest in manifests)
            {
                foreach (var obj in manifest.ObjectsToStrip)
                {
                    if (obj != null)
                    {
                        Debug.Log($"[ServerStripping] Удаление: {obj.name}");
                        Object.DestroyImmediate(obj);
                        totalRemoved++;
                    }
                }

                if (manifest.DestroySelfOnServer)
                {
                    Object.DestroyImmediate(manifest.gameObject);
                }
            }

            Debug.Log($"[ServerStripping] Итого удалено корневых объектов: {totalRemoved}.");
        }
    }
}
#endif