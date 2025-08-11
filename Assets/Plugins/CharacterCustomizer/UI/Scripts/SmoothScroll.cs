using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace CC
{
    /// <summary>
    /// ScrollRect с плавной прокруткой. Без лишних аллокаций, корректно останавливает корутины.
    /// </summary>
    public class SmoothScroll : ScrollRect
    {
        public bool SmoothScrolling { get; set; } = true;
        public float SmoothScrollTime { get; set; } = 0.2f;

        private Coroutine _smoothScrollCoroutine;

        public override void OnScroll(PointerEventData data)
        {
            if (!IsActive()) return;

            if (SmoothScrolling)
            {
                if (_smoothScrollCoroutine != null)
                    StopCoroutine(_smoothScrollCoroutine);

                Vector2 before = normalizedPosition;
                base.OnScroll(data);
                Vector2 after = normalizedPosition;
                normalizedPosition = before;

                _smoothScrollCoroutine = StartCoroutine(SmoothScrollToPosition(after));
            }
            else
            {
                base.OnScroll(data);
            }
        }

        public void SetScrollTarget(Vector2 targetPosition)
        {
            if (SmoothScrolling)
            {
                if (_smoothScrollCoroutine != null)
                    StopCoroutine(_smoothScrollCoroutine);

                // Нормализуем целевую позицию через сам ScrollRect (учёт ограничений)
                Vector2 before = normalizedPosition;
                normalizedPosition = targetPosition;
                Vector2 after = normalizedPosition;
                normalizedPosition = before;

                _smoothScrollCoroutine = StartCoroutine(SmoothScrollToPosition(after));
            }
            else
            {
                normalizedPosition = targetPosition;
            }
        }

        public void resetScroll()
        {
            SetScrollTarget(new Vector2(0f, 1f));
        }

        public void ScrollToContent(RectTransform targetContent)
        {
            if (targetContent == null || content == null || viewport == null || !targetContent.IsChildOf(content))
            {
                Debug.LogWarning("SmoothScroll: target content is invalid or not a child of content.");
                return;
            }

            // Позиция цели во внутренних координатах контента
            Vector2 targetLocalPos = content.InverseTransformPoint(targetContent.position);
            Vector2 contentSize = content.rect.size;
            Vector2 viewportSize = viewport.rect.size;

            // Учёт верхнего левого выравнивания ScrollRect (y инвертирован)
            float nx = (contentSize.x > viewportSize.x)
                ? Mathf.Clamp01((targetLocalPos.x - viewportSize.x * 0.5f) / (contentSize.x - viewportSize.x))
                : 0f;

            float ny = (contentSize.y > viewportSize.y)
                ? Mathf.Clamp01((targetLocalPos.y - viewportSize.y * 0.5f) / (contentSize.y - viewportSize.y))
                : 1f;

            SetScrollTarget(new Vector2(nx, ny));
        }

        private IEnumerator SmoothScrollToPosition(Vector2 targetPosition)
        {
            float elapsed = 0f;
            Vector2 start = normalizedPosition;
            float dur = Mathf.Max(0.0001f, SmoothScrollTime);

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                normalizedPosition = Vector2.Lerp(start, targetPosition, elapsed / dur);
                yield return null;
            }

            normalizedPosition = targetPosition;
            _smoothScrollCoroutine = null;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (_smoothScrollCoroutine != null)
            {
                StopCoroutine(_smoothScrollCoroutine);
                _smoothScrollCoroutine = null;
            }
        }

        protected override void OnDestroy()
        {
            if (_smoothScrollCoroutine != null)
            {
                StopCoroutine(_smoothScrollCoroutine);
                _smoothScrollCoroutine = null;
            }
            base.OnDestroy();
        }
    }
}
