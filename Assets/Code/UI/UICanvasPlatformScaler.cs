using UnityEngine;
using UnityEngine.UI;

namespace Code.UI
{
    [RequireComponent(typeof(CanvasScaler))]
    public class UICanvasPlatformScaler : MonoBehaviour
    {
        [SerializeField] private int mobileScale = 800;
        [SerializeField] private int desktopScale = 1400;

#if UNITY_EDITOR
        [SerializeField] private bool forceMobile = false;
#endif
        
        private CanvasScaler _canvas;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            InitCanvasScaler();
        }
#endif
        
        private void Awake()
        {
            InitCanvasScaler();
        }
        
        private void InitCanvasScaler()
        {
            _canvas = gameObject.GetComponent<CanvasScaler>();
            
            var isMobile = Application.isMobilePlatform;
#if UNITY_EDITOR
            isMobile = forceMobile;
#endif      
            _canvas.referenceResolution = new Vector2(_canvas.referenceResolution.x, isMobile ? mobileScale : desktopScale);
        }
    }
}
