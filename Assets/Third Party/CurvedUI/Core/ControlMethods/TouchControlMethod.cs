using System;
using CurvedUI.Core.Utilities;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM && CURVEDUI_NEW_INPUT
using UnityEngine.InputSystem;
#endif

// Добавляем опциональную зависимость для Touchscreen, аналогично Mouse в оригинале
[assembly: OptionalDependency("UnityEngine.InputSystem.Touchscreen", "CURVEDUI_NEW_INPUT")]

namespace CurvedUI.Core.ControlMethods
{
    [Serializable]
    public class TouchControlMethod : CurvedUIControlMethod
    {
        #region PUBLIC

        public override void Initialize(bool isPlayMode) { }

        public override ControlArgs Process(Hand usedHand, Camera mainEventCamera)
            => new()
            {
                Ray = GetEventRay(usedHand, mainEventCamera),
                ButtonState = IsTouching,
            };

        public override Ray GetEventRay(Hand usedHand, Camera eventCam = null)
        {
            if (eventCam == null)
            {
                Debug.LogError("CURVEDUI: No camera provided for touch ray cast. Returning empty ray.");
                return new Ray();
            }

            // Если касания нет, возвращаем луч из центра или нулевой (зависит от логики игры, 
            // но обычно для UI важно только когда касание есть).
            // Здесь мы берем последнюю позицию, чтобы не "стрелять" в 0,0 координат.
            return eventCam.ScreenPointToRay(TouchPosition);
        }

        public override Transform GetPointerTransform(Hand usedHand)
        {
            Debug.LogError($"CurvedUI: {nameof(TouchControlMethod)} does not have a pointer transform.");
            return null;
        }

        #endregion

        #region SETTERS AND GETTERS

        /// <summary>
        /// Текущая позиция первого касания на экране.
        /// Возвращает Vector2.zero, если касаний нет.
        /// </summary>
        public static Vector2 TouchPosition
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && CURVEDUI_NEW_INPUT
                // Новая система ввода
                return Touchscreen.current?.primaryTouch.position.ReadValue() ?? Vector2.zero;
#else
                // Старая система ввода
                if (Input.touchCount > 0)
                {
                    return Input.GetTouch(0).position;
                }
                // Если касаний нет, можно возвращать позицию мыши для дебага в редакторе, 
                // если это требуется, или zero. Оставим zero для чистого Touch.
                return Vector2.zero; 
#endif
            }
        }

        /// <summary>
        /// Нажат ли палец на экран? (Эквивалент нажатия кнопки мыши)
        /// </summary>
        public static bool IsTouching
        {
            get
            {
#if ENABLE_INPUT_SYSTEM && CURVEDUI_NEW_INPUT
                // Новая система ввода: проверяем нажатие основного тача
                return Touchscreen.current?.primaryTouch.press.isPressed ?? false;
#else
                // Старая система ввода: проверяем наличие хотя бы одного касания
                return Input.touchCount > 0 && 
                       (Input.GetTouch(0).phase == TouchPhase.Began || 
                        Input.GetTouch(0).phase == TouchPhase.Moved || 
                        Input.GetTouch(0).phase == TouchPhase.Stationary);
#endif
            }
        }

        #endregion
    }
}
