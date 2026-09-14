using TMPro;
using UnityEngine;

namespace Code.UI.Admin
{
    public abstract class ControlElement : MonoBehaviour
    {
        [SerializeField] protected TMP_Text titleDisplayText;
        public string SearchKey { get; protected set; }
    }
}