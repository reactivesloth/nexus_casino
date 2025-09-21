using UnityEngine;

namespace Code.Scene.SceneObjectControl
{
    public class ObjectActiveControl : MonoBehaviour, IControlledSceneObject
    {
        [SerializeField] private string objectName;
        
        public string Name => objectName;
        
        public void Action(string actionName)
        {
            switch (actionName)
            {
                case "enable":
                    gameObject.SetActive(true);
                    break;
                case "disable":
                    gameObject.SetActive(false);
                    break;
            }
        }
    }
}