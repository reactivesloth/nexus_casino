using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CC
{
    /// <summary>
    /// Генерирует набор кнопок-точек цвета. Без LINQ, с очисткой подписок и созданных объектов.
    /// </summary>
    public sealed class Option_Tint_Buttons : MonoBehaviour, ICustomizerUI
    {
        public CC_Property property;
        public List<Color> tints = new List<Color>();
        public GameObject buttonPrefab;
        public UnityEvent customEvent;

        private CharacterCustomization _customizer;
        private readonly List<Button> _buttons = new List<Button>(16);
        private readonly List<GameObject> _spawned = new List<GameObject>(16);

        private void Start()
        {
            // Кнопка "сброс" (прозрачный)
            var defaultBtn = GetComponentInChildren<Button>(true);
            if (defaultBtn != null)
            {
                defaultBtn.onClick.AddListener(() =>
                {
                    setProperty(new Color(0, 0, 0, 0));
                    customEvent?.Invoke();
                });
                _buttons.Add(defaultBtn);
            }

            if (buttonPrefab == null) return;

            // Генерация кнопок под палитру
            for (int i = 0; i < tints.Count; i++)
            {
                var btnGO = Instantiate(buttonPrefab, transform);
                if (btnGO == null) continue;

                _spawned.Add(btnGO);

                var btn = btnGO.GetComponentInChildren<Button>(true);
                var img = btnGO.GetComponentInChildren<Image>(true);

                if (img != null) img.color = tints[i];

                if (btn != null)
                {
                    int idx = i;
                    btn.onClick.AddListener(() =>
                    {
                        setProperty(tints[idx]);
                        customEvent?.Invoke();
                    });
                    _buttons.Add(btn);
                }

                // Не навешиваю лишние Image на корень — это плодит компоненты без необходимости.
            }

            var rt = GetComponent<RectTransform>();
            if (rt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void setProperty(Color color)
        {
            property.colorValue = color;
            if (_customizer != null)
                _customizer.setColorProperty(property, true);
        }

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util parentUI)
        {
            _customizer = customizerScript;
        }

        public void RefreshUIElement() { }

        private void OnDestroy()
        {
            for (int i = 0; i < _buttons.Count; i++)
                if (_buttons[i] != null) _buttons[i].onClick.RemoveAllListeners();

            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
        }
    }
}
