using Code.Utility;
using PurrNet;
using UnityEngine;

namespace Code.Player
{
    public class PlayerEmotionsController : NetworkBehaviour {
        
        [System.Serializable]
        public class EmotionBase
        {
            public string key;
            public Sprite emotionIcon;
            [HideInInspector] public UltimateRadialButtonInfo radialButtonInfo;
        }
        
        
        public EmotionBase[] Emotions;
        EmotionBase currentEmotion;
        private PlayerMovementController playerMovementController;
        private PlayerInput input;
        private bool isMenuOpen;
        
        private void Awake()
        {
            playerMovementController = gameObject.GetComponent<PlayerMovementController>();
            input = PlayerInput.Instance;
        }

        private void Update()
        {
            if (!isOwner || !playerMovementController.CanMove)
            {
                return;
            }

            if (input.IsEmotionsControllerButton)
            {
                if (!PlayerInput.Instance.IsRadialMenuOpen)
                    ShowRadialMenu();
                else
                    CloseRadialMenu();
            }

            if (isMenuOpen)
            {
                CursorManager.Instance.SetForceShowCursor(true);
            }
        }

        private void ShowRadialMenu()
        {
            input.ShowRadialMenu(true);
            CursorManager.Instance.SetForceShowCursor(true);
            input.RadialMenu.ClearMenu();
            isMenuOpen = true;
            
            for( int i = 0; i < Emotions.Length; i++ )
            {
                // Assign the information inside the WeaponBase class to the radialButtonInfo to supply to the radial menu.
                Emotions[ i ].radialButtonInfo.key = Emotions[ i ].key;
                Emotions[ i ].radialButtonInfo.icon = Emotions[ i ].emotionIcon;

                // Add a radial button to the menu with the current Light Weapon information.
                input.RadialMenu.RegisterButton( PlayAnimation, Emotions[ i ].radialButtonInfo);
            }
        }

        private void CloseRadialMenu()
        {
            input.RadialMenu.ClearMenu();
            CursorManager.Instance.SetForceShowCursor(false);
            CursorManager.Instance.HideCursor();
            isMenuOpen = false;
            input.ShowRadialMenu(false);
        }

        private void PlayAnimation(string key)
        {
            playerMovementController.PlayEmotionAnimation(int.Parse(key));
            CloseRadialMenu();
        }
    }
}
