/* Written by Kaz Crowe */
/* UltimateButtonReadme.cs */
using UnityEngine;
using System.Collections.Generic;

public class UltimateButtonReadme : ScriptableObject
{
	public Texture2D icon;
	public Texture2D settings;
	public Texture2D scriptReference;

	// GIZMO COLORS //
	[HideInInspector]
	public Color colorDefault = new Color( 1.0f, 1.0f, 1.0f, 0.5f );
	[HideInInspector]
	public Color colorValueChanged = new Color( 0.0f, 0.376f, 1.0f, 1.0f );

	// VERSION CHANGES //
	public static int ImportantChange = 4;
	public class VersionHistory
	{
		public string versionNumber = "";
		public string[] changes;
	}
	public VersionHistory[] versionHistory = new VersionHistory[]
	{
		// VERSION 4.0.0 //
		new VersionHistory ()
		{
			versionNumber = "4.0.0",
			changes = new string[]
			{
				// GENERAL CHANGES //
				"Reordered and simplified the inspector to improve workflow and ease of use",
				"Improved editor performance",
				"Improved existing scene gizmos and added new useful ones",
				"Serialized and made private all internal fields to clean up the script and reference",
				"Added internal documentation and range values for fields inside the main Ultimate Button script",
				"Removed the Scaling Axis option and improved default screen positioning calculations",
				"Added additional checks within the editor script to disable Raycast Target on child images",
				"Removed the UltimateButtonScreenSizeUpdater script",
				"Added code to replace outdated EventSystem components for the new Input System if it is used",
				// NEW FEATURES //
				"Added the ability to reposition and resize the button at runtime using the new Player Position Override option. See the New Functions section below for the list of associated functions",
				"Added the ability to click and drag the button graphic in the editor to set it's position on the screen",
				"Added the ability to open sections on the Ultimate Button inspector when dragging an object in the inspector window",
				"Added two new options to the Anchor property: Relative To Transform and Orbit Transform",
				"Added new functionality for the input outside the range of the button to be transmitted to any images below it",
				"Along with the above change, added a new option that allows input on the button to be transmitted to any graphics that might be below it even when the input is within range",
				"Added a new setting to determine how the input is handled on the button, whether using the EventSystem or directly using touch input on the screen (new Input System completely supported)",
				"Added a lot of new multi-object editing features to help position and edit multiple buttons at the same time",
				// DEPRECIATED FUNCTIONS //
				"Removed the GetTapCount functions. The new tap count callbacks (OnTapAchieved or OnTapReleased) should now be used instead",
				"Depreciated the GetUltimateButton static function. The new ReturnComponent function should now be used instead",
				"Depreciated both the public and static DisableButton functions. The new Disable function should be used instead",
				"Depreciated both the public and static EnableButton functions. The new Enable function should be used instead",
				"Removed the public function: UpdateParentCanvas",
				// NEW FUNCTIONS //
				"Added 5 new functions for custom player defined positioning: StartOverridePositioning, StopOverridePositioning, SetOverridePosition, SetOverrideSize, and ResetOverridePositioning",
				"Added a new override to the UpdatePositioning function that allows the user to specify new position values to apply to the button",
				// FIXES //
				"Fixed a debug that would sometimes be sent to the console while dragging an Ultimate Button prefab into a scene",
				"Fixed warnings in Unity 2023+ dealing with obsolete FindObjectOfType references",
			},
		},
		// VERSION 3.1.0 //
		new VersionHistory ()
		{
			versionNumber = "3.1.0",
			changes = new string[]
			{
				// GENERAL CHANGES //
				"Improved calculations for making the Ultimate Button completely compatible with the new Unity Input System if the user is using it in their project",
				// REMOVED //
				"Removed the option for exclusively using Unity's touch input instead of the EventSystem. The option was not working consistently across all versions of Unity",
				"Removed all of the old depreciated code that was left in on version 3.0.0",
			},
		},
		// VERSION 3.0.1 //
		new VersionHistory ()
		{
			versionNumber = "3.0.1",
			changes = new string[]
			{
				// GENERAL CHANGES //
				"Updated the folder structure to place all of the Tank & Healer Studio assets under one root folder",
			},
		},
		// VERSION 3.0.0 // Important Change = 4
		new VersionHistory ()
		{
			versionNumber = "3.0.0",
			changes = new string[]
			{
				// GENERAL CHANGES //
				"Removed the Touch Size option and replaced it with a slider for Activation Range",
				"Improved calculations for initiating the touch on the button",
				"Removed the use of the Button Size Folder",
				"Added new option for exclusively using Unity's touch input instead of the EventSystem. This has been requested to combat some potential issues when using different versions of Unity",
				"Updated the README file to be able to stay on the same page, even after compiling scripts",
				"Removed the option for playing an animation when interacting with the button and replaced it with an option for scaling the button when being interacted with",
				"Simplified calculations within the Ultimate Button script to improve performance",
				"Overall cleanup of the Ultimate Button script",
				"Added new versions of the existing button textures and removed the old versions",
				"Updated button prefabs to use the new textures",
				"Improved positioning calculations to support various canvas setups",
				// EDITOR IMPROVEMENTS //
				"Reorganized the inspector to improve workflow",
				"Added new scene gizmos to help understand what an option is affecting, such as the on the Activation Range variable so that the user can see a visual representation of the range of the button",
				"Condensed the options for displaying transitions when interacting with the button into one option: Input Transition",
				"Added a Base Color option into the inspector which can be used to change the color of the button image. This variable is only available from the inspector so as not to clutter up the main Ultimate Button script",
				"Overall improved the Ultimate Button inspector and workflow",
				"Added an option for developers that want to expand on the Ultimate Button code. The option is located in the Settings of the README window. To access it, select the README file and click the gear icon in the top right. There will be an option at the bottom for Enable Development Mode. Now the Ultimate Button inspector will have a new section: Development Inspector",
				// NEW FEATUERS //
				"Added new positioning option for orbiting the button around another Rect Transform",
				"Added in new functionality to support icons and cooldown images. This will allow the button to be used for skills and items",
			},
		},
		// VERSION 2.6.1
		new VersionHistory ()
		{
			versionNumber = "2.6.1",
			changes = new string[]
			{
				"Updated the Truck example scene to remove a warning about the JointMotor2D in Unity 2018+. Also modified the order value for the truck to look correct",
				"Updated script reference section of the Ultimate Button to not include the if() conditional to copy and paste. This simplifies the reference and makes it easier to use",
			},
		},
		// VERSION 2.6.0 // Important Change = 3
		new VersionHistory ()
		{
			versionNumber = "2.6.0",
			changes = new string[]
			{
				"Improved the Ultimate Button textures",
				"Removed AnimBool functionality from the inspector to avoid errors with Unity 2019+",
				"Added a new positioning option for Relative to Transform",
				"Added new script: UltimateButtonReadme.cs",
				"Added new script: UltimateButtonReadmeEditor.cs",
				"Added new file at the Ultimate Button root folder: README. This file has all the documentation and how to information",
				"Removed the UltimateButtonWindow.cs file. All of that information is now located in the README file",
				"Removed the old README text file. All of that information is now located in the README file",
				"Removed several useless public functions: UpdateBaseColor, UpdateHighlightColor, and UpdateTensionColor. Even without these functions you can still modify the corresponding variables easily to get the same functionality",
				"Added several new public functions for reference: GetButtonDown, GetButton, GetButtonUp, GetTapCount",
			},
		},
	};

	[HideInInspector]
	public List<int> pageHistory = new List<int>();
	[HideInInspector]
	public Vector2 scrollValue = new Vector2();
}