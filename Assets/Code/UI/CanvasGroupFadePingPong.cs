using UnityEngine;

namespace Code.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class CanvasGroupFadePingPong : MonoBehaviour
    {
        [SerializeField] private float speed = 0.25f;
        
        private CanvasGroup _canvasGroup;
        private float _alpha;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        public void Update()
        {
            _alpha = Mathf.PingPong(Time.time * speed, 1f);
            
            _canvasGroup.alpha = _alpha;
        }
    }
}
