using UnityEngine;
using UnityEngine.UI;

namespace Code.Utility
{
    public static class RectTransformUtil
    {
        // Меняет pivot, сохраняя визуальную позицию в родителе (anchoredPosition компенсируется)
        public static void SetPivotKeepingPosition(RectTransform rt, Vector2 newPivot)
        {
            if (!rt) return;

            Canvas.ForceUpdateCanvases(); // актуализируем размеры до вычислений

            var oldPivot = rt.pivot;
            var rect = rt.rect; // актуальные width/height
            var deltaPivot = newPivot - oldPivot;

            // Считаем компенсацию в локальных координатах родителя
            var delta = new Vector2(deltaPivot.x * rect.width, deltaPivot.y * rect.height);

            // Меняем pivot и компенсируем сдвиг
            rt.pivot = newPivot;
            rt.anchoredPosition += delta;

            Canvas.ForceUpdateCanvases(); // закрепляем изменения
        }

        // Безопасный обёртчик для ScrollRect: сохраняет прогресс/скорость прокрутки
        public static void DoWithoutAffectingScroll(ScrollRect sr, System.Action body)
        {
            if (!sr) { body?.Invoke(); return; }

            // Сохраняем состояние прокрутки
            var norm = sr.normalizedPosition;
            var vel  = sr.velocity;

            body?.Invoke();

            // Восстанавливаем (обычно не понадобится, но полезно при инерции)
            sr.normalizedPosition = norm;
            sr.velocity = vel;
        }
    }
}