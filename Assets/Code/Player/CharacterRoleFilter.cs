using UnityEngine;

namespace Code.Player
{
    public class CharacterRoleFilter : MonoBehaviour
    {
        public bool IsAdmin;
        public bool IsHost;
        public bool IsModerator;
        
        public bool IsAdminRole => IsAdmin || IsHost || IsModerator;
    }
}