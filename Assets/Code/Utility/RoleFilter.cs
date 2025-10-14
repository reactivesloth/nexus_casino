using UnityEngine;

namespace Code.Utility
{
    public class RoleFilter : MonoBehaviour
    {
        public bool IsAdmin;
        public bool IsHost;
        public bool IsModerator;
        
        public bool IsAdminRole => IsAdmin || IsHost || IsModerator;
    }
}