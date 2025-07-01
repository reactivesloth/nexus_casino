using System;
using System.Collections.Generic;
using System.Linq;
using Code.Network.HostMigration.Data;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace Code.Network.HostMigration.Components
{
    public class MigratableObject : NetworkBehaviour
    {
        [SerializeField] private RegisterType registerType;

        [Header("Object data")] [SerializeField]
        private bool isSceneObject;

        [SerializeField] private string sceneObjectId;
        [SerializeField] private List<Component> migratableComponents;

        private List<IMigratableComponentBase> MigratableComponentsList =>
            migratableComponents.Cast<IMigratableComponentBase>().ToList();

        public Guid ObjectGuid => Guid.Parse(sceneObjectId);

        private bool IsUniquenessSceneObjectId =>
            FindObjectsByType<MigratableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(o => o != this && o.isSceneObject).All(o => o.ObjectGuid != ObjectGuid);

        protected override void OnValidate()
        {
            base.OnValidate();
            PreprocessSceneObject();
            PreprocessComponents();
        }

        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            if (IsOwner && registerType == RegisterType.OnOwnership)
                HostMigrationManager.Instance.Register(this);
        }

        private void PreprocessSceneObject()
        {
            isSceneObject = gameObject.scene.IsValid() && !string.IsNullOrEmpty(gameObject.scene.name);

            if (!isSceneObject)
            {
                sceneObjectId = string.Empty;
                return;
            }

            if (!Guid.TryParse(sceneObjectId, out var guid))
                sceneObjectId = Guid.NewGuid().ToString();

            while (!IsUniquenessSceneObjectId)
                sceneObjectId = Guid.NewGuid().ToString();
        }

        private void PreprocessComponents() => migratableComponents =
            GetComponentsInChildren<Component>().Where(c => c is IMigratableComponentBase).ToList();

        public MigratableObjectData GetData()
        {
            var networkObject = gameObject.GetComponent<NetworkObject>();
            var prefabId = NetworkObject.UNSET_PREFABID_VALUE;
            if (networkObject)
                prefabId = networkObject.PrefabId;

            var componentsData = new List<MigratableComponentData>();
            MigratableComponentsList.ForEach(c => componentsData.Add(new MigratableComponentData
            {
                componentName = c.GetType().FullName,
                jsonData = c.GetJson(c.GetMigrateData())
            }));

            return new MigratableObjectData
            {
                objectName = gameObject.name,

                isSceneObject = isSceneObject,
                sceneObjectId = sceneObjectId,
                prefabId = prefabId,
                transformData = SerializableTransform.SetFromUnityTransform(transform),

                componentsData = componentsData
            };
        }

        public void RestoreData(MigratableObjectData migratableObjectData)
        {
            foreach (var data in migratableObjectData.componentsData)
            {
                Debug.Log($"[HostSessionRestorer] Process {data.componentName} component");

                if (gameObject.GetComponent(data.componentName) is not IMigratableComponentBase migratableComponent)
                {
                    Debug.LogError($"Component {data.componentName} not found on {gameObject.name}");
                    continue;
                }

                migratableComponent.OnMigrateDataReceived(data.jsonData);
            }
        }

        public static MigratableObject FindSceneObject(string id)
        {
            return FindObjectsByType<MigratableObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(o => o.sceneObjectId == id);
        }
    }

    enum RegisterType
    {
        None,
        OnOwnership
    }
}