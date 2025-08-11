using UnityEngine;

namespace CC
{
    public class Cursor_Manager : MonoBehaviour
    {
        public static Cursor_Manager instance;

        public Texture2D cursorTexture;
        private Vector2 hotSpot = Vector2.zero;

        private void Awake()
        {
            if (instance == null) instance = this;
            else { Destroy(gameObject); return; }

            if (cursorTexture != null)
            {
                hotSpot = new Vector2(cursorTexture.width * 0.5f, cursorTexture.height * 0.5f);
                setDefaultCursor();
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void setCursor(Texture2D texture)
        {
            if (texture == null) return;
            Cursor.SetCursor(texture, hotSpot, CursorMode.Auto);
        }

        public void setDefaultCursor()
        {
            if (cursorTexture != null) setCursor(cursorTexture);
        }
    }
}