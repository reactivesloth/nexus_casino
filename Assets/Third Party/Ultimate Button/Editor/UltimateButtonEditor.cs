/* Written by Kaz Crowe */
/* UltimateButtonEditor.cs */
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEditorInternal;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TankAndHealerStudioAssets;
using static UnityEngine.GraphicsBuffer;

[CanEditMultipleObjects]
[CustomEditor( typeof( UltimateButton ) )]
public class UltimateButtonEditor : Editor
{
	UltimateButton targ;
	bool isInProjectWindow = false;
	Canvas parentCanvas;

	// -----< BUTTON SETTINGS >----- //
	SerializedProperty buttonBase;
	Sprite buttonBaseSprite;
	Color baseColor;
	// Button Positioning //
	SerializedProperty positioning, anchor;
	SerializedProperty relativeTransform, relativeSpaceMod;
	SerializedProperty buttonSize, activationRange;
	SerializedProperty positionHorizontal, positionVertical;
	SerializedProperty orbitDistance, centerAngle;
	// Input Settings //
	SerializedProperty inputHandling, boundary, trackInput, transmitEventData, tapDecayRate;
	SerializedProperty overridePositioning, applyConstraintDuringOverride, saveAsPlayerPrefs;

	// -----< OPTIONAL SETTINGS >----- //
	// Input Transition //
	SerializedProperty inputTransition, useFade, useScale;
	SerializedProperty useTension, tensionColorDefault, tensionColorActive;
	SerializedProperty tensionAccent;
	Sprite tensionAccentSprite;
	SerializedProperty transitionUntouchedDuration, transitionTouchedDuration;
	SerializedProperty fadeUntouched, fadeTouched, scaleTouched;
	// Highlight //
	SerializedProperty useHighlight, buttonHighlight;
	Color highlightColor = Color.white;
	Sprite buttonHighlightSprite;
	// Icon //
	bool useIcon = false, useIconMask = false;
	SerializedProperty buttonIcon;
	float iconScale = 1.0f;
	Image buttonIconMask;
	Sprite iconSprite, iconMaskSprite;
	Color iconColor = Color.white;
	// Cooldown //
	SerializedProperty useCooldown, cooldownImage, useCooldownText;
	SerializedProperty displayDecimalCooldown, cooldownTextScaleCurve, cooldownText;
	Sprite cooldownSprite;
	float cooldownImageScale = 1.0f, fillAmount = 0.0f;
	float cooldownTestValue = 0.0f, cooldownTestValueMax = 5.0f;
	bool simulateCooldown = false;
	float timeBetweenLastFrame = 0.0f;
	Image.FillMethod fillMethod = Image.FillMethod.Radial360;
	Color cooldownColor = new Color( 0.0f, 0.0f, 0.0f, 0.5f ), textColor = Color.white, textOutlineColor = Color.black;
	float textAnchorMod = 0.5f;
	Font textFont;
	bool textOutline = false;
	// Reorder Child Hierarchy //
	List<RectTransform> childTransforms = new List<RectTransform>();
	ReorderableList childObjects;

	// -----< SCRIPT REFERENCE >----- //
	SerializedProperty buttonName;
	class ExampleCode
	{
		public string optionName = "";
		public string optionDescription = "";
		public string basicCode = "";
	}
	ExampleCode[] exampleCodes = new ExampleCode[]
	{
		new ExampleCode() { optionName = "GetButtonDown ( string buttonName )", optionDescription = "Returns true on the frame that the button was pressed down.", basicCode = "UltimateButton.GetButtonDown( \"{0}\" )" },
		new ExampleCode() { optionName = "GetButtonUp ( string buttonName )", optionDescription = "Returns true on the frame that the button was released.", basicCode = "UltimateButton.GetButtonUp( \"{0}\" )" },
		new ExampleCode() { optionName = "GetButton ( string buttonName )", optionDescription = "Returns the current state of the buttons state. True for being pressed down and false for no input.", basicCode = "UltimateButton.GetButton( \"{0}\" )" },
		new ExampleCode() { optionName = "GetTapCount ( string buttonName )", optionDescription = "Returns true when the user has achieved the tap count.", basicCode = "UltimateButton.GetTapCount( \"{0}\" )" },
		new ExampleCode() { optionName = "GetUltimateButton ( string buttonName )", optionDescription = "Returns the Ultimate Button component that has been registered with the targeted button name.", basicCode = "UltimateButton jumpButton = UltimateButton.GetUltimateButton( \"{0}\" );" },
		new ExampleCode() { optionName = "DisableButton ( string buttonName )", optionDescription = "Disables the Ultimate Button.", basicCode = "UltimateButton.DisableButton( \"{0}\" );" },
		new ExampleCode() { optionName = "EnableButton ( string buttonName )", optionDescription = "Enables the Ultimate Button.", basicCode = "UltimateButton.EnableButton( \"{0}\" );" },
	};
	List<string> exampleCodeOptions = new List<string>();
	int exampleCodeIndex = 0;

	// -----< BUTTON EVENTS >----- //
	SerializedProperty onButtonDown;
	SerializedProperty onButtonUp;
	SerializedProperty tapCountEvent;

	// -----< DEVELOPMENT MODE >----- //
	public bool showDefaultInspector = false;

	// -----< SCENE GUI >----- //
	bool DisplayOrbitRadius = false;
	bool DisplayCenterAngle = false;
	bool DisplayCooldownTextAnchor = false;
	static bool isDirty = false;
	Vector2 currentButtonSize = Vector2.zero;

	// GIZMO COLORS //
	Color colorDefault = Color.black;
	Color colorValueChanged = Color.black;

	// -----< EDITOR STYLES >----- //
	GUIStyle handlesCenteredText = new GUIStyle();
	GUIStyle collapsableSectionStyle = new GUIStyle();

	// Multi Button Options //
	bool allUsingOrbitTransform = true;
	bool allSameRelativeTransform = true;
	float multiCenterAngle = 315.0f;
	float multiAnglePer = 45.0f;
	bool missingButtonBase = false;
	bool missingTensionImage = false, missingHighlightImage = false;
	bool missingIconImage = false;
	bool missingCooldownText = false, missingCooldownImage = false;

	// DRAG AND DROP //
	bool disableDragAndDrop = false;
	bool isDraggingObject = false;
	Vector2 dragAndDropMousePos = Vector2.zero;
	double dragAndDropStartTime = 0.0f;
	double dragAndDropCurrentTime = 0.0f;
	bool DragAndDropHover
	{
		get
		{
			if( disableDragAndDrop )
				return false;

			if( DragAndDrop.objectReferences.Length == 0 )
			{
				dragAndDropStartTime = 0.0f;
				dragAndDropCurrentTime = 0.0f;
				isDraggingObject = false;
				return false;
			}

			isDraggingObject = true;

			var rect = GUILayoutUtility.GetLastRect();
			if( Event.current.type == EventType.Repaint && rect.Contains( Event.current.mousePosition ) )
			{
				if( dragAndDropStartTime == 0.0f )
				{
					dragAndDropStartTime = EditorApplication.timeSinceStartup;
					dragAndDropCurrentTime = 0.0f;
				}

				if( dragAndDropMousePos == Event.current.mousePosition )
					dragAndDropCurrentTime = EditorApplication.timeSinceStartup - dragAndDropStartTime;
				else
				{
					dragAndDropStartTime = EditorApplication.timeSinceStartup;
					dragAndDropCurrentTime = 0.0f;
				}

				if( dragAndDropCurrentTime >= 0.5f )
				{
					dragAndDropStartTime = 0.0f;
					dragAndDropCurrentTime = 0.0f;
					return true;
				}

				dragAndDropMousePos = Event.current.mousePosition;
			}

			return false;
		}
	}

	void OnEnable ()
	{
		StoreReferences();
		Undo.undoRedoPerformed += StoreReferences;

		if( targ != null && !isInProjectWindow )
		{
			if( !targ.gameObject.GetComponent<Image>() )
				Undo.AddComponent<Image>( targ.gameObject );

			Undo.RecordObject( targ.gameObject.GetComponent<Image>(), "Null Image Alpha" );
			targ.gameObject.GetComponent<Image>().color = new Color( 1.0f, 1.0f, 1.0f, 0.0f );
		}
		
		if( EditorPrefs.HasKey( "UB_ColorHexSetup" ) )
		{
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "UB_ColorDefaultHex" ), out colorDefault );
			ColorUtility.TryParseHtmlString( EditorPrefs.GetString( "UB_ColorValueChangedHex" ), out colorValueChanged );
		}

		UltimateButton.CustomEditorPositioning = false;

		// DISABLE RAYCAST TARGET //
		if( targ.ButtonBase != null && targ.ButtonBase.gameObject != targ.gameObject && targ.ButtonBase.GetComponent<Image>().raycastTarget )
		{
			Undo.RecordObject( targ.ButtonBase.GetComponent<Image>(), "Disable Unnecessary Button Raycast" );
			targ.ButtonBase.GetComponent<Image>().raycastTarget = false;
		}

		if( targ.ButtonHighlight != null && targ.ButtonHighlight.gameObject != targ.gameObject && targ.ButtonHighlight.raycastTarget )
		{
			Undo.RecordObject( targ.ButtonHighlight, "Disable Unnecessary Button Raycast" );
			targ.ButtonHighlight.raycastTarget = false;
		}

		if( targ.TensionAccent != null && targ.TensionAccent.gameObject != targ.gameObject && targ.TensionAccent.raycastTarget )
		{
			Undo.RecordObject( targ.TensionAccent, "Disable Unnecessary Button Raycast" );
			targ.TensionAccent.raycastTarget = false;
		}

		if( targ.CooldownImage != null && targ.CooldownImage.gameObject != targ.gameObject && targ.CooldownImage.raycastTarget )
		{
			Undo.RecordObject( targ.CooldownImage, "Disable Unnecessary Button Raycast" );
			targ.CooldownImage.raycastTarget = false;
		}

		// IMAGE ANCHORS //
		if( inputTransition.boolValue && useTension.boolValue && targ.TensionAccent != null )
		{
			if( targ.TensionAccent.rectTransform.anchoredPosition != Vector2.zero )
			{
				Undo.RecordObject( targ.TensionAccent, "Update Button Image Positions" );
				targ.TensionAccent.rectTransform.offsetMax = Vector2.zero;
				targ.TensionAccent.rectTransform.offsetMin = Vector2.zero;
			}

			if( targ.TensionAccent.raycastTarget )
			{
				Undo.RecordObject( targ.TensionAccent, "Disable Unnecessary Button Raycast" );
				targ.TensionAccent.raycastTarget = false;
			}
		}

		if( useHighlight.boolValue && targ.ButtonHighlight != null )
		{
			if( targ.ButtonHighlight.rectTransform.anchoredPosition != Vector2.zero )
			{
				Undo.RecordObject( targ.ButtonHighlight, "Update Button Image Positions" );
				targ.ButtonHighlight.rectTransform.offsetMax = Vector2.zero;
				targ.ButtonHighlight.rectTransform.offsetMin = Vector2.zero;
			}

			if( targ.ButtonHighlight.raycastTarget )
			{
				Undo.RecordObject( targ.ButtonHighlight, "Disable Unnecessary Button Raycast" );
				targ.ButtonHighlight.raycastTarget = false;
			}
		}

		if( useCooldown.boolValue && targ.CooldownImage != null )
		{
			if( targ.CooldownImage.rectTransform.anchoredPosition != Vector2.zero )
			{
				Undo.RecordObject( targ.CooldownImage, "Update Button Image Positions" );
				targ.CooldownImage.rectTransform.offsetMax = Vector2.zero;
				targ.CooldownImage.rectTransform.offsetMin = Vector2.zero;
			}

			if( targ.CooldownImage.raycastTarget )
			{
				Undo.RecordObject( targ.CooldownImage, "Disable Unnecessary Button Raycast" );
				targ.CooldownImage.raycastTarget = false;
			}
		}

		if( useIcon && targ.ButtonIcon != null )
		{
			if( targ.ButtonIcon.rectTransform.anchoredPosition != Vector2.zero )
			{
				Undo.RecordObject( targ.ButtonIcon, "Update Button Image Positions" );
				targ.ButtonIcon.rectTransform.offsetMax = Vector2.zero;
				targ.ButtonIcon.rectTransform.offsetMin = Vector2.zero;
			}

			if( targ.ButtonIcon.raycastTarget )
			{
				Undo.RecordObject( targ.ButtonIcon, "Disable Unnecessary Button Raycast" );
				targ.ButtonIcon.raycastTarget = false;
			}
		}
	}

	void OnDisable ()
	{
		Undo.undoRedoPerformed -= StoreReferences;
		UltimateButton.CustomEditorPositioning = false;
	}

	Canvas GetParentCanvas ()
	{
		if( Selection.activeGameObject == null )
			return null;

		Transform parent = Selection.activeGameObject.transform.parent;

		while( parent != null )
		{
			if( parent.transform.GetComponent<Canvas>() && parent.transform.GetComponent<Canvas>().enabled )
				return parent.transform.GetComponent<Canvas>();

			parent = parent.transform.parent;
		}

		if( parent == null && !AssetDatabase.Contains( Selection.activeGameObject ) )
			RequestCanvas( Selection.activeGameObject );

		return null;
	}

	void CheckPropertyHover ( ref bool hovered )
	{
		if( Event.current.type != EventType.Repaint )
			return;

		hovered = false;
		var rect = GUILayoutUtility.GetLastRect();
		if( rect.Contains( Event.current.mousePosition ) )
			hovered = isDirty = true;
	}

	void PropertyUpdated ( ref bool propertyController )
	{
		propertyController = isDirty = true;
	}

	void StoreReferences ()
	{
		targ = ( UltimateButton )target;

		if( targ == null )
			return;

		isInProjectWindow = AssetDatabase.Contains( targ.gameObject );
		parentCanvas = GetParentCanvas();

		// -------------------< BUTTON SETTINGS >------------------- //
		buttonBase = serializedObject.FindProperty( "buttonBase" );
		if( targ.ButtonBase != null && targ.ButtonBase.sprite != null )
			buttonBaseSprite = targ.ButtonBase.sprite;
		baseColor = targ.ButtonBase == null ? Color.white : targ.ButtonBase.color;

		// Button Positioning //
		positioning = serializedObject.FindProperty( "positioning" );
		relativeTransform = serializedObject.FindProperty( "relativeTransform" );
		relativeSpaceMod = serializedObject.FindProperty( "relativeSpaceMod" );
		anchor = serializedObject.FindProperty( "anchor" );
		activationRange = serializedObject.FindProperty( "activationRange" );
		buttonSize = serializedObject.FindProperty( "buttonSize" );
		positionHorizontal = serializedObject.FindProperty( "positionHorizontal" );
		positionVertical = serializedObject.FindProperty( "positionVertical" );
		orbitDistance = serializedObject.FindProperty( "orbitDistance" );
		centerAngle = serializedObject.FindProperty( "centerAngle" );
		// Input Settings //
		inputHandling = serializedObject.FindProperty( "inputHandling" );
		boundary = serializedObject.FindProperty( "boundary" );
		trackInput = serializedObject.FindProperty( "trackInput" );
		transmitEventData = serializedObject.FindProperty( "transmitEventData" );
		tapDecayRate = serializedObject.FindProperty( "tapDecayRate" );
		// Override Positioning //
		overridePositioning = serializedObject.FindProperty( "overridePositioning" );
		applyConstraintDuringOverride = serializedObject.FindProperty( "applyConstraintDuringOverride" );
		saveAsPlayerPrefs = serializedObject.FindProperty( "saveAsPlayerPrefs" );

		// -------------------< OPTIONAL SETTINGS >------------------- //
		// Input Transition //
		inputTransition = serializedObject.FindProperty( "inputTransition" );
		transitionUntouchedDuration = serializedObject.FindProperty( "transitionUntouchedDuration" );
		transitionTouchedDuration = serializedObject.FindProperty( "transitionTouchedDuration" );
		useTension = serializedObject.FindProperty( "useTension" );
		tensionColorDefault = serializedObject.FindProperty( "tensionColorDefault" );
		tensionColorActive = serializedObject.FindProperty( "tensionColorActive" );
		tensionAccent = serializedObject.FindProperty( "tensionAccent" );
		if( targ.TensionAccent != null && targ.TensionAccent.sprite != null )
			tensionAccentSprite = targ.TensionAccent.sprite;
		useFade = serializedObject.FindProperty( "useFade" );
		fadeUntouched = serializedObject.FindProperty( "fadeUntouched" );
		fadeTouched = serializedObject.FindProperty( "fadeTouched" );
		useScale = serializedObject.FindProperty( "useScale" );
		scaleTouched = serializedObject.FindProperty( "scaleTouched" );
		// Highlight //
		useHighlight = serializedObject.FindProperty( "useHighlight" );
		buttonHighlight = serializedObject.FindProperty( "buttonHighlight" );
		if( targ.ButtonHighlight != null )
		{
			highlightColor = targ.HighlightColor;
			if( targ.ButtonHighlight.sprite != null )
				buttonHighlightSprite = targ.ButtonHighlight.sprite;
		}
		// Icon //
		buttonIcon = serializedObject.FindProperty( "buttonIcon" );
		if( targ.ButtonIcon != null )
		{
			useIcon = targ.ButtonIcon.gameObject.activeInHierarchy;
			iconSprite = targ.ButtonIcon.sprite;
			iconColor = targ.ButtonIcon.color;
			iconScale = targ.ButtonIcon.rectTransform.localScale.x;
		}
		useIconMask = targ.ButtonIcon != null && targ.ButtonIcon.transform.parent != targ.ButtonBase.transform;
		if( useIconMask )
			buttonIconMask = targ.ButtonIcon.transform.parent.GetComponent<Image>();
		if( useIconMask && buttonIconMask != null && buttonIconMask.sprite != null )
			iconMaskSprite = buttonIconMask.sprite;
		// Cooldown //
		useCooldown = serializedObject.FindProperty( "useCooldown" );
		cooldownImage = serializedObject.FindProperty( "cooldownImage" );
		if( targ.CooldownImage != null )
		{
			fillMethod = targ.CooldownImage.fillMethod;
			cooldownTestValue = targ.CooldownImage.fillAmount * cooldownTestValueMax;
			fillAmount = targ.CooldownImage.fillAmount;
			cooldownImageScale = targ.CooldownImage.rectTransform.localScale.x;

			if( targ.CooldownImage.sprite != null )
				cooldownSprite = targ.CooldownImage.sprite;

			cooldownColor = targ.CooldownImage.color;
		}
		// Cooldown Text //
		useCooldownText = serializedObject.FindProperty( "useCooldownText" );
		displayDecimalCooldown = serializedObject.FindProperty( "displayDecimalCooldown" );
		cooldownTextScaleCurve = serializedObject.FindProperty( "cooldownTextScaleCurve" );
		cooldownText = serializedObject.FindProperty( "cooldownText" );
		if( useCooldownText.boolValue && targ.CooldownText != null )
		{
			textAnchorMod = Mathf.Lerp( -1.0f, 1.0f, targ.CooldownText.rectTransform.anchorMax.y );
			textFont = targ.CooldownText.font;
			textColor = targ.CooldownText.color;
			if( targ.CooldownText.GetComponent<Outline>() )
			{
				textOutline = true;
				textOutlineColor = targ.CooldownText.GetComponent<Outline>().effectColor;
			}
		}
		else
		{
#if UNITY_2022_2_OR_NEWER
			textFont = Resources.GetBuiltinResource<Font>( "LegacyRuntime.ttf" );
#else
			textFont = Resources.GetBuiltinResource<Font>( "Arial.ttf" );
#endif
		}

		// If the user was in the middle of simulating a cooldown test, then reset the simulation.
		if( simulateCooldown )
		{
			simulateCooldown = false;
			cooldownTestValue = cooldownTestValueMax;
			if( targ.CooldownImage != null )
				targ.CooldownImage.enabled = false;

			targ.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
		
			if( targ.CooldownImage != null )
				targ.CooldownImage.enabled = true;
		}

		// ------< SCRIPT REFERENCE >------ //
		buttonName = serializedObject.FindProperty( "buttonName" );
		exampleCodeOptions = new List<string>();
		for( int i = 0; i < exampleCodes.Length; i++ )
			exampleCodeOptions.Add( exampleCodes[ i ].optionName );

		// ------< BUTTON EVENTS >------ //
		onButtonDown = serializedObject.FindProperty( "onButtonDown" );
		onButtonUp = serializedObject.FindProperty( "onButtonUp" );
		tapCountEvent = serializedObject.FindProperty( "tapCountEvent" );
		
		StoreChildTransforms();

		// MULTI-BUTTON //
		multiCenterAngle = centerAngle.floatValue;
		if( targets.Length > 1 )
		{
			float smallestAngle = 360.0f;
			float largestAngle = 0.0f;
			missingCooldownText = false;
			allUsingOrbitTransform = true;
			allSameRelativeTransform = true;

			for( int i = 0; i < targets.Length; i++ )
			{
				SerializedObject btnObj = new SerializedObject( targets[ i ] );
				UltimateButton btn = ( UltimateButton )targets[ i ];

				// Multi-button positioning //
				if( btnObj.FindProperty( "centerAngle" ).floatValue < smallestAngle )
				{
					smallestAngle = btnObj.FindProperty( "centerAngle" ).floatValue;
					i = 0;
				}

				if( btnObj.FindProperty( "centerAngle" ).floatValue > largestAngle )
					largestAngle = btnObj.FindProperty( "centerAngle" ).floatValue;

				if( btnObj.FindProperty( "anchor" ).enumValueIndex != 3 )
					allUsingOrbitTransform = false;

				if( targ.RelativeTransform != btnObj.FindProperty( "relativeTransform" ).objectReferenceValue )
					allSameRelativeTransform = false;

				if( btn.ButtonBase == null )
					missingButtonBase = true;
				else if( btn.ButtonBase.sprite != null )
					buttonBaseSprite = btn.ButtonBase.sprite;

				// Tension //
				if( btnObj.FindProperty( "useTension" ).boolValue )
				{
					if( btn.TensionAccent == null )
						missingTensionImage = true;
					else if( btn.TensionAccent.sprite != null )
						tensionAccentSprite = btn.TensionAccent.sprite;
				}

				// Highlight //
				if( btnObj.FindProperty( "useHighlight" ).boolValue )
				{
					if( btn.ButtonHighlight == null )
						missingHighlightImage = true;
					else if( btn.ButtonHighlight.sprite != null )
						buttonHighlightSprite = btn.ButtonHighlight.sprite;
				}

				// Icon Image //
				if( useIcon )
				{
					if( btn.ButtonIcon == null )
						missingIconImage = true;
					else if( btn.ButtonIcon.rectTransform.localScale.x != 1.0f )
						iconScale = btn.ButtonIcon.rectTransform.localScale.x;
				}

				// Cooldown Image //
				if( btnObj.FindProperty( "useCooldown" ).boolValue && btn.CooldownImage == null )
					missingCooldownImage = true;
				else if( btn.CooldownImage != null )
				{
					cooldownSprite = btn.CooldownImage.sprite;
					if( btn.CooldownImage.rectTransform.localScale.x != 1.0f )
						cooldownImageScale = btn.CooldownImage.rectTransform.localScale.x;
				}

				// Cooldown Text //
				if( btnObj.FindProperty( "useCooldownText" ).boolValue && btn.CooldownText == null )
					missingCooldownText = true;
			}

			multiCenterAngle = smallestAngle + ( ( largestAngle - smallestAngle ) / 2 );
			multiAnglePer = ( largestAngle - smallestAngle ) / ( targets.Length - 1 );
		}
	}

	void StoreChildTransforms ()
	{
		if( targ.transform == null )
			return;

		if( targ.ButtonBase == null )
			return;

		childTransforms = new List<RectTransform>();
		RectTransform[] childRectTrans = targ.ButtonBase.GetComponentsInChildren<RectTransform>();
		for( int i = 0; i < childRectTrans.Length; i++ )
		{
			if( targ.ButtonBase != null && childRectTrans[ i ] == targ.ButtonBase.rectTransform )
				continue;

			if( targ.ButtonIcon != null && childRectTrans[ i ] == targ.ButtonIcon.rectTransform )
			{
				if( useIconMask )
					continue;
			}

			if( useCooldown.boolValue && useCooldownText.boolValue && targ.CooldownText != null && childRectTrans[ i ] == targ.CooldownText.rectTransform )
				continue;

			childTransforms.Add( childRectTrans[ i ] );
		}

		childObjects = new ReorderableList( childTransforms, typeof( RectTransform ), true, false, false, false );

		childObjects.drawHeaderCallback = ( Rect rect ) =>
		{
			EditorGUI.LabelField( rect, targ.gameObject.name );
		};

		childObjects.drawElementCallback = ( Rect rect, int index, bool isActive, bool isFocused ) =>
		{
			if( index > childTransforms.Count - 1 )
				return;

			var element = childObjects.list;
			rect.y += 2;
			EditorGUI.LabelField( new Rect( rect.x, rect.y, EditorGUIUtility.currentViewWidth, EditorGUIUtility.singleLineHeight ), childTransforms[ index ].name );
		};

		childObjects.onChangedCallback = ( ReorderableList l ) =>
		{
			for( int i = 0; i < childTransforms.Count; i++ )
			{
				childTransforms[ i ].SetSiblingIndex( i );
			}
		};
	}

	void PropertyFieldSingleObject ( SerializedProperty prop )
	{
		EditorGUI.BeginDisabledGroup( targets.Length > 1 );
		EditorGUILayout.PropertyField( prop );
		EditorGUI.EndDisabledGroup();
	}

	public override void OnInspectorGUI ()
	{
		serializedObject.Update();

		// DEVELOPMENT INSPECTOR //
		if( EditorPrefs.GetBool( "UUI_DevelopmentMode" ) )
		{
			EditorGUILayout.Space();
			GUIStyle toolbarStyle = new GUIStyle( EditorStyles.toolbarButton ) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 11, richText = true };
			GUILayout.BeginHorizontal();
			GUILayout.Space( -10 );
			showDefaultInspector = GUILayout.Toggle( showDefaultInspector, ( showDefaultInspector ? "▼" : "►" ) + "<color=#ff0000ff> Development Inspector</color>", toolbarStyle );
			GUILayout.EndHorizontal();

			if( showDefaultInspector )
			{
				EditorGUILayout.Space();

				base.OnInspectorGUI();

				EditorGUILayout.LabelField( "End of Development Inspector", EditorStyles.centeredGreyMiniLabel );
				EditorGUILayout.Space();
				return;
			}
			else if( DragAndDropHover )
				showDefaultInspector = true;

			EditorGUILayout.Space();
		}
		// END DEVELOPMENT INSPECTOR //

		if( isInProjectWindow )
		{
			if( targ.ButtonBase == null )
			{
				EditorGUILayout.HelpBox( "This button does not have the basic needed objects to function. The needed objects cannot be created within the project window.", MessageType.Error );
				EditorGUILayout.HelpBox( "Please drag this prefab into the scene and create all the needed objects and then apply the changes to the prefab before continuing.", MessageType.Info );
				return;
			}
			else
				EditorGUILayout.HelpBox( "You are selecting a prefab in the Project window. Some options may be limited, and objects cannot be created here.", MessageType.Info );
		}

		handlesCenteredText = new GUIStyle( EditorStyles.label ) { normal = new GUIStyleState() { textColor = Color.white } };
		collapsableSectionStyle = new GUIStyle( EditorStyles.label ) { alignment = TextAnchor.MiddleCenter, richText = true };
		collapsableSectionStyle.active.textColor = collapsableSectionStyle.normal.textColor;

		bool valueChanged = false;

		// ------------------------< BUTTON SETTINGS >----------------------- //
		EditorGUILayout.LabelField( "Button Settings", EditorStyles.boldLabel );

		EditorGUI.BeginChangeCheck();
		PropertyFieldSingleObject( buttonBase );
		if( EditorGUI.EndChangeCheck() )
		{
			serializedObject.ApplyModifiedProperties();

			if( targ.ButtonBase != null )
				buttonBaseSprite = targ.ButtonBase.sprite;
		}

		EditorGUI.BeginChangeCheck();
		buttonBaseSprite = ( Sprite )EditorGUILayout.ObjectField( "Base Sprite", buttonBaseSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
		if( EditorGUI.EndChangeCheck() )
		{
			UltimateButton btn;
			for( int i = 0; i < targets.Length; i++ )
			{
				btn = ( UltimateButton )targets[ i ];
				if( btn.ButtonBase != null )
				{
					Undo.RecordObject( btn.ButtonBase, "Update Base Sprite" );
					btn.ButtonBase.enabled = false;
					btn.ButtonBase.sprite = buttonBaseSprite;
					btn.ButtonBase.enabled = true;
				}
			}
		}

		// BASE COLOR //
		EditorGUI.BeginChangeCheck();
		baseColor = EditorGUILayout.ColorField( "Base Color", baseColor );
		if( EditorGUI.EndChangeCheck() )
		{
			UltimateButton btn;
			for( int i = 0; i < targets.Length; i++ )
			{
				btn = ( UltimateButton )targets[ i ];
				if( btn.ButtonBase != null )
				{
					Undo.RecordObject( btn.ButtonBase, "Update Base Color" );
					btn.ButtonBase.enabled = false;
					btn.ButtonBase.color = baseColor;
					btn.ButtonBase.enabled = true;
				}
			}
		}

		if( targ.ButtonBase == null && targets.Length == 1 )
		{
			EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || buttonBaseSprite == null );
			if( GUILayout.Button( "Generate Button Base", EditorStyles.miniButton ) )
			{
				serializedObject.FindProperty( "buttonBase" ).objectReferenceValue = CreateButtonBaseImage( targ.transform );
				serializedObject.ApplyModifiedProperties();
				StoreChildTransforms();
			}
			EditorGUI.EndDisabledGroup();
			return;
		}
		else if( missingButtonBase )
		{
			if( isInProjectWindow )
			{
				EditorGUILayout.HelpBox( "One or more of the selected objects are missing the button base image component, but the selected objects are prefabs in the Project window. Please drag these objects into the scene to fix them.", MessageType.Error );
				return;
			}

			EditorGUILayout.BeginVertical( "Box" );
			EditorGUILayout.HelpBox( "One or more of the selected objects are missing the button base image component.", MessageType.Error );
			EditorGUI.BeginDisabledGroup( buttonBaseSprite == null );
			if( GUILayout.Button( "Attempt Fix" ) )
			{
				for( int i = 0; i < targets.Length; i++ )
				{
					SerializedObject btnObj = new SerializedObject( targets[ i ] );
					UltimateButton btn = ( UltimateButton )targets[ i ];
					if( btn.ButtonBase != null )
						continue;

					btnObj.FindProperty( "buttonBase" ).objectReferenceValue = CreateButtonBaseImage( btn.transform );
					btnObj.ApplyModifiedProperties();
				}
				missingButtonBase = false;
			}
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.EndVertical();
			return;
		}

		EditorGUILayout.Space();

		// BUTTON POSITIONING //
		if( DisplayCollapsibleBoxSection( "Button Positioning", "UB_ButtonPositioning", anchor.enumValueIndex >= 2 && targ.RelativeTransform == null ) )
		{
			// CHANGE CHECK FOR APPLYING SETTINGS DURING RUNTIME //
			if( Application.isPlaying )
			{
				EditorGUILayout.HelpBox( "The application is running. Changes made here will revert when exiting play mode.", MessageType.Warning );
				EditorGUI.BeginChangeCheck();
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( anchor );
			if( EditorGUI.EndChangeCheck() )
			{
				if( overridePositioning.boolValue )
					overridePositioning.boolValue = false;

				serializedObject.ApplyModifiedProperties();

				allUsingOrbitTransform = true;
				for( int i = 0; i < targets.Length; i++ )
				{
					SerializedObject btnObj = new SerializedObject( targets[ i ] );

					if( btnObj.FindProperty( "anchor" ).enumValueIndex != 3 )
						allUsingOrbitTransform = false;
				}
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( buttonSize );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.Slider( activationRange, 0.0f, 2.0f, new GUIContent( "Activation Range", "The range that the Ultimate Button will react to when initiating and dragging the input." ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			// MULTI OBJECT SELECT OPTIONS //
			if( targets.Length > 1 )
			{
				if( allUsingOrbitTransform )
				{
					collapsableSectionStyle.fontStyle = FontStyle.Bold;
					EditorGUILayout.LabelField( "Multi Button Options", collapsableSectionStyle );
					collapsableSectionStyle.fontStyle = FontStyle.Normal;

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( relativeTransform );
					if( EditorGUI.EndChangeCheck() )
					{
						serializedObject.ApplyModifiedProperties();

						allSameRelativeTransform = true;
						for( int i = 0; i < targets.Length; i++ )
						{
							SerializedObject btnObj = new SerializedObject( targets[ i ] );

							if( targ.RelativeTransform != btnObj.FindProperty( "relativeTransform" ).objectReferenceValue )
								allSameRelativeTransform = false;
						}
					}

					if( !allSameRelativeTransform )
						EditorGUILayout.HelpBox( "Different Relative Transforms detected!", MessageType.Warning );

					EditorGUI.BeginChangeCheck();
					multiCenterAngle = EditorGUILayout.Slider( "Center Angle", multiCenterAngle, -180.0f, 180.0f );
					CheckPropertyHover( ref DisplayCenterAngle );
					multiAnglePer = EditorGUILayout.Slider( "Angle Per", multiAnglePer, 0.0f, ( 360.0f / targets.Length ) );
					if( EditorGUI.EndChangeCheck() )
					{
						float totalAngle = ( targets.Length - 1 ) * multiAnglePer;
						float start = multiCenterAngle - ( totalAngle / 2 );
						for( int i = 0; i < targets.Length; i++ )
						{
							SerializedObject btnObj = new SerializedObject( targets[ i ] );
							btnObj.FindProperty( "centerAngle" ).floatValue = start + ( multiAnglePer * i );
							btnObj.ApplyModifiedProperties();
						}
					}

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( orbitDistance );
					CheckPropertyHover( ref DisplayOrbitRadius );
					if( EditorGUI.EndChangeCheck() )
					{
						PropertyUpdated( ref DisplayOrbitRadius );
						serializedObject.ApplyModifiedProperties();
						for( int i = 0; i < targets.Length; i++ )
						{
							SerializedObject btnObj = new SerializedObject( targets[ i ] );
							btnObj.FindProperty( "orbitDistance" ).floatValue = orbitDistance.floatValue;
							btnObj.ApplyModifiedProperties();
						}
					}
				}
				else
					EditorGUILayout.HelpBox( "Multi-select positioning only supported with the Anchor set to Orbit Transform.", MessageType.Warning );
			}
			else
			{
				if( anchor.enumValueIndex >= 2 )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( relativeTransform );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();

					if( targ.RelativeTransform == null )
						EditorGUILayout.HelpBox( "Relative Transform Unassigned", MessageType.Error );
				}

				if( anchor.enumValueIndex == 3 )
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( centerAngle );
					CheckPropertyHover( ref DisplayCenterAngle );
					if( EditorGUI.EndChangeCheck() )
					{
						PropertyUpdated( ref DisplayCenterAngle );
						serializedObject.ApplyModifiedProperties();
						multiCenterAngle = centerAngle.floatValue;
					}

					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( orbitDistance );
					CheckPropertyHover( ref DisplayOrbitRadius );
					if( EditorGUI.EndChangeCheck() )
					{
						PropertyUpdated( ref DisplayOrbitRadius );
						serializedObject.ApplyModifiedProperties();
					}
				}
				else
				{
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.PropertyField( positionHorizontal );
					EditorGUILayout.PropertyField( positionVertical );
					if( EditorGUI.EndChangeCheck() )
						serializedObject.ApplyModifiedProperties();
				}
			}

			// CHANGE CHECK FOR APPLYING SETTINGS DURING RUNTIME //
			if( Application.isPlaying )
			{
				if( EditorGUI.EndChangeCheck() )
					targ.UpdatePositioning();
			}
		}
		EndCollapsibleBoxSection( "UB_ButtonPositioning" );

		// INPUT OPTIONS //
		if( DisplayCollapsibleBoxSection( "Input Settings", "UB_InputSettings" ) )
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( inputHandling );
			EditorGUILayout.PropertyField( boundary );
			EditorGUILayout.PropertyField( trackInput );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( tapDecayRate );
			if( EditorGUI.EndChangeCheck() )
			{
				if( tapDecayRate.floatValue < 0.0f )
					tapDecayRate.floatValue = 0.0f;

				serializedObject.ApplyModifiedProperties();
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( transmitEventData );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();
		}
		EndCollapsibleBoxSection( "UB_InputSettings" );

		// OVERRIDE POSITIONING //
		if( anchor.enumValueIndex < 2 )
		{
			if( DisplayCollapsibleBoxSection( "Player Position Override", "UB_OverridePositioning", overridePositioning, ref valueChanged ) )
			{
				EditorGUILayout.LabelField( $"Horizontal Position Constraint", EditorStyles.boldLabel );
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "repositionConstraintMin.x" ), GUIContent.none, GUILayout.Width( Screen.width / 10 ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				float minHorizontalRange = serializedObject.FindProperty( "repositionConstraintMin.x" ).floatValue;
				float maxHorizontalRange = serializedObject.FindProperty( "repositionConstraintMax.x" ).floatValue;
				EditorGUILayout.MinMaxSlider( ref minHorizontalRange, ref maxHorizontalRange, 0.0f, 100.0f );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.FindProperty( "repositionConstraintMin.x" ).floatValue = Mathf.Round( minHorizontalRange * 10 ) / 10;
					serializedObject.FindProperty( "repositionConstraintMax.x" ).floatValue = Mathf.Round( maxHorizontalRange * 10 ) / 10;
					serializedObject.ApplyModifiedProperties();
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "repositionConstraintMax.x" ), GUIContent.none, GUILayout.Width( Screen.width / 10 ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space();

				EditorGUILayout.LabelField( $"Vertical Position Constraint", EditorStyles.boldLabel );
				EditorGUILayout.BeginHorizontal();

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "repositionConstraintMin.y" ), GUIContent.none, GUILayout.Width( Screen.width / 10 ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUI.BeginChangeCheck();
				float minVerticalRange = serializedObject.FindProperty( "repositionConstraintMin.y" ).floatValue;
				float maxVerticalRange = serializedObject.FindProperty( "repositionConstraintMax.y" ).floatValue;
				EditorGUILayout.MinMaxSlider( ref minVerticalRange, ref maxVerticalRange, 0.0f, 100.0f );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.FindProperty( "repositionConstraintMin.y" ).floatValue = Mathf.Round( minVerticalRange * 10 ) / 10;
					serializedObject.FindProperty( "repositionConstraintMax.y" ).floatValue = Mathf.Round( maxVerticalRange * 10 ) / 10;
					serializedObject.ApplyModifiedProperties();
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( serializedObject.FindProperty( "repositionConstraintMax.y" ), GUIContent.none, GUILayout.Width( Screen.width / 10 ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUILayout.EndHorizontal();

				EditorGUILayout.Space();

				EditorGUI.BeginChangeCheck();
				applyConstraintDuringOverride.boolValue = EditorGUILayout.ToggleLeft( "Apply Constraint During Override", applyConstraintDuringOverride.boolValue );
				saveAsPlayerPrefs.boolValue = EditorGUILayout.ToggleLeft( "Save As PlayerPrefs", saveAsPlayerPrefs.boolValue );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				if( saveAsPlayerPrefs.boolValue )
				{
					string uniqueId = serializedObject.FindProperty( "uniqueId" ).stringValue;
					float horizontalOverride = PlayerPrefs.GetFloat( $"UBHPO_{uniqueId}" );
					float verticalOverride = PlayerPrefs.GetFloat( $"UBVPO_{uniqueId}" );
					float sizeOverride = PlayerPrefs.GetFloat( $"UBSO_{uniqueId}" );
					EditorGUILayout.LabelField( $"Unique ID: {uniqueId}", EditorStyles.miniLabel );
					EditorGUILayout.LabelField( $"Horizontal: {( horizontalOverride < 0.0f ? "Not Overridden" : horizontalOverride.ToString() )}", EditorStyles.miniLabel );
					EditorGUILayout.LabelField( $"Vertical: {( verticalOverride < 0.0f ? "Not Overridden" : verticalOverride.ToString() )}", EditorStyles.miniLabel );
					EditorGUILayout.LabelField( $"Size: {( sizeOverride < 0.0f ? "Not Overridden" : sizeOverride.ToString() )}", EditorStyles.miniLabel );

					EditorGUI.BeginDisabledGroup( Application.isPlaying || ( horizontalOverride < 0.0f && verticalOverride < 0.0f && sizeOverride < 0.0f ) );
					if( GUILayout.Button( "Reset Override" ) && EditorUtility.DisplayDialog( "Ultimate Button Position Override", "Are you sure you want to reset the stored position override information?", "Yes", "No" ) )
					{
						if( !isInProjectWindow )
							targ.ResetOverridePositioning();
						else
						{
							PlayerPrefs.SetFloat( $"UBHPO_{uniqueId}", -1.0f );
							PlayerPrefs.SetFloat( $"UBVPO_{uniqueId}", -1.0f );
							PlayerPrefs.SetFloat( $"UBSO_{uniqueId}", -1.0f );
						}
					}
					EditorGUI.EndDisabledGroup();
				}
			}
			EndCollapsibleBoxSection( "UB_OverridePositioning", overridePositioning.boolValue );
		}
		// ------------------------< END BUTTON SETTINGS >----------------------- //

		EditorGUILayout.Space();

		// ------------------------< OPTIONAL SETTINGS >----------------------- //
		EditorGUILayout.LabelField( "Optional Settings", EditorStyles.boldLabel );
		// -----------------------< INPUT TRANSITION >---------------------- //
		valueChanged = false;
		if( DisplayCollapsibleBoxSection( "Input Transition", "UB_InputTransition", inputTransition, ref valueChanged ) )
		{
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( transitionUntouchedDuration, new GUIContent( "Released Duration", "The time is seconds for the transition to the default state." ) );
			EditorGUILayout.PropertyField( transitionTouchedDuration, new GUIContent( "Pressed Duration", "The time is seconds for the transition to the pressed state." ) );
			if( EditorGUI.EndChangeCheck() )
			{
				if( transitionUntouchedDuration.floatValue < 0 )
					transitionUntouchedDuration.floatValue = 0.0f;

				if( transitionTouchedDuration.floatValue < 0 )
					transitionTouchedDuration.floatValue = 0.0f;

				serializedObject.ApplyModifiedProperties();
			}

			EditorGUILayout.Space();

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( useTension );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				missingTensionImage = false;
				for( int i = 0; i < targets.Length; i++ )
				{
					UltimateButton btn = ( UltimateButton )targets[ i ];
					if( btn.TensionAccent == null )
					{
						missingTensionImage = true;
						continue;
					}

					Undo.RecordObject( btn.TensionAccent.gameObject, ( useTension.boolValue ? "Enable" : "Disable" ) + " Tension Accent" );
					btn.TensionAccent.gameObject.SetActive( useTension.boolValue );
				}
				StoreChildTransforms();
			}

			if( useTension.boolValue )
			{
				EditorGUI.BeginChangeCheck();
				PropertyFieldSingleObject( tensionAccent );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					if( targ.TensionAccent != null )
					{
						Undo.RecordObject( targ.TensionAccent, "Update Tension Color" );
						targ.TensionAccent.color = tensionColorDefault.colorValue;
					}
				}

				EditorGUI.BeginChangeCheck();
				tensionAccentSprite = ( Sprite )EditorGUILayout.ObjectField( "Tension Sprite", tensionAccentSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
				if( EditorGUI.EndChangeCheck() )
				{
					UltimateButton btn;
					for( int i = 0; i < targets.Length; i++ )
					{
						btn = ( UltimateButton )targets[ i ];
						if( btn.TensionAccent != null )
						{
							Undo.RecordObject( btn.TensionAccent, "Update Tension Accent Sprite" );
							btn.TensionAccent.enabled = false;
							btn.TensionAccent.sprite = tensionAccentSprite;
							btn.TensionAccent.enabled = true;
						}
					}
				}

				if( targ.TensionAccent == null && targets.Length == 1 )
				{
					EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || tensionAccentSprite == null );
					if( GUILayout.Button( "Generate Tension Image", EditorStyles.miniButton ) )
					{
						missingTensionImage = false;
						serializedObject.FindProperty( "tensionAccent" ).objectReferenceValue = CreateTensionImage( targ.ButtonBase.transform );
						serializedObject.ApplyModifiedProperties();
						StoreChildTransforms();
					}
					EditorGUI.EndDisabledGroup();
				}
				else if( missingTensionImage )
				{
					EditorGUILayout.BeginVertical( "Box" );
					EditorGUILayout.HelpBox( "One or more of the selected objects are missing the tension image component.", MessageType.Error );
					EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || tensionAccentSprite == null );
					if( GUILayout.Button( "Attempt Fix" ) )
					{
						for( int i = 0; i < targets.Length; i++ )
						{
							SerializedObject btnObj = new SerializedObject( targets[ i ] );
							UltimateButton btn = ( UltimateButton )targets[ i ];
							if( btn.TensionAccent != null )
								continue;

							btnObj.FindProperty( "tensionAccent" ).objectReferenceValue = CreateTensionImage( btn.ButtonBase.transform );
							btnObj.ApplyModifiedProperties();
						}
						missingTensionImage = false;
					}
					EditorGUI.EndDisabledGroup();
					EditorGUILayout.EndVertical();
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( tensionColorDefault, new GUIContent( "Default Color", "The Color of the Tension with no input." ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.TensionAccent != null )
						{
							Undo.RecordObject( btn.TensionAccent, "Update Tension Color" );
							btn.TensionAccent.enabled = false;
							btn.TensionAccent.color = tensionColorDefault.colorValue;
							btn.TensionAccent.enabled = true;
						}
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( tensionColorActive, new GUIContent( "Pressed Color", "The Color of the Tension when there is input." ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUILayout.Space();
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( useFade );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				for( int i = 0; i < targets.Length; i++ )
				{
					UltimateButton btn = ( UltimateButton )targets[ i ];
					Undo.RecordObject( btn.GetComponent<CanvasGroup>(), "Edit Button Fade" );
					btn.GetComponent<CanvasGroup>().alpha = useFade.boolValue ? fadeUntouched.floatValue : 1.0f;
				}
			}

			if( useFade.boolValue )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( fadeUntouched, new GUIContent( "Default Alpha" ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						Undo.RecordObject( btn.GetComponent<CanvasGroup>(), "Edit Button Fade" );
						btn.GetComponent<CanvasGroup>().alpha = fadeUntouched.floatValue;
					}
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( fadeTouched, new GUIContent( "Pressed Alpha" ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();

				EditorGUILayout.Space();
			}

			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( useScale );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			if( useScale.boolValue )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( scaleTouched, new GUIContent( "Pressed Scale" ) );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
			}
		}
		EndCollapsibleBoxSection( "UB_InputTransition", inputTransition.boolValue );
		if( valueChanged )
		{
			missingTensionImage = false;
			for( int i = 0; i < targets.Length; i++ )
			{
				SerializedObject btnObj = new SerializedObject( targets[ i ] );
				UltimateButton btn = ( UltimateButton )targets[ i ];

				if( btn == null )
					continue;

				if( !btn.gameObject.GetComponent<CanvasGroup>() )
					btn.gameObject.AddComponent<CanvasGroup>();

				if( btn.TensionAccent == null && targets.Length > 1 )
					missingTensionImage = true;

				if( btnObj.FindProperty( "useTension" ).boolValue && btn.TensionAccent != null )
				{
					Undo.RecordObject( btn.TensionAccent.gameObject, ( inputTransition.boolValue ? "Enable" : "Disable" ) + " Input Transition" );
					btn.TensionAccent.gameObject.SetActive( inputTransition.boolValue );
				}

				if( btnObj.FindProperty( "useFade" ).boolValue )
				{
					Undo.RecordObject( btn.gameObject.GetComponent<CanvasGroup>(), ( inputTransition.boolValue ? "Enable" : "Disable" ) + " Input Transition" );
					btn.gameObject.GetComponent<CanvasGroup>().alpha = inputTransition.boolValue ? btnObj.FindProperty( "fadeUntouched" ).floatValue : 1.0f;
				}
			}
			StoreChildTransforms();
		}
		// ---------------------< END INPUT TRANSITION >-------------------- //

		// --------------------------< HIGHLIGHT >-------------------------- //
		valueChanged = false;
		if( DisplayCollapsibleBoxSection( "Highlight", "UB_Highlight", useHighlight, ref valueChanged ) )
		{
			EditorGUI.BeginChangeCheck();
			highlightColor = EditorGUILayout.ColorField( "Highlight Color", highlightColor );
			if( EditorGUI.EndChangeCheck() )
			{
				UltimateButton btn;
				for( int i = 0; i < targets.Length; i++ )
				{
					btn = ( UltimateButton )targets[ i ];
					if( btn.ButtonHighlight != null )
					{
						Undo.RecordObject( btn.ButtonHighlight, "Update Highlight Color" );
						btn.ButtonHighlight.enabled = false;
						btn.ButtonHighlight.color = highlightColor;
						btn.ButtonHighlight.enabled = true;
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			PropertyFieldSingleObject( buttonHighlight );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				if( targ.ButtonHighlight != null )
					highlightColor = targ.ButtonHighlight.color;
			}

			EditorGUI.BeginChangeCheck();
			buttonHighlightSprite = ( Sprite )EditorGUILayout.ObjectField( "Highlight Sprite", buttonHighlightSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
			if( EditorGUI.EndChangeCheck() && targ.ButtonHighlight != null )
			{
				UltimateButton btn;
				for( int i = 0; i < targets.Length; i++ )
				{
					btn = ( UltimateButton )targets[ i ];
					if( btn.ButtonHighlight != null )
					{
						Undo.RecordObject( btn.ButtonHighlight, "Update Highlight Sprite" );
						btn.ButtonHighlight.enabled = false;
						btn.ButtonHighlight.sprite = buttonHighlightSprite;
						btn.ButtonHighlight.enabled = true;
					}
				}
			}

			if( missingHighlightImage && !isInProjectWindow )
			{
				EditorGUILayout.BeginVertical( "Box" );
				EditorGUILayout.HelpBox( "One or more of the selected objects are missing the highlight image component.", MessageType.Error );
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || buttonHighlightSprite == null );
				if( GUILayout.Button( "Attempt Fix" ) )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						SerializedObject btnObj = new SerializedObject( targets[ i ] );
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.ButtonIcon != null )
							continue;

						btnObj.FindProperty( "buttonHighlight" ).objectReferenceValue = CreateHighlightImage( btn.ButtonBase.transform );
						btnObj.ApplyModifiedProperties();
					}
					missingHighlightImage = false;
				}
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndVertical();
			}
			else if( targ.ButtonHighlight == null )
			{
				if( isInProjectWindow )
					EditorGUILayout.HelpBox( "Please drag this prefab into the scene and create all the needed objects and then apply the changes to the prefab before continuing.", MessageType.Info );

				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || buttonHighlightSprite == null );
				if( GUILayout.Button( "Generate Button Highlight", EditorStyles.miniButton ) )
				{
					serializedObject.FindProperty( "buttonHighlight" ).objectReferenceValue = CreateHighlightImage( targ.ButtonBase.transform );
					serializedObject.ApplyModifiedProperties();

					StoreChildTransforms();
				}
				EditorGUI.EndDisabledGroup();
			}
		}
		EndCollapsibleBoxSection( "UB_Highlight", useHighlight.boolValue );
		if( valueChanged )
		{
			missingHighlightImage = false;
			for( int i = 0; i < targets.Length; i++ )
			{
				UltimateButton btn = ( UltimateButton )targets[ i ];

				if( btn.ButtonHighlight == null )
				{
					if( targets.Length > 1 )
						missingHighlightImage = true;
					
					continue;
				}

				Undo.RecordObject( btn.ButtonHighlight.gameObject, ( useHighlight.boolValue ? "Enable " : "Disable " ) + "Button Highlight" );
				btn.ButtonHighlight.gameObject.SetActive( useHighlight.boolValue );
			}
			StoreChildTransforms();
		}
		// ------------------------< END HIGHLIGHT >------------------------ //

		// ------------------------------ ICON SETTINGS ----------------------------- //
		valueChanged = false;
		if( DisplayCollapsibleBoxSection( "Icon", "UB_IconSettings", ref useIcon, ref valueChanged ) )
		{
			EditorGUI.BeginChangeCheck();
			PropertyFieldSingleObject( buttonIcon );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			EditorGUI.BeginChangeCheck();
			iconSprite = ( Sprite )EditorGUILayout.ObjectField( "Icon Sprite", iconSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
			if( EditorGUI.EndChangeCheck() )
			{
				for( int i = 0; i < targets.Length; i++ )
				{
					UltimateButton btn = ( UltimateButton )targets[ i ];
					if( btn.ButtonIcon == null )
						continue;

					Undo.RecordObject( btn.ButtonIcon, "Update Icon Sprite" );
					btn.ButtonIcon.enabled = false;
					btn.ButtonIcon.sprite = iconSprite;

					if( btn.ButtonIcon.sprite == null )
					{
						iconColor.a = 0.0f;
						btn.ButtonIcon.color = iconColor;
					}
					else if( btn.ButtonIcon.color.a == 0.0f )
					{
						iconColor.a = 1.0f;
						btn.ButtonIcon.color = iconColor;
					}
					btn.ButtonIcon.enabled = true;
				}
			}

			if( missingIconImage && !isInProjectWindow )
			{
				EditorGUILayout.BeginVertical( "Box" );
				EditorGUILayout.HelpBox( "One or more of the selected Ultimate Button's is missing the icon component.", MessageType.Error );
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || iconSprite == null || targets.Length > 1 );
				if( GUILayout.Button( "Attempt Fix" ) )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						SerializedObject btnObj = new SerializedObject( targets[ i ] );
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.ButtonIcon != null )
							continue;

						btnObj.FindProperty( "buttonIcon" ).objectReferenceValue = CreateIconImage( btn.ButtonBase.transform );
						btnObj.ApplyModifiedProperties();
					}
					missingIconImage = false;
				}
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndVertical();
			}
			else if( targ.ButtonIcon == null )
			{
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || iconSprite == null || targets.Length > 1 );
				if( GUILayout.Button( "Generate Icon Image", EditorStyles.miniButton ) )
				{
					serializedObject.FindProperty( "buttonIcon" ).objectReferenceValue = CreateIconImage( targ.ButtonBase.transform );
					serializedObject.ApplyModifiedProperties();
					StoreChildTransforms();
				}
				EditorGUI.EndDisabledGroup();
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				iconColor = EditorGUILayout.ColorField( new GUIContent( "Image Color", "The color of the icon image." ), iconColor );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.ButtonIcon == null )
							continue;

						Undo.RecordObject( btn.ButtonIcon, "Update Icon Color" );
						btn.ButtonIcon.enabled = false;
						btn.ButtonIcon.color = iconColor;
						btn.ButtonIcon.enabled = true;
					}
				}

				EditorGUI.BeginChangeCheck();
				iconScale = EditorGUILayout.Slider( new GUIContent( "Icon Scale", "The scale of the icon for the button." ), iconScale, 0.0f, 2.0f );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.ButtonIcon == null )
							continue;

						Undo.RecordObject( btn.ButtonIcon.rectTransform, "Change Icon Scale" );
						btn.ButtonIcon.rectTransform.localScale = Vector3.one * iconScale;
					}

				}

				if( targets.Length == 1 )
				{
					EditorGUILayout.Space();

					EditorGUI.BeginChangeCheck();
					useIconMask = EditorGUILayout.Toggle( new GUIContent( "Use Icon Mask", "Determines if the icon should be placed inside a mask image or not." ), useIconMask );
					if( EditorGUI.EndChangeCheck() )
					{
						if( !useIconMask && buttonIconMask != null )
						{
							Undo.SetTransformParent( targ.ButtonIcon.transform, targ.ButtonBase.transform, "Disable Icon" );
							Undo.DestroyObjectImmediate( buttonIconMask.gameObject );
							useIconMask = false;
							ConfigureIconParent();
							StoreChildTransforms();
						}
					}

					if( useIconMask )
					{
						if( targets.Length > 1 )
							EditorGUILayout.HelpBox( "Icon Mask options not available for multi-object editing.", MessageType.Warning );
						EditorGUI.BeginChangeCheck();
						EditorGUI.BeginDisabledGroup( targets.Length > 1 );
						buttonIconMask = ( Image )EditorGUILayout.ObjectField( new GUIContent( "Icon Mask", "The icon mask image to be used for the button icon." ), buttonIconMask, typeof( Image ), true );
						EditorGUI.EndDisabledGroup();
						if( EditorGUI.EndChangeCheck() )
						{
							if( buttonIconMask != null )
								iconMaskSprite = buttonIconMask.sprite;

							ConfigureIconParent();
						}

						EditorGUI.BeginChangeCheck();
						iconMaskSprite = ( Sprite )EditorGUILayout.ObjectField( "└ Image Sprite", iconMaskSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
						if( EditorGUI.EndChangeCheck() && buttonIconMask != null )
						{
							Undo.RecordObject( buttonIconMask, "Update Icon Mask Sprite" );
							buttonIconMask.sprite = iconMaskSprite;
						}

						if( buttonIconMask == null )
						{
							EditorGUI.BeginDisabledGroup( iconMaskSprite == null || isInProjectWindow || targets.Length > 1 );
							if( GUILayout.Button( "Generate Mask Image", EditorStyles.miniButton ) )
							{
								GameObject newGameObject = new GameObject();
								RectTransform trans = newGameObject.AddComponent<RectTransform>();
								newGameObject.AddComponent<CanvasRenderer>();
								Image imageComponent = newGameObject.AddComponent<Image>();
								imageComponent.sprite = iconMaskSprite;
								Mask maskComponent = newGameObject.AddComponent<Mask>();
								maskComponent.showMaskGraphic = false;

								newGameObject.transform.SetParent( targ.ButtonBase.transform );
								newGameObject.transform.SetAsFirstSibling();

								trans.anchorMin = new Vector2( 0.0f, 0.0f );
								trans.anchorMax = new Vector2( 1.0f, 1.0f );
								trans.offsetMin = Vector2.zero;
								trans.offsetMax = Vector2.zero;
								trans.localScale = Vector3.one;
								trans.localPosition = Vector3.zero;
								trans.localRotation = Quaternion.identity;

								newGameObject.name = "Button Icon Mask";

								buttonIconMask = imageComponent;

								Undo.RegisterCreatedObjectUndo( newGameObject, "Create Icon Mask Image Object" );
								ConfigureIconParent();
								StoreChildTransforms();
							}
							EditorGUI.EndDisabledGroup();
						}
					}
				}
			}
			GUILayout.Space( 1 );
		}
		EndCollapsibleBoxSection( "UB_IconSettings", useIcon );
		if( valueChanged )
		{
			missingIconImage = false;
			for( int i = 0; i < targets.Length; i++ )
			{
				UltimateButton btn = ( UltimateButton )targets[ i ];

				if( btn.ButtonIcon == null )
				{
					if( targets.Length > 1 )
						missingIconImage = true;
		
					continue;
				}

				Image btnIconMask = btn.ButtonIcon.transform.parent.GetComponent<Image>();

				if( !useIcon && btn.ButtonIcon != null && useIconMask && btnIconMask != null )
				{
					Undo.SetTransformParent( btn.ButtonIcon.transform, targ.ButtonBase.transform, "Disable Icon" );
					Undo.DestroyObjectImmediate( btnIconMask.gameObject );
					useIconMask = false;
				}

				Undo.RecordObject( btn.ButtonIcon.gameObject, ( useIcon ? "Enable " : "Disable " ) + "Button Icon" );
				btn.ButtonIcon.gameObject.SetActive( useIcon );
			}

			ConfigureIconParent();
			StoreChildTransforms();
		}
		// ---------------------------- END ICON SETTINGS -------------------------- //

		// ---------------------------- COOLDOWN SETTINGS ---------------------------- //
		valueChanged = false;
		if( DisplayCollapsibleBoxSection( "Cooldown", "UB_CooldownSettings", useCooldown, ref valueChanged ) )
		{
			EditorGUI.BeginChangeCheck();
			PropertyFieldSingleObject( cooldownImage );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				if( targ.CooldownImage != null )
				{
					cooldownSprite = targ.CooldownImage.sprite;
					cooldownColor = targ.CooldownImage.color;
					fillMethod = targ.CooldownImage.fillMethod;
				}
				else
					EditorPrefs.SetBool( "UB_CooldownTextSettings", false );
			}

			EditorGUI.BeginChangeCheck();
			cooldownSprite = ( Sprite )EditorGUILayout.ObjectField( "Cooldown Sprite", cooldownSprite, typeof( Sprite ), true, GUILayout.Height( EditorGUIUtility.singleLineHeight ) );
			if( EditorGUI.EndChangeCheck() && targ.CooldownImage != null )
			{
				for( int i = 0; i < targets.Length; i++ )
				{
					UltimateButton btn = ( UltimateButton )targets[ i ];
					if( btn.CooldownImage == null )
						continue;

					Undo.RecordObject( btn.CooldownImage, "Update Cooldown Sprite" );
					btn.CooldownImage.enabled = false;
					btn.CooldownImage.sprite = cooldownSprite;
					btn.CooldownImage.enabled = true;
				}
			}

			if( missingCooldownImage && !isInProjectWindow )
			{
				EditorGUILayout.BeginVertical( "Box" );
				EditorGUILayout.HelpBox( "One or more of the selected Ultimate Button's is missing the cooldown image component.", MessageType.Error );
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || cooldownSprite == null );
				if( GUILayout.Button( "Attempt Fix" ) )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						SerializedObject btnObj = new SerializedObject( targets[ i ] );
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownImage != null )
							continue;

						btnObj.FindProperty( "useCooldown" ).boolValue = true;
						btnObj.FindProperty( "cooldownImage" ).objectReferenceValue = CreateCooldownImage( btn.ButtonBase.transform );
						btnObj.ApplyModifiedProperties();

						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
					}
					missingCooldownImage = false;
				}
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndVertical();
			}
			else if( targ.CooldownImage == null )
			{
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning || cooldownSprite == null );
				if( GUILayout.Button( "Generate Cooldown Image", EditorStyles.miniButton ) )
				{
					serializedObject.FindProperty( "cooldownImage" ).objectReferenceValue = CreateCooldownImage( targ.ButtonBase.transform );
					serializedObject.ApplyModifiedProperties();
					StoreChildTransforms();
				}
				EditorGUI.EndDisabledGroup();
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				cooldownColor = EditorGUILayout.ColorField( new GUIContent( "Image Color", "The color of the cooldown image." ), cooldownColor );
				if( EditorGUI.EndChangeCheck() && targ.CooldownImage != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownImage == null )
							continue;

						Undo.RecordObject( btn.CooldownImage, "Update Cooldown Color" );
						btn.CooldownImage.enabled = false;
						btn.CooldownImage.color = cooldownColor;
						btn.CooldownImage.enabled = true;
					}
				}

				EditorGUI.BeginChangeCheck();
				fillMethod = ( Image.FillMethod )EditorGUILayout.EnumPopup( "Fill Method", fillMethod );
				if( EditorGUI.EndChangeCheck() && targ.CooldownImage != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownImage == null )
							continue;

						Undo.RecordObject( btn.CooldownImage, "Change Fill Method" );
						btn.CooldownImage.enabled = false;
						btn.CooldownImage.fillMethod = fillMethod;
						btn.CooldownImage.enabled = true;
					}
				}

				EditorGUI.BeginChangeCheck();
				cooldownImageScale = EditorGUILayout.Slider( "Image Scale", cooldownImageScale, 0.01f, 2.0f );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownImage == null )
							continue;

						Undo.RecordObject( btn.CooldownImage, "Change Cooldown Image Scale" );
						btn.CooldownImage.rectTransform.localScale = Vector3.one * cooldownImageScale;
					}
				}

				EditorGUI.BeginChangeCheck();
				fillAmount = EditorGUILayout.Slider( "Fill Amount Test", fillAmount, 0.0f, 1.0f );
				if( EditorGUI.EndChangeCheck() )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownImage == null )
							continue;

						Undo.RecordObject( btn.CooldownImage, "Change Cooldown Image Fill" );
						btn.CooldownImage.enabled = false;
						btn.CooldownImage.fillAmount = fillAmount;
						btn.CooldownImage.enabled = true;
					}
				}
			}
		}
		EndCollapsibleBoxSection( "UB_CooldownSettings", useCooldown.boolValue );
		if( valueChanged )
		{
			missingCooldownImage = false;
			for( int i = 0; i < targets.Length; i++ )
			{
				UltimateButton btn = ( UltimateButton )targets[ i ];

				if( btn.CooldownImage == null )
				{
					if( targets.Length > 1 )
						missingCooldownImage = true;

					continue;
				}

				Undo.RecordObject( btn.CooldownImage.gameObject, ( useCooldown.boolValue ? "Enable " : "Disable " ) + "Button Cooldown" );
				btn.CooldownImage.gameObject.SetActive( useCooldown.boolValue );
			}

			if( EditorPrefs.GetBool( "UB_CooldownTextSettings" ) )
				EditorPrefs.SetBool( "UB_CooldownTextSettings", false );

			StoreChildTransforms();
		}
		// ------------------------------ END COOLDOWN SETTINGS ------------------------------ //

		// ------------------------------ COOLDOWN TEXT SETTINGS ------------------------------ //
		valueChanged = false;
		EditorGUI.BeginDisabledGroup( !useCooldown.boolValue || targ.CooldownImage == null );
		if( DisplayCollapsibleBoxSection( "Cooldown Text", "UB_CooldownTextSettings", useCooldownText, ref valueChanged ) )
		{
			EditorGUI.BeginChangeCheck();
			PropertyFieldSingleObject( cooldownText );
			if( EditorGUI.EndChangeCheck() )
			{
				serializedObject.ApplyModifiedProperties();

				if( targ.CooldownText != null )
				{
					textAnchorMod = targ.CooldownText.rectTransform.anchorMax.y;
					textColor = targ.CooldownText.color;

					if( targ.CooldownText.GetComponent<Outline>() )
						textOutlineColor = targ.CooldownText.GetComponent<Outline>().effectColor;
				}
			}

			EditorGUI.BeginChangeCheck();
			textFont = ( Font )EditorGUILayout.ObjectField( "Font", textFont, typeof( Font ), false );
			if( EditorGUI.EndChangeCheck() )
			{
				if( textFont != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						Undo.RecordObject( btn.CooldownText, "Update Cooldown Text Font" );
						btn.CooldownText.enabled = false;
						btn.CooldownText.font = textFont;
						btn.CooldownText.enabled = true;
					}
				}
			}

			if( targ.CooldownText == null && targets.Length == 1 )
			{
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning );
				if( GUILayout.Button( "Generate Cooldown Text", EditorStyles.miniButton ) )
				{
					serializedObject.FindProperty( "cooldownText" ).objectReferenceValue = CreateCooldownText( targ.CooldownImage.transform );
					serializedObject.ApplyModifiedProperties();
					StoreChildTransforms();
				}
				EditorGUI.EndDisabledGroup();
			}
			else if( missingCooldownText )
			{
				EditorGUILayout.BeginVertical( "Box" );
				EditorGUILayout.HelpBox( "One or more of the selected Ultimate Button's is missing the cooldown text component.", MessageType.Error );
				EditorGUI.BeginDisabledGroup( IsInProjectWindowWarning );
				if( GUILayout.Button( "Attempt Fix" ) )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						SerializedObject btnObj = new SerializedObject ( targets[ i ] );
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText != null )
							continue;

						btnObj.FindProperty( "cooldownText" ).objectReferenceValue = CreateCooldownText( btn.CooldownImage.transform );
						btnObj.ApplyModifiedProperties();

						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
					}
					missingCooldownText = false;
				}
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.EndVertical();
			}
			else
			{
				EditorGUI.BeginChangeCheck();
				textOutline = EditorGUILayout.Toggle( new GUIContent( "Text Outline", "Determines if the text should have an outline or not." ), textOutline );
				if( EditorGUI.EndChangeCheck() && targ.CooldownText != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						if( textOutline && !btn.CooldownText.gameObject.GetComponent<Outline>() )
						{
							Undo.AddComponent( btn.CooldownText.gameObject, typeof( Outline ) );
							btn.CooldownText.gameObject.GetComponent<Outline>().effectColor = textOutlineColor;
						}
						else if( !textOutline && btn.CooldownText.gameObject.GetComponent<Outline>() )
							Undo.DestroyObjectImmediate( btn.CooldownText.gameObject.GetComponent<Outline>() );
					}
				}

				EditorGUI.BeginDisabledGroup( !textOutline );
				EditorGUI.BeginChangeCheck();
				textOutlineColor = EditorGUILayout.ColorField( new GUIContent( "Outline Color", "The color to apply to the outline component." ), textOutlineColor );
				if( EditorGUI.EndChangeCheck() && targ.CooldownText != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						Outline outline = btn.CooldownText.gameObject.GetComponent<Outline>();

						Undo.RecordObject( outline, "Update Outline Color" );
						outline.enabled = false;
						outline.effectColor = textOutlineColor;
						outline.enabled = true;
					}
				}
				EditorGUI.EndDisabledGroup();

				EditorGUI.BeginChangeCheck();
				textAnchorMod = EditorGUILayout.Slider( new GUIContent( "Text Size", "The size of the cooldown text." ), textAnchorMod, 0.0f, 1.0f );
				if( EditorGUI.EndChangeCheck() && targ.CooldownText != null )
				{
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						Undo.RecordObject( btn.CooldownText.rectTransform, "Update Cooldown Text Anchor" );
						btn.CooldownText.rectTransform.anchorMin = new Vector2( 0.0f, Mathf.Lerp( 0.5f, 0.0f, textAnchorMod ) );
						btn.CooldownText.rectTransform.anchorMax = new Vector2( 1.0f, ( 1.0f - Mathf.Lerp( 0.5f, 0.0f, textAnchorMod ) ) );
						btn.CooldownText.rectTransform.offsetMin = Vector2.zero;
						btn.CooldownText.rectTransform.offsetMax = Vector2.zero;
						btn.CooldownText.rectTransform.localScale = Vector3.one;
					}
					PropertyUpdated( ref DisplayCooldownTextAnchor );
				}
				CheckPropertyHover( ref DisplayCooldownTextAnchor );
				
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( displayDecimalCooldown, new GUIContent( "Display Decimal" ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						Undo.RecordObject( btn.CooldownText, "Display Decimal Cooldown" );

						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );

						if( btn.CooldownText != null && cooldownTestValue == 0.0f )
						{
							btn.CooldownText.rectTransform.localScale = Vector3.one;
							btn.CooldownText.text = displayDecimalCooldown.boolValue ? "0.0" : "00";
						}
					}
				}
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( cooldownTextScaleCurve, new GUIContent( "Text Scale Curve" ) );
				if( EditorGUI.EndChangeCheck() )
				{
					serializedObject.ApplyModifiedProperties();

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						if( btn.CooldownText == null )
							continue;

						Undo.RecordObject( btn.CooldownText, "Modify Cooldown Text Curve" );
						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );

						if( btn.CooldownText != null && cooldownTestValue == 0.0f )
						{
							btn.CooldownText.rectTransform.localScale = Vector3.one;
							btn.CooldownText.text = displayDecimalCooldown.boolValue ? "0.0" : "00";
						}
					}
				}

				EditorGUILayout.Space();
				EditorGUILayout.LabelField( "Cooldown Test", EditorStyles.boldLabel );
				EditorGUI.BeginDisabledGroup( cooldownImage.objectReferenceValue == null );
				cooldownTestValueMax = EditorGUILayout.FloatField( "Max Time", cooldownTestValueMax );
				EditorGUI.BeginChangeCheck();
				cooldownTestValue = EditorGUILayout.Slider( "Test Value", cooldownTestValue, 0.0f, cooldownTestValueMax );
				if( EditorGUI.EndChangeCheck() )
				{
					simulateCooldown = false;
					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						Undo.RecordObject( targ.CooldownImage, "Cooldown Test" );
						if( btn.CooldownText != null )
							Undo.RecordObject( btn.CooldownText, "Cooldown Test" );

						btn.CooldownImage.enabled = false;
						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
						btn.CooldownImage.enabled = true;
					}
				}
				if( GUILayout.Button( !simulateCooldown ? "Simulate" : "Stop Simulation" ) )
				{
					simulateCooldown = !simulateCooldown;
					cooldownTestValue = cooldownTestValueMax;
					timeBetweenLastFrame = Time.time;

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						Undo.RecordObject( targ.CooldownImage, "Cooldown Test" );
						if( btn.CooldownText != null )
							Undo.RecordObject( btn.CooldownText, "Cooldown Test" );

						btn.CooldownImage.enabled = false;
						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
						btn.CooldownImage.enabled = true;
					}
				}
				EditorGUI.EndDisabledGroup();

				if( simulateCooldown && Event.current.type == EventType.Repaint )
				{
					cooldownTestValue -= Time.time - timeBetweenLastFrame;
					timeBetweenLastFrame = Time.time;

					for( int i = 0; i < targets.Length; i++ )
					{
						UltimateButton btn = ( UltimateButton )targets[ i ];
						btn.CooldownImage.enabled = false;
						btn.UpdateCooldown( cooldownTestValue, cooldownTestValueMax );
						btn.CooldownImage.enabled = true;
					}
				}
			}
		}
		EndCollapsibleBoxSection( "UB_CooldownTextSettings", useCooldownText.boolValue );
		EditorGUI.EndDisabledGroup();
		if( valueChanged )
		{
			missingCooldownText = false;
			for( int i = 0; i < targets.Length; i++ )
			{
				UltimateButton btn = ( UltimateButton )targets[ i ];

				if( btn.CooldownText == null )
				{
					if( targets.Length > 1 )
						missingCooldownText = true;

					continue;
				}

				Undo.RecordObject( btn.CooldownText.gameObject, ( useCooldownText.boolValue ? "Enable " : "Disable " ) + "Button Cooldown Text" );
				btn.CooldownText.gameObject.SetActive( useCooldownText.boolValue );
			}
			StoreChildTransforms();
		}
		// ------------------------------ END COOLDOWN TEXT SETTINGS ------------------------------ //

		// ------------------------------ CHILD HEIRARCHY ------------------------------ //
		if( targets.Length == 1 )
		{
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField( "Reorder Child Hierarchy" );
			EditorPrefs.SetBool( "UB_ChildHierarchy", GUILayout.Toggle( EditorPrefs.GetBool( "UB_ChildHierarchy" ), EditorPrefs.GetBool( "UB_ChildHierarchy" ) ? "-" : "+", EditorStyles.miniButton, GUILayout.Width( 20 ) ) );
			EditorGUILayout.EndHorizontal();
			if( EditorPrefs.GetBool( "UB_ChildHierarchy" ) )
				childObjects.DoLayoutList();
			// ------------------------------ END CHILD HEIRARCHY ------------------------------ //

			EditorGUILayout.Space();

			// ------------------------< SCRIPT REFERENCE >----------------------- //
			EditorGUILayout.LabelField( "Script Reference", EditorStyles.boldLabel );
#if ENABLE_INPUT_SYSTEM
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( serializedObject.FindProperty( "_controlPath" ) );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();
#endif
			EditorGUI.BeginChangeCheck();
			EditorGUILayout.PropertyField( buttonName );
			if( EditorGUI.EndChangeCheck() )
				serializedObject.ApplyModifiedProperties();

			if( buttonName.stringValue == string.Empty )
				EditorGUILayout.HelpBox( "Please assign a Button Name in order to be able to get this button's input data.", MessageType.Warning );
			else
			{
				EditorGUILayout.BeginVertical( "Box" );
				GUILayout.Space( 1 );
				EditorGUILayout.LabelField( "Example Code Generator", EditorStyles.boldLabel );

				exampleCodeIndex = EditorGUILayout.Popup( "Function", exampleCodeIndex, exampleCodeOptions.ToArray() );

				EditorGUILayout.LabelField( "Function Description", EditorStyles.boldLabel );
				GUIStyle wordWrappedLabel = new GUIStyle( GUI.skin.label ) { wordWrap = true };
				EditorGUILayout.LabelField( exampleCodes[ exampleCodeIndex ].optionDescription, wordWrappedLabel );

				EditorGUILayout.LabelField( "Example Code", EditorStyles.boldLabel );
				GUIStyle wordWrappedTextArea = new GUIStyle( GUI.skin.textArea ) { wordWrap = true };
				EditorGUILayout.TextArea( string.Format( exampleCodes[ exampleCodeIndex ].basicCode, buttonName.stringValue ), wordWrappedTextArea );

				GUILayout.Space( 1 );
				EditorGUILayout.EndVertical();
			}

			if( GUILayout.Button( "Open Documentation" ) )
				UltimateButtonReadmeEditor.OpenReadmeDocumentation();

			// BUTTON EVENTS //
			if( DisplayCollapsibleBoxSection( "Button Events", "UB_ButtonEvents" ) )
			{
				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField( onButtonDown );
				EditorGUILayout.PropertyField( onButtonUp );
				if( EditorGUI.EndChangeCheck() )
					serializedObject.ApplyModifiedProperties();
			}
			EndCollapsibleBoxSection( "UB_ButtonEvents" );
		}

		EditorGUILayout.Space();

		if( Event.current.type == EventType.Layout )
		{
			if( !disableDragAndDrop && isDraggingObject )
				Repaint();

			if( isDirty )
				SceneView.RepaintAll();

			if( simulateCooldown )
			{
				if( cooldownTestValue <= 0.0f )
				{
					simulateCooldown = false;
					cooldownTestValue = 0.0f;
					targ.CooldownImage.enabled = false;
					targ.ResetCooldown();
					targ.CooldownImage.enabled = true;
				}

				Repaint();
			}
			isDirty = false;
		}
	}

	Image CreateButtonBaseImage ( Transform parent )
	{
		GameObject newGameObject = new GameObject();
		RectTransform trans = newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image imageComponent = newGameObject.AddComponent<Image>();

		imageComponent.sprite = buttonBaseSprite;
		imageComponent.color = baseColor;

		newGameObject.transform.SetParent( parent );
		newGameObject.transform.SetAsFirstSibling();

		newGameObject.name = "Button Base";

		trans.anchorMin = new Vector2( 0.5f, 0.5f );
		trans.anchorMax = new Vector2( 0.5f, 0.5f );
		trans.pivot = new Vector2( 0.5f, 0.5f );
		trans.anchoredPosition = Vector2.zero;
		trans.localScale = Vector3.one;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		Undo.RegisterCreatedObjectUndo( newGameObject, "Create Button Base Object" );

		return imageComponent;
	}

	Image CreateTensionImage ( Transform parent )
	{
		GameObject newGameObject = new GameObject();
		newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image imageComponent = newGameObject.AddComponent<Image>();

		imageComponent.sprite = tensionAccentSprite;
		imageComponent.color = tensionColorDefault.colorValue;

		newGameObject.transform.SetParent( parent );
		newGameObject.transform.SetAsLastSibling();

		newGameObject.name = "Tension Accent";

		RectTransform trans = newGameObject.GetComponent<RectTransform>();

		trans.anchorMin = new Vector2( 0.0f, 0.0f );
		trans.anchorMax = new Vector2( 1.0f, 1.0f );
		trans.offsetMin = Vector2.zero;
		trans.offsetMax = Vector2.zero;
		trans.pivot = new Vector2( 0.5f, 0.5f );
		trans.anchoredPosition = Vector2.zero;
		trans.localScale = Vector3.one;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		Undo.RegisterCreatedObjectUndo( newGameObject, "Create Tension Accent Object" );

		return imageComponent;
	}

	Image CreateHighlightImage ( Transform parent )
	{
		GameObject newGameObject = new GameObject();
		newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image imageComponent = newGameObject.AddComponent<Image>();

		imageComponent.sprite = buttonHighlightSprite;
		imageComponent.color = highlightColor;

		newGameObject.transform.SetParent( parent );
		newGameObject.transform.SetAsFirstSibling();

		newGameObject.name = "Button Highlight";

		RectTransform trans = newGameObject.GetComponent<RectTransform>();

		trans.anchorMin = new Vector2( 0.0f, 0.0f );
		trans.anchorMax = new Vector2( 1.0f, 1.0f );
		trans.offsetMin = Vector2.zero;
		trans.offsetMax = Vector2.zero;
		trans.pivot = new Vector2( 0.5f, 0.5f );
		trans.anchoredPosition = Vector2.zero;
		trans.localScale = Vector3.one;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		Undo.RegisterCreatedObjectUndo( newGameObject, "Create Button Highlight Object" );

		return imageComponent;
	}

	void ConfigureIconParent ()
	{
		if( !useIcon || targ.ButtonIcon == null )
			return;

		if( useIconMask && buttonIconMask != null )
		{
			Undo.SetTransformParent( targ.ButtonIcon.transform, buttonIconMask.transform, "Enable Icon Mask" );
			buttonIconMask.transform.SetAsFirstSibling();
		}
		else
		{
			Undo.SetTransformParent( targ.ButtonIcon.transform, targ.ButtonBase.transform, "Disable Icon Mask" );
			targ.ButtonIcon.transform.SetAsFirstSibling();
		}
	}

	Image CreateIconImage ( Transform parent )
	{
		GameObject newGameObject = new GameObject();
		RectTransform trans = newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image imageComponent = newGameObject.AddComponent<Image>();

		if( iconSprite != null )
		{
			imageComponent.color = iconColor;
			imageComponent.sprite = iconSprite;
		}
		else
			imageComponent.color = new Color( 1.0f, 1.0f, 1.0f, 0.0f );

		newGameObject.transform.SetParent( parent );
		newGameObject.transform.SetAsFirstSibling();

		trans.anchorMin = new Vector2( 0.0f, 0.0f );
		trans.anchorMax = new Vector2( 1.0f, 1.0f );
		trans.offsetMin = Vector2.zero;
		trans.offsetMax = Vector2.zero;
		trans.localScale = Vector3.one * iconScale;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		newGameObject.name = "Button Icon";

		Undo.RegisterCreatedObjectUndo( newGameObject, "Create Icon Image Object" );

		return imageComponent;
	}

	Image CreateCooldownImage ( Transform parent )
	{
		GameObject newGameObject = new GameObject();
		RectTransform trans = newGameObject.AddComponent<RectTransform>();
		newGameObject.AddComponent<CanvasRenderer>();
		Image imageComponent = newGameObject.AddComponent<Image>();

		imageComponent.color = cooldownColor;
		if( cooldownSprite != null )
			imageComponent.sprite = cooldownSprite;

		imageComponent.type = Image.Type.Filled;
		imageComponent.fillMethod = fillMethod;
		imageComponent.fillAmount = cooldownTestValue;

		newGameObject.transform.SetParent( parent );
		newGameObject.transform.SetAsLastSibling();

		trans.anchorMin = new Vector2( 0.0f, 0.0f );
		trans.anchorMax = new Vector2( 1.0f, 1.0f );
		trans.offsetMin = Vector2.zero;
		trans.offsetMax = Vector2.zero;
		trans.localScale = Vector3.one * cooldownImageScale;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		newGameObject.name = "Cooldown Image";

		Undo.RegisterCreatedObjectUndo( newGameObject, "Create Cooldown Image Object" );

		return imageComponent;
	}

	Text CreateCooldownText ( Transform parent )
	{
		GameObject newTextObject = new GameObject();
		RectTransform trans = newTextObject.AddComponent<RectTransform>();
		newTextObject.AddComponent<CanvasRenderer>();
		Text textComponent = newTextObject.AddComponent<Text>();

		newTextObject.transform.SetParent( parent );
		newTextObject.gameObject.name = "Cooldown Text";

		trans.anchorMin = new Vector2( 0.0f, 0.25f );
		trans.anchorMax = new Vector2( 1.0f, 0.75f );
		trans.offsetMin = Vector2.zero;
		trans.offsetMax = Vector2.zero;
		trans.localScale = Vector3.one;
		trans.localPosition = Vector3.zero;
		trans.localRotation = Quaternion.identity;

		textComponent.text = "00";
		textComponent.alignment = TextAnchor.MiddleCenter;
		textComponent.alignByGeometry = true;
		textComponent.resizeTextForBestFit = true;
		textComponent.resizeTextMinSize = 0;
		textComponent.resizeTextMaxSize = 300;
		textComponent.color = textColor;
		textComponent.raycastTarget = false;

		if( textFont != null )
			textComponent.font = textFont;

		if( textOutline )
		{
			Outline outline = newTextObject.AddComponent<Outline>();
			outline.effectColor = textOutlineColor;
		}

		Undo.RegisterCreatedObjectUndo( newTextObject, "Create Cooldown Text Object" );

		return textComponent;
	}

	// ----------------------< EDITOR GUI HELPER FUNCTIONS >----------------------- //
	bool DisplayCollapsibleBoxSection ( string sectionTitle, string editorPref, bool error = false )
	{
		if( error )
			sectionTitle += " <color=#ff0000ff>*</color>";

		EditorGUILayout.BeginVertical( "Box" );

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Bold;

		if( GUILayout.Button( sectionTitle, collapsableSectionStyle ) )
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Normal;

		return EditorPrefs.GetBool( editorPref );
	}

	bool DisplayCollapsibleBoxSection ( string sectionTitle, string editorPref, SerializedProperty enabledProp, ref bool valueChanged, bool error = false )
	{
		valueChanged = false;

		if( error )
			sectionTitle += " <color=#ff0000ff>*</color>";

		EditorGUILayout.BeginVertical( "Box" );

		if( EditorPrefs.GetBool( editorPref ) && enabledProp.boolValue )
			collapsableSectionStyle.fontStyle = FontStyle.Bold;

		EditorGUILayout.BeginHorizontal();

		EditorGUI.BeginChangeCheck();
		enabledProp.boolValue = EditorGUILayout.Toggle( enabledProp.boolValue, GUILayout.Width( 25 ) );
		if( EditorGUI.EndChangeCheck() )
		{
			serializedObject.ApplyModifiedProperties();

			if( enabledProp.boolValue )
				EditorPrefs.SetBool( editorPref, true );
			else
				EditorPrefs.SetBool( editorPref, false );

			valueChanged = true;
			isDirty = true;
		}

		GUILayout.Space( -25 );

		EditorGUI.BeginDisabledGroup( !enabledProp.boolValue );
		if( GUILayout.Button( sectionTitle, collapsableSectionStyle ) )
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.EndHorizontal();

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Normal;

		return EditorPrefs.GetBool( editorPref ) && enabledProp.boolValue;
	}

	bool DisplayCollapsibleBoxSection ( string sectionTitle, string editorPref, ref bool enabledProp, ref bool valueChanged, bool error = false )
	{
		valueChanged = false;

		if( error )
			sectionTitle += " <color=#ff0000ff>*</color>";

		EditorGUILayout.BeginVertical( "Box" );

		if( EditorPrefs.GetBool( editorPref ) && enabledProp )
			collapsableSectionStyle.fontStyle = FontStyle.Bold;

		EditorGUILayout.BeginHorizontal();

		EditorGUI.BeginChangeCheck();
		enabledProp = EditorGUILayout.Toggle( enabledProp, GUILayout.Width( 25 ) );
		if( EditorGUI.EndChangeCheck() )
		{
			if( enabledProp )
				EditorPrefs.SetBool( editorPref, true );
			else
				EditorPrefs.SetBool( editorPref, false );

			valueChanged = true;
			isDirty = true;
		}

		GUILayout.Space( -25 );

		EditorGUI.BeginDisabledGroup( !enabledProp );
		if( GUILayout.Button( sectionTitle, collapsableSectionStyle ) )
			EditorPrefs.SetBool( editorPref, !EditorPrefs.GetBool( editorPref ) );
		EditorGUI.EndDisabledGroup();

		EditorGUILayout.EndHorizontal();

		if( EditorPrefs.GetBool( editorPref ) )
			collapsableSectionStyle.fontStyle = FontStyle.Normal;

		return EditorPrefs.GetBool( editorPref ) && enabledProp;
	}

	void EndCollapsibleBoxSection ( string editorPref, bool sectionEnabled = true )
	{
		if( EditorPrefs.GetBool( editorPref ) )
			GUILayout.Space( 1 );
		else if( sectionEnabled && DragAndDropHover )
			EditorPrefs.SetBool( editorPref, true );

		EditorGUILayout.EndVertical();
	}

	bool IsInProjectWindowWarning
	{
		get
		{
			if( !isInProjectWindow )
				return false;

			EditorGUILayout.HelpBox( "Please drag this prefab into the scene and create all the needed objects and then apply the changes to the prefab before continuing.", MessageType.Warning );
			return true;
		}
	}

	void OnSceneGUI ()
	{
		if( targ == null || Selection.activeGameObject == null || Application.isPlaying || parentCanvas == null )
			return;

		if( targ.BaseTransform == null || targ.ButtonBase == null )
			return;

		if( anchor.enumValueIndex >= 2 && targ.RelativeTransform == null )
			return;

		Handles.color = colorDefault;

		// MULTI-BUTTON GIZMOS //
		for( int i = 0; i < Selection.objects.Length; i++ )
		{
			GameObject selectedObject = ( GameObject )Selection.objects[ i ];
			UltimateButton btn = selectedObject.GetComponent<UltimateButton>();
			if( btn == null )
				continue;

			// BOUNDARY //
			Handles.color = colorDefault;
			if( boundary.enumValueIndex == 1 )
				DrawWireBox( selectedObject.GetComponent<UltimateButton>().BaseTransform );
			else
				DrawWireCircle( selectedObject.GetComponent<UltimateButton>().BaseTransform );

			if( targ.RelativeTransform != null )
			{
				// CENTER ANGLE //
				Vector3 lineEnd = targ.RelativeTransform.position;
				lineEnd.x -= ( Mathf.Cos( ( -multiCenterAngle - 90 ) * Mathf.Deg2Rad ) * ( targ.RelativeTransform.sizeDelta.x * ( orbitDistance.floatValue ) ) );
				lineEnd.y -= ( Mathf.Sin( ( -multiCenterAngle - 90 ) * Mathf.Deg2Rad ) * ( targ.RelativeTransform.sizeDelta.x * ( orbitDistance.floatValue ) ) );
				Vector3 heading = lineEnd - targ.RelativeTransform.position;
				heading = heading / heading.magnitude;

				Handles.color = colorDefault;
				if( DisplayCenterAngle )
					Handles.color = colorValueChanged;

				Handles.DrawLine( targ.RelativeTransform.position, targ.RelativeTransform.position + ( targ.BaseTransform.TransformDirection( heading * targ.RelativeTransform.sizeDelta ) * orbitDistance.floatValue ) * parentCanvas.transform.localScale.x );
			}

			// COOLDOWN TEXT //
			if( EditorPrefs.GetBool( "UB_CooldownTextSettings" ) )
			{
				if( DisplayCooldownTextAnchor && useCooldownText.boolValue && btn.CooldownText != null )
				{
					btn.CooldownText.rectTransform.localScale = Vector3.one;
					Handles.color = colorDefault;
					DrawWireBox( btn.CooldownText.rectTransform );
				}
			}
		}

		Handles.color = colorDefault;

		// CUSTOM EDITOR POSITIONING //
		if( Selection.objects.Length == 1 && anchor.enumValueIndex < 2 )
		{
			Event e = Event.current;

			if( e.type == EventType.MouseDown )
			{
				UltimateButton.CustomEditorPositioning = true;
				currentButtonSize = targ.GetComponent<RectTransform>().sizeDelta;
			}
			else if( e.type == EventType.MouseDrag && UltimateButton.CustomEditorPositioning )
			{
				if( targ.GetComponent<RectTransform>().sizeDelta != currentButtonSize )
				{
					UltimateButton.CustomEditorPositioning = false;
					targ.UpdatePositioning();
				}
			}
			else if( e.type == EventType.MouseUp && UltimateButton.CustomEditorPositioning )
			{
				UltimateButton.CustomEditorPositioning = false;
				Vector2 buttonLocalPosition = ( Vector2 )parentCanvas.transform.InverseTransformPoint( targ.ButtonBase.rectTransform.position ) + ( parentCanvas.GetComponent<RectTransform>().sizeDelta / 2 );

				buttonLocalPosition.x = ( ( buttonLocalPosition.x - ( targ.ButtonBase.rectTransform.sizeDelta.x / 2 ) ) / ( parentCanvas.GetComponent<RectTransform>().sizeDelta.x - targ.ButtonBase.rectTransform.sizeDelta.x ) ) * 100;
				buttonLocalPosition.y = ( ( buttonLocalPosition.y - ( targ.ButtonBase.rectTransform.sizeDelta.y / 2 ) ) / ( parentCanvas.GetComponent<RectTransform>().sizeDelta.y - targ.ButtonBase.rectTransform.sizeDelta.y ) ) * 100;

				if( anchor.enumValueIndex == 1 )
					buttonLocalPosition.x = -( buttonLocalPosition.x - 100 );

				SerializedObject btnObj = new SerializedObject( target );
				btnObj.FindProperty( "positionHorizontal" ).floatValue = Mathf.Clamp( buttonLocalPosition.x, 0, 100 );
				btnObj.FindProperty( "positionVertical" ).floatValue = Mathf.Clamp( buttonLocalPosition.y, 0, 100 );
				btnObj.ApplyModifiedProperties();
				Undo.RecordObject( targ, "Custom Editor Positioning" );
				targ.UpdatePositioning();
			}
		}

		if( EditorPrefs.GetBool( "UB_ButtonPositioning" ) )
		{
			if( anchor.enumValueIndex >= 2 && relativeTransform.objectReferenceValue != null )
			{
				DrawWireBox( targ.RelativeTransform, targ.RelativeTransform.sizeDelta.x );

				if( anchor.enumValueIndex == 3 )
				{
					// Orbit Distance
					Handles.color = colorDefault;
					if( DisplayOrbitRadius )
						Handles.color = colorValueChanged;

					Quaternion rot = Quaternion.AngleAxis( ( ( -centerAngle.floatValue + ( 360 / 2 ) ) - 90 ) - 360, targ.transform.forward );
					Vector3 lDirection = rot * -targ.transform.right;
					Handles.DrawWireArc( targ.RelativeTransform.position, targ.transform.forward, lDirection, 360f, ( targ.RelativeTransform.sizeDelta.x * orbitDistance.floatValue ) * parentCanvas.transform.localScale.x );
				}
			}
		}
	}

	void DrawWireBox ( RectTransform trans, float radius = 1.0f )
	{
		Vector2 center = trans.rect.center + ( ( trans.rect.size * radius ) * ( trans.pivot - new Vector2( 0.5f, 0.5f ) ) );
		
		Vector3 topLeft = center + ( new Vector2( trans.rect.xMin, trans.rect.yMax ) * radius );
		Vector3 topRight = center + ( new Vector2( trans.rect.xMax, trans.rect.yMax ) * radius );
		Vector3 bottomLeft = center + ( new Vector2( trans.rect.xMin, trans.rect.yMin ) * radius );
		Vector3 bottomRight = center + ( new Vector2( trans.rect.xMax, trans.rect.yMin ) * radius );

		topLeft = trans.TransformPoint( topLeft );
		topRight = trans.TransformPoint( topRight );
		bottomRight = trans.TransformPoint( bottomRight );
		bottomLeft = trans.TransformPoint( bottomLeft );

		Handles.DrawLine( topLeft, topRight );
		Handles.DrawLine( topRight, bottomRight );
		Handles.DrawLine( bottomRight, bottomLeft );
		Handles.DrawLine( bottomLeft, topLeft );
	}

	void DrawWireCircle ( RectTransform trans, float radius = 1.0f )
	{
		Handles.DrawWireDisc( trans.position, trans.forward, ( trans.sizeDelta.x / 2 ) * radius );
	}
	// ---------------------------------< SCENE GIZMOS >--------------------------------- //

	// ---------------------------------< CANVAS CREATOR FUNCTIONS >--------------------------------- //
	static void CreateNewCanvas ( GameObject child )
	{
		GameObject canvasObject = new GameObject( "Canvas" );
		canvasObject.layer = LayerMask.NameToLayer( "UI" );
		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvasObject.AddComponent<GraphicRaycaster>();
		canvasObject.AddComponent<CanvasScaler>();
		Undo.RegisterCreatedObjectUndo( canvasObject, "Create Ultimate Button" );
		Undo.SetTransformParent( child.transform, canvasObject.transform, "Create Ultimate Button" );
		CreateEventSystem();
	}

	static void CreateEventSystem ()
	{
#if UNITY_2022_2_OR_NEWER
		EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
#else
		EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif
		if( eventSystem == null )
		{
			GameObject newEventSystemObject = new GameObject( "EventSystem", typeof( EventSystem ) );
#if ENABLE_INPUT_SYSTEM
			newEventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
			newEventSystemObject.AddComponent<StandaloneInputModule>();
#endif
			Undo.RegisterCreatedObjectUndo( newEventSystemObject, "Create Joystick" );
		}
#if ENABLE_INPUT_SYSTEM
		else if( eventSystem.gameObject.GetComponent<StandaloneInputModule>() )
		{
			DestroyImmediate( eventSystem.gameObject.GetComponent<StandaloneInputModule>() );
			eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
		}
#endif
	}

	public static void RequestCanvas ( GameObject child )
	{
#if UNITY_2022_2_OR_NEWER
		UnityEngine.Canvas[] allCanvas = ( UnityEngine.Canvas[] )FindObjectsByType<UnityEngine.Canvas>( FindObjectsSortMode.None );
#else
		UnityEngine.Canvas[] allCanvas = ( UnityEngine.Canvas[] )FindObjectsOfType<UnityEngine.Canvas>();
#endif

		for( int i = 0; i < allCanvas.Length; i++ )
		{
			if( allCanvas[ i ].enabled == true && allCanvas[ i ].renderMode != RenderMode.WorldSpace )
			{
				Undo.SetTransformParent( child.transform, allCanvas[ i ].transform, "Create Joystick" );
				CreateEventSystem();
				return;
			}
		}
		CreateNewCanvas( child );
	}
	// -------------------------------< END CANVAS CREATOR FUNCTIONS >------------------------------- //
}