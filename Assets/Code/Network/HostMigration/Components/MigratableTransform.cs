using Code.Network.HostMigration.Data;
using FishNet.Component.Transforming;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;
using DataQuaternion = Code.Network.HostMigration.Data.Quaternion;
using DataVector3 = Code.Network.HostMigration.Data.Vector3;

namespace Code.Network.HostMigration.Components
{
    [RequireComponent(typeof(NetworkTransform))]
    public class MigratableTransform : MonoBehaviour, IMigratable<SerializableTransform>
    {
        public void SetMigrateData(SerializableTransform data)
        {
            transform.position = new Vector3(data.position.x, data.position.y, data.position.z);
            transform.rotation = new Quaternion(data.rotation.x, data.rotation.y, data.rotation.z, data.rotation.w);
            transform.localScale = new Vector3(data.scale.x, data.scale.y, data.scale.z);
        }

        public SerializableTransform GetMigrateData()
        {
            return new SerializableTransform
            {
                position = new DataVector3(transform.position.x, transform.position.y, transform.position.z),
                rotation = new DataQuaternion(transform.rotation.x, transform.rotation.y, transform.rotation.z, transform.rotation.w),
                scale = new DataVector3(transform.localScale.x, transform.localScale.y, transform.localScale.z)
            };
        }
    }
}