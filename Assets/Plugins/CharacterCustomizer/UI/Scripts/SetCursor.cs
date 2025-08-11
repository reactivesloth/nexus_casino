using UnityEngine;
using UnityEngine.EventSystems;

namespace CC
{
    public sealed class SetCursor : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Texture2D cursorTexture;

        public void OnPointerEnter(PointerEventData eventData)
        {
            var inst = Cursor_Manager.instance;
            if (inst != null && cursorTexture != null)
                inst.setCursor(cursorTexture);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var inst = Cursor_Manager.instance;
            if (inst != null)
                inst.setDefaultCursor();
        }
    }
}