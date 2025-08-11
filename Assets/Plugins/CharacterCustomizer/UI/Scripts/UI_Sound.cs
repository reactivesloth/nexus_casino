using UnityEngine;
using UnityEngine.UI;

namespace CC
{
    /// <summary>
    /// Вешается на корень с UI-кнопками. Автоматически подписывает все Button в дочерних объектах,
    /// играет звук по индексу через CC_UI_Manager. Снимает подписки при уничтожении.
    /// </summary>
    public sealed class UI_Sound : MonoBehaviour
    {
        [Tooltip("Индекс клипа в CC_UI_Manager.UISounds")]
        public int sound;

        private Button[] _buttons;

        private void Awake()
        {
            _buttons = GetComponentsInChildren<Button>(true);
            if (_buttons == null || _buttons.Length == 0) return;

            // Локальная копия индекса, без замыканий на поля
            int snd = sound;

            for (int i = 0; i < _buttons.Length; i++)
            {
                var btn = _buttons[i];
                if (btn == null) continue;
                btn.onClick.AddListener(() =>
                {
                    var inst = CC_UI_Manager.instance;
                    if (inst != null) inst.playUIAudio(snd);
                });
            }
        }

        private void OnDestroy()
        {
            if (_buttons == null) return;
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i] != null)
                    _buttons[i].onClick.RemoveAllListeners();
            }
        }
    }
}