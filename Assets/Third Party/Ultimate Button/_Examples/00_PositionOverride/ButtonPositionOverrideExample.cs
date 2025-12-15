using UnityEngine;
using UnityEngine.UI;

namespace TankAndHealerStudioAssets
{
	public class ButtonPositionOverrideExample : MonoBehaviour
	{
		public UltimateButton[] buttons;
		public Button startButton, stopButton;
		public Slider sizeSlider;

		public void StartOverride ()
		{
			// Since the reposition has started, set the start button interactable to false, and the stop button to true.
			startButton.interactable = false;
			stopButton.interactable = true;

			// Loop through all the joysticks and start override positioning.
			for( int i = 0; i < buttons.Length; i++ )
				buttons[ i ].StartOverridePositioning();
		}

		public void StopOverride ()
		{
			// Since the override is stopped, flip the interactable state of the buttons.
			stopButton.interactable = false;
			startButton.interactable = true;

			// Loop through all the joysticks and stop the override.
			for( int i = 0; i < buttons.Length; i++ )
				buttons[ i ].StopOverridePositioning();
		}

		public void OverrideSize ( float sliderValue )
		{
			// Loop through all the joysticks and override the joystick size.
			for( int i = 0; i < buttons.Length; i++ )
				buttons[ i ].SetOverrideSize( sliderValue );
		}

		public void ResetOverride ()
		{
			// Loop through all the joysticks and reset the override positioning.
			for( int i = 0; i < buttons.Length; i++ )
				buttons[ i ].ResetOverridePositioning();

			// Reset the slider value.
			sizeSlider.SetValueWithoutNotify( 2.5f );
		}
	}
}