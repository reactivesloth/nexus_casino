using Unity.Cinemachine;
using UnityEngine;

namespace Code.Utility
{
    public class CameraLight : MonoBehaviour
    {
        [SerializeField] private Light light;
        private Cinemachine3rdPersonFollow virtualCamera;
        private Transform t;
        
        private void Awake()
        {
            t = transform;   
            virtualCamera = FindObjectOfType<CinemachineVirtualCamera>().GetCinemachineComponent<Cinemachine3rdPersonFollow>();
        }

        private void Update()
        {
            if (virtualCamera != null && light != null)
            {
                var distance = Vector3.Distance(virtualCamera.FollowTargetPosition, t.position);
                light.intensity = distance + 0.1f;
                light.range = distance * 2 + 0.1f;
            }
        }
    }
}
