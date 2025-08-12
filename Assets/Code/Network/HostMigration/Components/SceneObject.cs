using System;
using UnityEngine;

namespace Code.Network.HostMigration.Components
{
    public class SceneObject : MonoBehaviour
    {
        [SerializeField] private string objectId;

        public Guid ObjectGuid => Guid.TryParse(objectId, out var g) ? g : Guid.Empty;

#if UNITY_EDITOR
        private void OnValidate() => GenId();

        private void GenId()
        {
            if (!gameObject.scene.IsValid() || string.IsNullOrEmpty(gameObject.scene.name))
            {
                objectId = string.Empty;
                return;
            }

            if (!Guid.TryParse(objectId, out _))
                objectId = Guid.NewGuid().ToString();

            // проверка уникальности
            int safety = 0;
            while (!IsUnique() && safety++ < 1000)
                objectId = Guid.NewGuid().ToString();
        }

        private bool IsUnique()
        {
            var all = FindObjectsByType<SceneObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var meGuid = ObjectGuid;
            for (int i = 0; i < all.Length; i++)
            {
                var other = all[i];
                if (other == null || other == this) continue;
                if (other.ObjectGuid == meGuid) return false;
            }
            return true;
        }
#endif

        public static SceneObject GetObjectById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var all = FindObjectsByType<SceneObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                var so = all[i];
                if (so != null && so.objectId == id) return so;
            }
            return null;
        }
    }
}