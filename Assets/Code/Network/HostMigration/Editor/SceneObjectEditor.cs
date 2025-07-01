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
                    "SceneObject added on prefab. This component has need add to object on scene!",
                    MessageType.Warning
                );
            }
            
            DrawDefaultInspector();
        }
    }
#endif
}