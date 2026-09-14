using UnityEngine;

namespace Code.Tests
{
    public class EventSystemEnabler : MonoBehaviour
    {
        [SerializeField] private Behaviour enableComponent;

        private void Update()
        {
            if (enableComponent != null && !enableComponent.enabled)
                enableComponent.enabled = true;
        }
    }
}