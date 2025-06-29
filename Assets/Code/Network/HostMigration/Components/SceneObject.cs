using System;
using System.Linq;
using UnityEngine;

namespace Code.Network.HostMigration.Components
{
    public class SceneObject : MonoBehaviour
    {
        [SerializeField] private string objectId;

        public Guid ObjectGuid => Guid.Parse(objectId);

        private void OnValidate()
        {
            if (!Guid.TryParse(objectId, out var guid))
                objectId = Guid.NewGuid().ToString();
            
            while (!IsUniqueness())
                objectId = Guid.NewGuid().ToString();
        }

        private bool IsUniqueness()
        {
            var allSceneObjects = FindObjectsByType<SceneObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            return allSceneObjects.Where(o => o != this).All(o => o.ObjectGuid != ObjectGuid);
        }
    }
}