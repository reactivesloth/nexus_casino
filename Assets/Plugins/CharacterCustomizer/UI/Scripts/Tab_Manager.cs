using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CC
{
    public sealed class Tab_Manager : MonoBehaviour
    {
        [Header("Button Active Colors")]
        public ColorBlock TabColorActive;

        [Header("Button Inactive Colors")]
        public ColorBlock TabColorInactive;

        public GameObject TabParent;
        public SmoothScroll targetScroll;

        private readonly List<GameObject> _tabs = new List<GameObject>(8);
        private readonly List<GameObject> _tabMenus = new List<GameObject>(8);
        private SmoothScroll _scrollRect;
        private RectTransform _tabParentRT;

        private void Start()
        {
            // Собираем вкладки (кнопки)
            int childCount = transform.childCount;
            for (int i = 0; i < childCount; i++)
            {
                var tr = transform.GetChild(i);
                if (tr == null) continue;

                var tabGO = tr.gameObject;
                _tabs.Add(tabGO);

                var btn = tabGO.GetComponentInChildren<Button>(true);
                if (btn == null) continue;

                int idx = i; // фиксируем индекс
                btn.onClick.AddListener(() => switchTab(idx));
            }

            // Собираем панели содержимого
            if (TabParent != null)
            {
                _tabParentRT = TabParent.GetComponent<RectTransform>();
                var parentTr = TabParent.transform;
                int cnt = parentTr.childCount;
                for (int i = 0; i < cnt; i++)
                {
                    var child = parentTr.GetChild(i);
                    if (child != null) _tabMenus.Add(child.gameObject);
                }
            }

            _scrollRect = GetComponentInParent<SmoothScroll>();
            switchTab(0);

            if (_scrollRect != null) _scrollRect.resetScroll();
        }

        public void switchTab(int tabIndex)
        {
            if (_tabs.Count == 0) return;
            if (tabIndex < 0 || tabIndex >= _tabs.Count) tabIndex = 0;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tabGO = _tabs[i];
                if (tabGO == null) continue;

                var btn = tabGO.GetComponentInChildren<Button>(true);
                if (btn != null) btn.colors = (tabIndex == i) ? TabColorActive : TabColorInactive;

                if (i < _tabMenus.Count && _tabMenus[i] != null)
                    _tabMenus[i].SetActive(tabIndex == i);
            }

            var rt = _tabs[tabIndex] != null ? _tabs[tabIndex].GetComponent<RectTransform>() : null;
            if (_scrollRect != null && rt != null) _scrollRect.ScrollToContent(rt);

            if (_tabParentRT != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_tabParentRT);

            if (targetScroll != null)
                targetScroll.resetScroll();
        }

        private void OnDestroy()
        {
            // Чистим подписки у кнопок, чтобы не висели делегаты
            for (int i = 0; i < _tabs.Count; i++)
            {
                var tabGO = _tabs[i];
                if (tabGO == null) continue;

                var btn = tabGO.GetComponentInChildren<Button>(true);
                if (btn != null) btn.onClick.RemoveAllListeners();
            }
        }
    }
}
