using System;
using UnityEngine;

namespace Code.UI
{
    public class UISafeAreaHandler : MonoBehaviour
    {
        void Awake()
        {
            ApplySafeArea();
        }
        
        void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            RectTransform rectTransform = GetComponent<RectTransform>();
            
            float offset = 0;

#if !UNITY_IOS
            offset = Screen.width - safeArea.width;
#endif
            
            // Convert safe area from screen coordinates to Canvas's local coordinates
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;

            anchorMin.x /= Screen.width - offset / 2;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width + offset / 2;
            anchorMax.y /= Screen.height;

            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}