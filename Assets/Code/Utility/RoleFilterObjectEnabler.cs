using Code.API;
using UnityEngine;

namespace Code.Utility
{
    [RequireComponent(typeof(RoleFilter))]
    public class RoleFilterObjectEnabler : MonoBehaviour
    {
        private RoleFilter _filter;
        [SerializeField] private GameObject[] roleFilterObjects;

        private void OnEnable()
        {
            _filter = GetComponent<RoleFilter>();
            
            if (_filter != null)
            {
                var filter = ClientDataStorage.UserData.IsAdminRole && _filter.IsAdminRole;
                foreach (var obj in roleFilterObjects)
                {
                    obj.SetActive(filter);
                }
            }
        }
    }
}