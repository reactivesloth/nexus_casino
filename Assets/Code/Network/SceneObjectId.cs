using UnityEngine;

namespace Code.Network
{
    public class SceneObjectId : MonoBehaviour
    {
        public string Id;

        private void Reset()
        {
            if (string.IsNullOrEmpty(Id))
                Id = gameObject.name + "_" + System.Guid.NewGuid().ToString();
        }
    }
} 