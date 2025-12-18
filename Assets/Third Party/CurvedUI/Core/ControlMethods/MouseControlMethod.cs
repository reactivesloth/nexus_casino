using System;
using CurvedUI.Core.Utilities;
using UnityEngine;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.TouchPhase;
#if ENABLE_INPUT_SYSTEM && CURVEDUI_NEW_INPUT // we need both because enabled new input backends != imported package
using UnityEngine.InputSystem;
#endif
[assembly: OptionalDependency("UnityEngine.InputSystem.Mouse", "CURVEDUI_NEW_INPUT")] 

namespace CurvedUI.Core.ControlMethods
{
    [Serializable]
    public class MouseControlMethod : CurvedUIControlMethod
    {
        #region PUBLIC
        public override void Initialize(bool isPlayMode) { }

        public override ControlArgs Process(Hand usedHand, Camera mainEventCamera) 
            => new()
            {
                Ray = GetEventRay(usedHand, mainEventCamera),
                ButtonState = MouseLeftButtonIsPressed,
            };

        public override Ray GetEventRay(Hand usedHand, Camera eventCam = null)
        {
            if (eventCam == null)
            {
                Debug.LogError("CURVEDUI: No camera provided for mouse ray cast. Returning empty ray.");
                return new Ray();
            }
                
            // Get a ray from the camera through the point on the screen - used for mouse input
            return eventCam.ScreenPointToRay(MousePosition);
        }

        public override Transform GetPointerTransform(Hand usedHand)
        {
            Debug.LogError($"CurvedUI: {nameof(MouseControlMethod)} does not have a pointer transform.");
            return null;
        }
        #endregion
        
        
        #region SETTERS AND GETTERS
        public static Vector2 MousePosition => Touch.activeTouches.Count > 0 ? Touch.activeTouches[0].screenPosition : Mouse.current.position.ReadValue();

        public static bool MouseLeftButtonIsPressed => Touch.activeTouches.Count > 0 || Mouse.current.leftButton.isPressed;

        #endregion
    }
}
