#if UNITY_EDITOR
using UnityEditor;

namespace Code.Network.HostMigration.Editor
{
    [CustomEditor(typeof(Components.SceneObject))]
    public class SceneObjectEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var sceneObject = (Components.SceneObject)target;

            if (PrefabUtility.IsPartOfPrefabAsset(sceneObject.gameObject))
            {
                EditorGUILayout.HelpBox(
                    "SceneObject добавлен на prefab. Этот компонент должен быть на объекте сцены!",
                    MessageType.Warning
                );
            }

            DrawDefaultInspector();
        }
    }
}
#endif