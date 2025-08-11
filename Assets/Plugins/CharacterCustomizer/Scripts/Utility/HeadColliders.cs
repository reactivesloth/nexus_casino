using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public class HeadColliders : MonoBehaviour
    {
        public List<ColliderSetup> colliders = new List<ColliderSetup>();

        public void createColliders()
        {
            if (colliders == null || colliders.Count == 0) return;

            for (int i = 0; i < colliders.Count; i++)
            {
                var setup = colliders[i];
                if (setup == null) continue;

                if (setup.mirror)
                {
                    CreateColliderObject(setup.position, setup.label + "_l");
                    Vector3 mirroredPosition = new Vector3(setup.position.x, setup.position.y, -setup.position.z);
                    CreateColliderObject(mirroredPosition, setup.label + "_r");
                }
                else
                {
                    CreateColliderObject(setup.position, setup.label);
                }
            }
        }

        private void CreateColliderObject(Vector3 localPosition, string label)
        {
            // avoid duplicates if called twice
            Transform existing = transform.Find(label);
            GameObject colliderObject;
            if (existing != null)
            {
                colliderObject = existing.gameObject;
                colliderObject.transform.localPosition = localPosition;
            }
            else
            {
                colliderObject = new GameObject(label);
                colliderObject.transform.SetParent(transform);
                colliderObject.transform.localPosition = localPosition;
                colliderObject.transform.localRotation = Quaternion.identity;
                colliderObject.transform.localScale = Vector3.one;

                SphereCollider sphereCollider = colliderObject.AddComponent<SphereCollider>();
                sphereCollider.radius = 0.03f;

                var thisCollider = GetComponent<Collider>();
                if (thisCollider != null) Physics.IgnoreCollision(thisCollider, sphereCollider, true);
            }
        }
    }

    [System.Serializable]
    public class ColliderSetup
    {
        public string label;
        public Vector3 position;
        public bool mirror;
    }
}