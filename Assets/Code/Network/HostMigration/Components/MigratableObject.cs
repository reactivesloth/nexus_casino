using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Code.Network.HostMigration.Components
{
    public class MigratableObject : MonoBehaviour
    {
        [SerializeField] private bool hasRegisterOnAwake = true;
        [SerializeField] private bool hasIncludeComponentsInChildren = true;

        public List<IMigratableBase> MigratableComponents =>
            (hasIncludeComponentsInChildren ? GetComponentsInChildren<Component>() : GetComponents<Component>())
            .OfType<IMigratableBase>().ToList();

        private void Awake()
        {
            //if(hasRegisterOnAwake)
                //HostMigrator.Instance.RegisterMigratableObject(this);
        }
    }
}