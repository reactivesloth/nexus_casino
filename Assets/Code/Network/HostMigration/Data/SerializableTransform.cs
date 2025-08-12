using System;
using Newtonsoft.Json;
using UnityEngine;

namespace Code.Network.HostMigration.Data
{
    [Serializable]
    public struct SerializableTransform
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        [JsonIgnore] public UnityEngine.Vector3 GetUnityPosition => new(position.x, position.y, position.z);
        [JsonIgnore] public UnityEngine.Quaternion GetUnityRotation => new(rotation.x, rotation.y, rotation.z, rotation.w);
        [JsonIgnore] public UnityEngine.Vector3 GetUnityScale => new(scale.x, scale.y, scale.z);

        public static SerializableTransform SetFromUnityTransform(Transform fromTransform)
        {
            return new SerializableTransform
            {
                position = new Vector3(fromTransform.position.x, fromTransform.position.y, fromTransform.position.z),
                rotation = new Quaternion(fromTransform.rotation.x, fromTransform.rotation.y, fromTransform.rotation.z, fromTransform.rotation.w),
                scale    = new Vector3(fromTransform.localScale.x, fromTransform.localScale.y, fromTransform.localScale.z)
            };
        }

        public static SerializableTransform SetFromUnityTransformLocal(Transform fromTransform)
        {
            return new SerializableTransform
            {
                position = new Vector3(fromTransform.localPosition.x, fromTransform.localPosition.y, fromTransform.localPosition.z),
                rotation = new Quaternion(fromTransform.localRotation.x, fromTransform.localRotation.y, fromTransform.localRotation.z, fromTransform.localRotation.w),
                scale    = new Vector3(fromTransform.localScale.x, fromTransform.localScale.y, fromTransform.localScale.z)
            };
        }

        public override string ToString() => $"{GetUnityPosition}.{GetUnityRotation}";
    }

    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Quaternion { public float x, y, z, w; public Quaternion(float x, float y, float z, float w) { this.x=x; this.y=y; this.z=z; this.w=w; } }
}
