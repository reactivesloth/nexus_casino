/* Written by Kaz Crowe */
/* UltimateButton.cs */
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using System.Collections.Generic;
using System.Linq;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;
#endif

[ExecuteInEditMode]
#if ENABLE_INPUT_SYSTEM
public class UltimateButton : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
#else
public class UltimateButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
#endif
{
	// INTERNAL CALCULATIONS //
	/// <summary>
	/// The parent Canvas component that this Ultimate Button is inside of.
	/// </summary>
	public Canvas ParentCanvas { get; private set; }
	/// <summary>
	/// The RectTransform associated with the parent canvas.
	/// </summary>
	public RectTransform ParentCanvasTransform { get; private set; }
	/// <summary>
	/// The Graphic Raycaster component attached to the parent canvas.
	/// </summary>
	public GraphicRaycaster ParentCanvasRaycaster { get; private set; }
	Vector2 parentCanvasSize = Vector2.zero;
	/// <summary>
	/// The base RectTransform component of this Ultimate Button.
	/// </summary>
	public RectTransform BaseTransform { get; private set; }
	/// <summary>
	/// The current state of input being active on this Ultimate Button.
	/// </summary>
	public bool InputActive { get; private set; }
	bool interactable = true;
	/// <summary>
	/// Determines if the button is interactable or not.
	/// </summary>
	public bool Interactable
	{
		get => interactable;
		set
		{
			ResetButton();
			interactable = value;
		}
	}
	int interactPointerId = -10;
	bool getButtonDown, getButtonUp;
	Rect buttonRect;

	// --------------- < BUTTON SETTINGS > --------------- //
	[SerializeField] [Tooltip( "The base image of the button." )]
	private Image buttonBase;
	/// <summary>
	/// The Image used as the base of the button.
	/// </summary>
	public Image ButtonBase { get => buttonBase; }
	[SerializeField] [Tooltip( "The overall size of the button." )] [Range( 1.0f, 5.0f )]
	private float buttonSize = 1.75f;
	[SerializeField] [Tooltip( "The horizontal position of the button on the screen." )] [Range( 0.0f, 100.0f )]
	private float positionHorizontal = 5.0f;
	[SerializeField] [Tooltip( "The vertical position of the button on the screen." )] [Range( 0.0f, 100.0f )]
	private float positionVertical = 20.0f;
	[SerializeField] [Tooltip( "The size of the area in which the button can be initiated." )] [Range( 0.0f, 2.0f )]
	private float activationRange = 1.0f;
	enum Anchor { Left, Right, RelativeToTransform, OrbitTransform }
	[SerializeField] [Tooltip( "Determines what the button should be anchored to." )]
	private Anchor anchor = Anchor.Right;
	[SerializeField] [Tooltip( "The RectTransform that the button will position itself relative to." )]
	private RectTransform relativeTransform;
	/// <summary>
	/// The RectTransform that this button is positioned relative to.
	/// </summary>
	public RectTransform RelativeTransform { get => relativeTransform; }
	Vector2 relativeTransformSize, relativeTransformPosition;
	[SerializeField] [Tooltip( "The center angle for the button to orbit around the relative transform." )] [Range( -180.0f, 180.0f )]
	private float centerAngle = 0.0f;
	[SerializeField] [Tooltip( "The distance for the button to orbit around the transform." )] [Range( 0.0f, 2.0f )]
	private float orbitDistance = 1.0f;
	// INPUT SETTINGS //
	enum InputHandling { EventSystem, TouchInputExclusive }
	[SerializeField] [Tooltip( "Determines how the input is handled for this button, whether by the EventSystem or by directly calculating the touch input on the screen." )]
	private InputHandling inputHandling = InputHandling.EventSystem;
	private enum Boundary
	{
		Circular,
		Square
	}
	[SerializeField] [Tooltip( "Determines whether the boundary should be circular or square. This option affects how the Activation Range and Track Input options function." )]
	private Boundary boundary = Boundary.Circular;
	[SerializeField] [Tooltip( "Should the button track the users input to ensure that button events and states are only called when the input is within the Activation Range?" )]
	private bool trackInput = false;
	[SerializeField] [Tooltip( "The time in seconds that tap calculations will check for." )] [FormerlySerializedAs( "tapCountDuration" )]
	private float tapDecayRate = 0.5f;
	float currentTapTime = 0.0f;
	int tapCount = 0;
	/// <summary>
	/// This callback notifies any subscribers the tap count any time a touch is initiated on the button within the tap decay rate.
	/// </summary>
	public event Action<int> OnTapAchieved;
	/// <summary>
	/// This callback will be called only if the button has been released within the tap decay time.
	/// </summary>
	public event Action OnTapReleased;
	[SerializeField] [Tooltip( "Should the button attempt to transmit event data to any graphics that are positioned below it?" )] [FormerlySerializedAs( "transmitInput" )]
	private bool transmitEventData = false;
	IDragHandler dragHandler;
	IPointerUpHandler pointerUpHandler;
	GameObject transmittedObject;
	bool eventDataCalculated = false;
	// Override Positioning //
	[SerializeField] [Tooltip( "Determines if the button should be able to move and resize at runtime, or if the values set in the editor should be the only ones used." )]
	private bool overridePositioning = false;
	/// <summary>
	/// Returns true if the player is currently overriding and adjusting the position of this Ultimate Button.
	/// </summary>
	public bool IsOverridingPosition{ get; private set; }
	Vector2 inputRelativePosition = Vector2.zero;
	[SerializeField] [Tooltip( "The minimum position constraint to apply to the override positioning." )]
	private Vector2 repositionConstraintMin = new Vector2( 0, 0 );
	[SerializeField] [Tooltip( "The maximum position constraint to apply to the override positioning." )]
	private Vector2 repositionConstraintMax = new Vector2( 100, 100 );
	[SerializeField] [Tooltip( "Should the constraint be applied during the override? This will stop the position at the min and max values of the constraint so that it cannot exceed it." )]
	private bool applyConstraintDuringOverride = false;
	[SerializeField] [Tooltip( "[INTERNAL] Do not adjust." )] [HideInInspector]
	private string uniqueId;
	[SerializeField] [Tooltip( "Should the size and position override values be stored as PlayerPrefs local to each machine?" )]
	private bool saveAsPlayerPrefs = true;
	Vector2 buttonPositionOverride = new Vector2( -1, -1 );
	float buttonSizeOverride = -1;
	/// <summary>
	/// This callback is called any time the position or size is overridden and provides the listener with the new Vector2 position value and float size value.
	/// </summary>
	public event Action<Vector2, float> OnOverridePositioning;

	// --------------- < OPTIONAL SETTINGS > --------------- //
	// Input Transition //
	[SerializeField] [Tooltip( "Transitions between the different input states for more visual feedback." )]
	private bool inputTransition = false;
	[SerializeField] [Tooltip( "Time in seconds to transition to the default state." )]
	private float transitionUntouchedDuration = 0.1f;
	[SerializeField] [Tooltip( "Time in seconds to transition to the interacted state." )]
	private float transitionTouchedDuration = 0.1f;
	float fadeInSpeed, fadeOutSpeed, scaleInSpeed, scaleOutSpeed;
	// Tension //
	[SerializeField] [Tooltip( "Determines if a specific image should be used to display the input state of the button." )]
	private bool useTension = false;
	[SerializeField] [Tooltip( "The image component to be used for the button tension accent." )]
	private Image tensionAccent;
	/// <summary>
	/// The image component used as the tension accent for the button.
	/// </summary>
	public Image TensionAccent { get => tensionAccent; }
	[SerializeField] [Tooltip( "The Color of the Tension with no input." )] [FormerlySerializedAs( "tensionColorNone" )]
	private Color tensionColorDefault = Color.white;
	/// <summary>
	/// The default color of the tension accent image.
	/// </summary>
	public Color TensionColorDefault
	{
		get
		{
			if( tensionAccent == null )
			{
				Debug.LogError( FormatDebug( "The Tension Accent image is unassigned", "Please ensure that the Tension Accent image is assigned before attempting to modify the tension colors", gameObject.name ) );
				return Color.white;
			}
			return tensionColorDefault;
		}
		set
		{
			if( tensionAccent == null )
			{
				Debug.LogError( FormatDebug( "The Tension Accent image is unassigned", "Please ensure that the Tension Accent image is assigned before attempting to modify the tension colors", gameObject.name ) );
				return;
			}
			tensionColorDefault = value;
		}
	}
	[SerializeField] [Tooltip( "The Color of the Tension when the input is active." )] [FormerlySerializedAs( "tensionColorFull" )]
	private Color tensionColorActive = Color.white;
	/// <summary>
	/// The active color of the tension accent image.
	/// </summary>
	public Color TensionColorActive
	{
		get
		{
			if( tensionAccent == null )
			{
				Debug.LogError( FormatDebug( "The Tension Accent image is unassigned", "Please ensure that the Tension Accent image is assigned before attempting to modify the tension colors", gameObject.name ) );
				return Color.white;
			}
			return tensionColorActive;
		}
		set
		{
			if( tensionAccent == null )
			{
				Debug.LogError( FormatDebug( "The Tension Accent image is unassigned", "Please ensure that the Tension Accent image is assigned before attempting to modify the tension colors", gameObject.name ) );
				return;
			}
			tensionColorActive = value;
		}
	}
	Color tensionColorStart = Color.white;
	// Fade //
	[SerializeField] [Tooltip( "Should the button fade alpha when being interacted with?" )]
	private bool useFade = false;
	CanvasGroup canvasGroup;
	[SerializeField] [Tooltip( "The alpha to apply by default." )] [Range( 0.0f, 1.0f )]
	private float fadeUntouched = 1.0f;
	[SerializeField] [Tooltip( "The alpha to apply to the button when it is interacted with." )] [Range( 0.0f, 1.0f )]
	private float fadeTouched = 0.5f;
	// Scale //
	[SerializeField] [Tooltip( "Should the button scale when interacting?" )]
	private bool useScale = false;
	[SerializeField] [Tooltip( "The scale value to apply to the button when it is pressed." )] [Range ( 0.0f, 2.0f )]
	private float scaleTouched = 0.9f;
	// Highlight //
	[SerializeField] [Tooltip( "Should this Ultimate Button use a separate image as a highlight?" )]
	private bool useHighlight = false;
	[SerializeField] [Tooltip( "The highlight image to use." )]
	private Image buttonHighlight;
	/// <summary>
	/// The image associated with the button highlight.
	/// </summary>
	public Image ButtonHighlight { get => buttonHighlight; }
	/// <summary>
	/// The color of the highlight image.
	/// </summary>
	public Color HighlightColor
	{
		get
		{
			if( buttonHighlight == null )
				return Color.white;

			return buttonHighlight.color;
		}
		set
		{
			if( !useHighlight )
			{
				Debug.LogWarning( FormatDebug( "You are attempting to update the highlight color, but the Highlight option has not been enabled", "Please exit play mode and enable the Highlight option on this Ultimate Button", gameObject.name ) );
				return;
			}

			if( buttonHighlight == null )
				return;

			buttonHighlight.color = value;
		}
	}
	// Cooldown //
	[SerializeField] [Tooltip( "Should this Ultimate Button use a cooldown?" )]
	private bool useCooldown = false;
	[SerializeField] [Tooltip( "The image component to be used for the button cooldown." )] [FormerlySerializedAs( "buttonCooldown" )]
	private Image cooldownImage;
	/// <summary>
	/// The image used to display the cooldown of the button.
	/// </summary>
	public Image CooldownImage { get => cooldownImage; }
	[SerializeField] [Tooltip( "Determines if this button should display text for the cooldown." )]
	private bool useCooldownText = false;
	[SerializeField] [Tooltip( "Determines if the cooldown time should display the decimal point." )]
	private bool displayDecimalCooldown = false;
	[SerializeField] [Tooltip( "The scale curve of the text while processing the cooldown." )]
	private AnimationCurve cooldownTextScaleCurve = new AnimationCurve( new Keyframe[ 2 ] { new Keyframe( 0.0f, 1.0f ), new Keyframe( 1.0f, 1.0f ) } );
	[SerializeField] [Tooltip( "The text component to be used for the button cooldown." )]
	private Text cooldownText;
	/// <summary>
	/// The text component used to display the cooldown time.
	/// </summary>
	public Text CooldownText { get => cooldownText; }
	/// <summary>
	/// The current state of the button being considered "in cooldown" or not.
	/// </summary>
	public bool InCooldown
	{
		get;
		private set;
	}
	// Icon //
	[SerializeField] [Tooltip( "The image component to be used for the button icon." )]
	private Image buttonIcon;
	/// <summary>
	/// The image component used as the button icon
	/// </summary>
	public Image ButtonIcon { get => buttonIcon; }
	/// <summary>
	/// The color of the button icon.
	/// </summary>
	public Color ButtonIconColor
	{
		get
		{
			if( buttonIcon == null )
				return Color.white;
			return buttonIcon.color;
		}
		set
		{
			if( buttonIcon == null )
			{
				Debug.Log( FormatDebug( "The button icon component is not assigned", "Please ensure that the button icon has been created before attempting to adjust the color of the image", gameObject.name ) );
				return;
			}

			buttonIcon.color = value;
		}
	}

	// SCRIPT REFERENCE //
	[SerializeField] [Tooltip( "The name value for referencing this specific Ultimate Button." )]
	private string buttonName;
	static Dictionary<string, UltimateButton> UltimateButtons = new Dictionary<string, UltimateButton>();
#if ENABLE_INPUT_SYSTEM
	[InputControl( layout = "Button" )] [SerializeField]
	private string _controlPath;
	protected override string controlPathInternal
	{
		get => _controlPath;
		set => _controlPath = value;
	}
#endif

	// BUTTON EVENTS //
	public UnityEvent onButtonDown, onButtonUp;

	// PUBLIC CALLBACKS //
	/// <summary>
	/// Called on the frame that catches the input down on the image.
	/// </summary>
	public event Action OnPointerDownCallback;
	/// <summary>
	/// Called when the input on the image moves.
	/// </summary>
	public event Action OnDragCallback;
	/// <summary>
	/// Called on the frame that the input is released.
	/// </summary>
	public event Action OnPointerUpCallback;
	/// <summary>
	/// Called when the positioning of this Ultimate Button is updated.
	/// </summary>
	public event Action OnUpdatePositioning;

#if UNITY_EDITOR
	public static bool CustomEditorPositioning = false;
#endif


	/// <summary>
	/// [INTERNAL] Called by Unity when this script instance is initialized on scene load.
	/// </summary>
	void Awake ()
	{
		// If the application is being run, then send this button name and states to the static dictionary for reference.
		if( Application.isPlaying && buttonName != string.Empty )
		{
			// If the dictionary already contains a Ultimate Button with this name, then remove the button.
			if( UltimateButtons.ContainsKey( buttonName ) )
				UltimateButtons.Remove( buttonName );
			
			// Add the button name and this Ultimate Button into the dictionary.
			UltimateButtons.Add( buttonName, GetComponent<UltimateButton>() );
		}
	}

	/// <summary>
	/// [INTERNAL] Called by Unity on the first frame when this script is enabled.
	/// </summary>
	void Start ()
	{
		// If there is no unique ID, or there is another button with the same ID (VERY unlikely), then get a new random ID.
#if UNITY_2022_2_OR_NEWER
		if( overridePositioning && saveAsPlayerPrefs && ( string.IsNullOrEmpty( uniqueId ) || !( FindObjectsByType<UltimateButton>( FindObjectsSortMode.None ).Count( x => x.uniqueId == uniqueId ) == 1 ) ) )
			uniqueId = Guid.NewGuid().ToString().Substring( 0, 8 );
#else
		if( overridePositioning && saveAsPlayerPrefs && ( string.IsNullOrEmpty( uniqueId ) || !( FindObjectsOfType<UltimateButton>().Count( x => x.uniqueId == uniqueId ) == 1 ) ) )
			uniqueId = Guid.NewGuid().ToString().Substring( 0, 8 );
#endif

#if ENABLE_INPUT_SYSTEM && UNITY_EDITOR
#if UNITY_2022_2_OR_NEWER
		EventSystem eventSystem = FindAnyObjectByType<EventSystem>();
#else
		EventSystem eventSystem = FindObjectOfType<EventSystem>();
#endif

		// If the user has the new Input System and there is still an old input system component on the event system...
		if( eventSystem != null && eventSystem.gameObject.GetComponent<StandaloneInputModule>() )
		{
			if( Application.isPlaying )
			{
				// Destroy the old component and add the new one so there will be no errors.
				DestroyImmediate( eventSystem.gameObject.GetComponent<StandaloneInputModule>() );
				eventSystem.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
			}
			else
			{
				UnityEditor.Undo.DestroyObjectImmediate( eventSystem.gameObject.GetComponent<StandaloneInputModule>() );
				UnityEditor.Undo.AddComponent( eventSystem.gameObject, typeof( UnityEngine.InputSystem.UI.InputSystemUIInputModule ) );
			}
		}
#endif
		// If the application is not running then return.
		if( !Application.isPlaying )
			return;

#if UNITY_EDITOR
		// If the user has the Enter Play Mode settings enabled, but does not have the scene reload, then call the Awake() function since it was not invoked on playing the application.
		if( UnityEditor.EditorSettings.enterPlayModeOptionsEnabled && UnityEditor.EditorSettings.enterPlayModeOptions.HasFlag( UnityEditor.EnterPlayModeOptions.DisableSceneReload ) )
			Awake();
#endif
		// If the parent canvas is null...
		if( ParentCanvas == null )
		{
			// Try to get the parent canvas component.
			OnTransformParentChanged();

			// If it is still null, then log a error and return.
			if( ParentCanvas == null )
			{
				Debug.LogError( FormatDebug( "This component is not within a Canvas object. Disabling this component to avoid any errors", "Please ensure that this object is placed within a Canvas in your scene", gameObject.name ) );
				enabled = false;
				return;
			}
		}

		// If the user wants to transition on different input...
		if( inputTransition )
		{
			// Try to store the canvas group.
			canvasGroup = GetComponent<CanvasGroup>();

			// If the canvas group is still null, then add a canvas group component.
			if( canvasGroup == null )
				canvasGroup = gameObject.AddComponent<CanvasGroup>();

			// Configure the transition speeds. The extra calculations are needed because of using the MoveTowards function.
			fadeInSpeed = ( 1.0f / transitionTouchedDuration ) * Mathf.Abs( fadeTouched - fadeUntouched );
			fadeOutSpeed = ( 1.0f / transitionUntouchedDuration ) * Mathf.Abs( fadeTouched - fadeUntouched );
			scaleInSpeed = 1.0f / transitionTouchedDuration * Mathf.Abs( 1.0f - scaleTouched );
			scaleOutSpeed = 1.0f / transitionUntouchedDuration * Mathf.Abs( 1.0f - scaleTouched );
		}
		
		// If the user is wanting to display cooldown on this button...
		if( useCooldown )
		{
			// If the cooldown image is assigned, then set the fill amount to zero.
			if( cooldownImage != null )
				cooldownImage.fillAmount = 0.0f;

			// If the user wants text and the text is assigned, then reset the text string.
			if( useCooldownText && cooldownText != null )
				cooldownText.text = "";
		}

		// Update the size and positioning of the button.
		UpdatePositioning();
	}

	/// <summary>
	/// [INTERNAL] Called by Unity every frame.
	/// </summary>
	void Update ()
	{
#if UNITY_EDITOR
		// The button will be updated constantly when the game is not being run.
		if( !Application.isPlaying )
		{
			if( !CustomEditorPositioning )
				UpdatePositioning();
	
			return;
		}
#endif

		// If the stored canvas size is not the same as the current canvas size, then update positioning.
		if( parentCanvasSize != ParentCanvasTransform.sizeDelta )
			UpdatePositioning();

		// If the user wants the button positioned relative to another transform, and that transform is assigned...
		if( ( anchor == Anchor.RelativeToTransform || anchor == Anchor.OrbitTransform ) && relativeTransform != null )
		{
			// If the transform has changed at all...
			if( relativeTransformSize != relativeTransform.sizeDelta || relativeTransformPosition != ( Vector2 )relativeTransform.position )
			{
				// Store the new transform information.
				relativeTransformSize = relativeTransform.sizeDelta;
				relativeTransformPosition = relativeTransform.position;

				// Update the positioning.
				UpdatePositioning();
			}
		}

		// If the user wants the input to be handled exclusively by touch input, then process the touch input.
		if( inputHandling == InputHandling.TouchInputExclusive )
		{
#if ENABLE_INPUT_SYSTEM
			// If there are touches on the screen...
			if( UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count > 0 )
			{
				// Loop through each finger on the screen...
				for( int touchId = 0; touchId < UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count; touchId++ )
				{
					// If a finger id has been stored, and this finger id is not the same as the stored finger id, then continue.
					if( interactPointerId >= 0 && interactPointerId != UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[ touchId ].touchId )
						continue;
				
					Vector2 touchPosition = UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[ touchId ].screenPosition;

					if( UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[ touchId ].phase == UnityEngine.InputSystem.TouchPhase.Began )
						OnInputDown( touchPosition, UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[ touchId ].touchId );
					else if( UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches[ touchId ].phase == UnityEngine.InputSystem.TouchPhase.Ended )
						OnInputUp( touchPosition );
					else
						OnInputDrag( touchPosition );
				}
			}
			// Else reset the button.
			else if( interactPointerId >= 0 )
				ResetButton();
#if UNITY_EDITOR
			// If there are no touches and this code is being run in the editor, check for mouse input.
			if( UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0 )
			{
				// Store the mouse device.
				Mouse mouse = InputSystem.GetDevice<Mouse>();

				// If the mouse button is down this frame, call OnInputDown with -1 for pointer index. This will bypass the reset in the code above.
				if( mouse.leftButton.wasPressedThisFrame )
					OnInputDown( mouse.position.ReadValue(), -1 );
				// Else if the mouse button is released this frame, call OnInputUp().
				else if( mouse.leftButton.wasReleasedThisFrame )
					OnInputUp( mouse.position.ReadValue() );
				// Else if the input is active, call OnInputDrag().
				else if( InputActive )
					OnInputDrag( mouse.position.ReadValue() );
			}
#endif
#else
			// If there are touches on the screen...
			if( Input.touchCount > 0 )
			{
				// Loop through each finger on the screen...
				for( int fingerId = 0; fingerId < Input.touchCount; fingerId++ )
				{
					// If a finger id has been stored, and this finger id is not the same as the stored finger id, then continue.
					if( interactPointerId >= 0 && interactPointerId != Input.GetTouch( fingerId ).fingerId )
						continue;

					// If the touch phase has begun this frame, then call the OnInputDown function to check initial touch.
					if( Input.GetTouch( fingerId ).phase == TouchPhase.Began )
						OnInputDown( Input.GetTouch( fingerId ).position, Input.GetTouch( fingerId ).fingerId );
					// Else if the input has ended, call the OnInputUp function.
					else if( Input.GetTouch( fingerId ).phase == TouchPhase.Ended )
						OnInputUp( Input.GetTouch( fingerId ).position );
					// Else process the drag function.
					else
						OnInputDrag( Input.GetTouch( fingerId ).position );
				}
			}
			// Else reset the button.
			else if( interactPointerId >= 0 )
				ResetButton();
#if UNITY_EDITOR
			// If there are no touches and this code is being run in the editor, check for mouse input.
			if( Input.touchCount == 0 )
			{
				// If the mouse button is down this frame, call OnInputDown with -1 for pointer index. This will bypass the reset in the code above.
				if( Input.GetMouseButtonDown( 0 ) )
					OnInputDown( Input.mousePosition, -1 );
				// Else if the mouse button is released this frame, call OnInputUp().
				else if( Input.GetMouseButtonUp( 0 ) )
					OnInputUp( Input.mousePosition );
				// Else if the input is active, call OnInputDrag().
				else if( InputActive )
					OnInputDrag( Input.mousePosition );
			}
#endif
#endif
		}

		// If the user wants to transition the input...
		if( inputTransition )
		{
			// If the user wants to display a fade transition...
			if( useFade && canvasGroup != null )
			{
				// If the input is currently active and the alpha is not the set touched alpha value, then lerp it over time.
				if( InputActive && canvasGroup.alpha != fadeTouched )
					canvasGroup.alpha = Mathf.MoveTowards( canvasGroup.alpha, fadeTouched, fadeInSpeed * Time.deltaTime );
				// Else if the input is NOT currently active and the alpha isn't the untouched value, lerp to it over time.
				else if( !InputActive && canvasGroup.alpha != fadeUntouched )
					canvasGroup.alpha = Mathf.MoveTowards( canvasGroup.alpha, fadeUntouched, fadeOutSpeed * Time.deltaTime );
			}

			// If the user wants to scale the button over time...
			if( useScale && buttonBase != null )
			{
				// If the input is currently active and the scale is not the set touched value, then move the current scale towards that value.
				if( InputActive && buttonBase.rectTransform.localScale != Vector3.one * scaleTouched )
					buttonBase.rectTransform.localScale = Vector3.one * Mathf.MoveTowards( buttonBase.rectTransform.localScale.x, scaleTouched, scaleInSpeed * Time.deltaTime );
				// Else if the input is NOT active and the scale is not 1, then move the current scale towards 1.
				else if( !InputActive && buttonBase.rectTransform.localScale != Vector3.one )
					buttonBase.rectTransform.localScale = Vector3.one * Mathf.MoveTowards( buttonBase.rectTransform.localScale.x, 1.0f, scaleOutSpeed * Time.deltaTime );
			}

			// If the user wants to display tension on the button over time...
			if( useTension && tensionAccent != null )
			{
				// If the input is currently active and the color is not what the user has set as full yet, then move the values towards the full color.
				if( InputActive && tensionAccent.color != tensionColorActive )
					tensionAccent.color = Vector4.MoveTowards( tensionAccent.color, tensionColorActive, ( 1.0f / transitionTouchedDuration ) * Time.deltaTime );
				// Else if the input is NOT currently active and the color is not set to the none value yet, move towards it.
				else if( !InputActive && tensionAccent.color != tensionColorDefault )
					tensionAccent.color = Vector4.MoveTowards( tensionAccent.color, tensionColorDefault, ( 1.0f / transitionUntouchedDuration ) * Time.deltaTime );
			}
		}

		// If the current tap time is higher than zero then reduce the timer.
		if( currentTapTime > 0.0f )
			currentTapTime -= Time.deltaTime;
		// Else if the tap time is less than zero, then finalize the tap time and reset the tap count.
		else if( currentTapTime < 0.0f )
		{
			currentTapTime = 0.0f;
			tapCount = 0;
		}

		// If the input is active, notify any subscribers that the input is pressed.
		if( InputActive )
			OnDragCallback?.Invoke();
	}

	/// <summary>
	/// [INTERNAL] Called by Unity at the end of every frame.
	/// </summary>
	void LateUpdate ()
	{
		// Reset the 1 frame only input variables since this is the end of the frame.
		getButtonDown = false;
		getButtonUp = false;
		eventDataCalculated = false;
	}

	protected override void OnDisable()
	{
		base.OnDisable();
		ResetButton();
	}

	/// <summary>
	/// This function is called by Unity when the parent of this transform changes.
	/// </summary>
	void OnTransformParentChanged ()
	{
		// Store the parent of this object.
		Transform parent = transform.parent;

		// If the parent is null, then just return.
		if( parent == null )
			return;

		// While the parent is assigned...
		while( parent != null )
		{
			// If the parent object has a Canvas component, then assign the ParentCanvas and transform.
			if( parent.transform.GetComponent<Canvas>() )
			{
				ParentCanvas = parent.transform.GetComponent<Canvas>();
				ParentCanvasTransform = ParentCanvas.GetComponent<RectTransform>();
				ParentCanvasRaycaster = ParentCanvas.GetComponent<GraphicRaycaster>();
				return;
			}

			// If the parent does not have a canvas, then store it's parent to loop again.
			parent = parent.transform.parent;
		}
	}
	
	/// <summary>
	/// [INTERNAL] This function is called by Unity when the application receives focus again.
	/// </summary>
	void OnApplicationFocus ( bool focus )
	{
		if( !Application.isPlaying || !InputActive || !focus )
			return;

		ResetButton();
	}

	/// <summary>
	/// [INTERNAL] Called from Unity's EventSystem when the input is pressed down on this image.
	/// </summary>
	public void OnPointerDown ( PointerEventData eventData )
	{
		// If the user wants to calculate touch input exclusively to bypass the EventSystem, then just return.
		if( inputHandling != InputHandling.EventSystem )
			return;

		// Otherwise call the OnInputDown function with the information from the EventSystem.
		OnInputDown( eventData.position, eventData.pointerId );
	}

	void OnInputDown ( Vector2 inputPosition, int pointerId )
	{
		// If the button is currently in the repositioning state...
		if( IsOverridingPosition )
		{
			// If the render mode is set to Camera, then simply store the event data position.
			if( ParentCanvas.renderMode == RenderMode.ScreenSpaceCamera )
				inputRelativePosition = inputPosition;
			// Else calculate the input relative to the button base position.
			else
				inputRelativePosition = BaseTransform.InverseTransformPoint( inputPosition ) - buttonBase.rectTransform.localPosition;

			return;
		}

		// If the user wants to attempt to transmit data to other UI objects then send the input down events.
		if( transmitEventData || !IsInRange( inputPosition ) )
		{
			// If the event data has already been processed this frame, then return to avoid double calculations.
			if( eventDataCalculated )
				return;

			// Set the bool to true so that the button will not process the event again.
			eventDataCalculated = true;

			// Create a new PointerEventData to sent to any potential receivers.
			PointerEventData eventData = new PointerEventData( EventSystem.current );
			eventData.position = inputPosition;

			// Create a temporary list of raycast results to go through.
			List<RaycastResult> raycastResults = new List<RaycastResult>();

			// Raycast the event data and give it the temporary list.
			ParentCanvasRaycaster.Raycast( eventData, raycastResults );

			int thisObjectRaycastDepth = 0;

			// Loop through all the raycast results that were found...
			for( int i = 0; i < raycastResults.Count; i++ )
			{
				// If the gameObject of the result is null, then skip this index.
				if( raycastResults[ i ].gameObject == null )
					continue;

				// If the gameObject is THIS gameObject, then store the raycast depth and skip this index.
				if( raycastResults[ i ].gameObject == gameObject )
				{
					thisObjectRaycastDepth = raycastResults[ i ].depth;
					continue;
				}

				// If the depth of this raycast is greater than the stored depth (0 by default until this object is found) then skip this index.
				if( raycastResults[ i ].depth > thisObjectRaycastDepth )
					continue;

				// If any of the parents of the hit object are in this button then skip this index.
				if( raycastResults[ i ].gameObject.transform.parent == BaseTransform || raycastResults[ i ].gameObject.transform.parent == ButtonBase.transform )
					continue;

				// If the object has a pointer down handler, then inform the object of the event data.
				raycastResults[ i ].gameObject.GetComponent<IPointerDownHandler>()?.OnPointerDown( eventData );

				// Attempt to store the OnDrag() and OnPointerUp() handlers.
				dragHandler = raycastResults[ i ].gameObject.GetComponent<IDragHandler>();
				pointerUpHandler = raycastResults[ i ].gameObject.GetComponent<IPointerUpHandler>();

				// If an object has been found that can receive input...
				if( dragHandler != null || pointerUpHandler != null || raycastResults[ i ].gameObject.GetComponent<IPointerDownHandler>() != null )
				{
					// Store this object as the object that input was transmitted to. This is need for calling a OnClick() for needed Button objects.
					transmittedObject = raycastResults[ i ].gameObject;

					// Break the loop here since an object has been found. This will ensure that only the closest object below this UI object will be interacted with.
					break;
				}
			}
		}

		// If the button is already in use, or the user doesn't want the button interacted with, then return.
		if( InputActive || !interactable )
			return;

		// If the input is not in range, then attempt to send the current input data and return.
		if( !IsInRange( inputPosition ) )
			return;

		// If the user wants to display a cooldown, and this button is currently in cooldown, then return.
		if( useCooldown && InCooldown )
			return;

		// Set the input variables to true since the input has been pressed down on the button.
		InputActive = getButtonDown = true;
		interactPointerId = pointerId;

		// If the user wants the input transition to be instant...
		if( inputTransition && transitionTouchedDuration <= 0 )
		{
			// If the user wants tension to be displayed, then set the color to full tension.
			if( useTension && tensionAccent != null )
				tensionAccent.color = tensionColorActive;

			// If the user wants to fade the button alpha, then set the alpha value.
			if( useFade && canvasGroup != null )
				canvasGroup.alpha = fadeTouched;

			// If the user wants to scale the button, then set the scale.
			if( useScale )
				buttonBase.rectTransform.localScale = Vector3.one * scaleTouched;
		}

#if ENABLE_INPUT_SYSTEM
		// If the new Input System is being used, send the input value.
		SendValueToControl( 1.0f );
#endif
		// Increase the tap count and set the tap time for calculations.
		tapCount++;
		currentTapTime = tapDecayRate;

		// If the tap count is greater than this one, then inform any subscribers that there may be a tap event.
		if( tapCount > 1 )
			OnTapAchieved?.Invoke( tapCount );

		// If the down event is assigned, then call the event.
		if( onButtonDown != null )
			onButtonDown.Invoke();

		// Notify any subscribers that the OnPointerDown function has been called.
		OnPointerDownCallback?.Invoke();
	}

	/// <summary>
	/// [INTERNAL] Called from Unity's EventSystem when the input is being dragged.
	/// </summary>
	public void OnDrag ( PointerEventData eventData )
	{
		// If the user wants to calculate touch input exclusively to bypass the EventSystem, then just return.
		if( inputHandling != InputHandling.EventSystem )
			return;

		// Otherwise call the OnInputDrag function with the information from the EventSystem.
		OnInputDrag( eventData.position );
	}

	void OnInputDrag ( Vector2 inputPosition )
	{
		// If the button is currently in the repositioning state...
		if( IsOverridingPosition )
		{
			// If the button is in a camera render canvas, then apply the position of the difference in input and store the current input for the next calculation.
			if( ParentCanvas.renderMode == RenderMode.ScreenSpaceCamera )
			{
				buttonBase.rectTransform.localPosition += ( Vector3 )( inputPosition - inputRelativePosition ) / ParentCanvas.scaleFactor;
				inputRelativePosition = inputPosition;
			}
			// Else just apply the position of the difference in input relative to the base transform.
			else
				buttonBase.rectTransform.localPosition = ( Vector2 )BaseTransform.InverseTransformPoint( inputPosition ) - inputRelativePosition;

			// Update the position override values.
			UpdatePositioningOverride();

			// If the user wants to apply the constraint during the override, then apply it if needed.
			if( applyConstraintDuringOverride && ( buttonPositionOverride.x <= repositionConstraintMin.x || buttonPositionOverride.x >= repositionConstraintMax.x || buttonPositionOverride.y <= repositionConstraintMin.y || buttonPositionOverride.y >= repositionConstraintMax.y ) )
				UpdatePositioning();

			return;
		}

		// If the user wants to attempt to transmit data to other UI objects...
		if( transmitEventData || !InputActive )
		{
			// If the event data has already been processed this frame, then return to avoid double calculations.
			if( eventDataCalculated )
				return;

			// Set the bool to true so that the button will not process the event again.
			eventDataCalculated = true;

			// Create a new PointerEventData to sent to any potential receivers.
			PointerEventData eventData = new PointerEventData( EventSystem.current );
			eventData.position = inputPosition;

			// Inform the drag target that a drag event has been executed.
			dragHandler?.OnDrag( eventData );
		}

		// If the pointer event that is calling this function is not the same as the one that initiated the button, then return.
		if( !InputActive )
			return;

		// If the user wants to track the input, and the input is not within range of the button, then release the input.
		if( trackInput && !IsInRange( inputPosition ) )
			OnInputUp( inputPosition );
	}

	/// <summary>
	/// [INTERNAL] Called from Unity's EventSystem when the input has been released.
	/// </summary>
	public void OnPointerUp ( PointerEventData eventData )
	{
		// If the user wants to calculate touch input exclusively to bypass the EventSystem, then just return.
		if( inputHandling != InputHandling.EventSystem )
			return;

		// Otherwise call the OnInputUp function with the information from the EventSystem.
		OnInputUp( eventData.position );
	}

	void OnInputUp ( Vector2 inputPosition )
	{
		// If the button is currently in the repositioning state...
		if( IsOverridingPosition )
		{
			// Update the position values.
			UpdatePositioningOverride();
			UpdatePositioning();
			return;
		}

		// If the user wants to attempt to transmit data to other UI objects...
		if( transmitEventData || !InputActive )
		{
			// If the event data has already been processed this frame, then return to avoid double calculations.
			if( eventDataCalculated )
				return;

			// Set the bool to true so that the button will not process the event again.
			eventDataCalculated = true;

			// Create a new PointerEventData to sent to any potential receivers.
			PointerEventData eventData = new PointerEventData( EventSystem.current );
			eventData.position = inputPosition;

			// Inform the stored object of the event data.
			pointerUpHandler?.OnPointerUp( eventData );

			// Temporary list for the raycast results.
			List<RaycastResult> raycastResults = new List<RaycastResult>();

			// Raycast all objects with the event data and get a list of hit objects.
			ParentCanvasRaycaster.Raycast( eventData, raycastResults );

			// Loop through all the hit objects.
			for( int i = 0; i < raycastResults.Count; i++ )
			{
				// If the gameObject of the result is null or the raycast result object is not the same as the object stored then skip this index.
				if( raycastResults[ i ].gameObject == null || raycastResults[ i ].gameObject != transmittedObject )
					continue;

				// Otherwise attempt to inform a click handlers of this event.
				raycastResults[ i ].gameObject.GetComponent<IPointerClickHandler>()?.OnPointerClick( eventData );
			}

			// Clear the stored information.
			dragHandler = null;
			pointerUpHandler = null;
			transmittedObject = null;
		}

		// If the button has not been initialized properly, then return.
		if( !InputActive )
			return;

		// Set the current input state to false since the input has been released.
		InputActive = false;
		interactPointerId = -10;

		// Set getButtonUp to true since the input has been released.
		getButtonUp = true;

		// If the up event is assigned, then call the event.
		if( onButtonUp != null )
			onButtonUp.Invoke();

		// If the OnGetButtonUp action has any subscribers, then notify them.
		if( OnPointerUpCallback != null )
			OnPointerUpCallback.Invoke();

		// If the user wants an instant input transition...
		if( inputTransition && transitionUntouchedDuration <= 0 )
		{
			// If the users wants tension to be displayed, then reset the color.
			if( useTension && tensionAccent != null )
				tensionAccent.color = tensionColorDefault;

			// If the user wants to fade, then apply the default alpha value.
			if( useFade && canvasGroup != null )
				canvasGroup.alpha = fadeUntouched;

			// Reset the scale back to one if the user wants to scale.
			if( useScale )
				buttonBase.rectTransform.localScale = Vector3.one;
		}

#if ENABLE_INPUT_SYSTEM
		// If the new Input System is being used, send the input value.
		SendValueToControl( 0.0f );
#endif
		// If the current tap time is still active by the time this input was released, then notify any subscribers that the tap was released in time.
		if( currentTapTime > 0.0f )
			OnTapReleased?.Invoke();

		// Notify any subscribers that the OnPointerUp function has been called.
		OnPointerUpCallback?.Invoke();
	}

	/// <summary>
	/// Returns the current state of the input being in range of the button or not.
	/// </summary>
	/// <param name="inputPosition">The current position of the input for calculations.</param>
	bool IsInRange ( Vector2 inputPosition )
	{
		// If the user has a circular button image...
		if( boundary == Boundary.Circular )
		{
			// distance = distance between the world position of the button base cast to a local position of the ParentCanvas (* by scale factor) - half of the actual canvas size, and the input position.
			float distance = Vector2.Distance( ( Vector2 )( ParentCanvas.transform.InverseTransformPoint( buttonBase.rectTransform.position ) * ParentCanvas.scaleFactor ) + ( ( ParentCanvasTransform.sizeDelta * ParentCanvas.scaleFactor ) / 2 ), inputPosition );

			// If the distance is out of range, then just return.
			if( distance / ( BaseTransform.sizeDelta.x * ParentCanvas.scaleFactor ) > 0.5f )
				return false;
		}
		// Else the user has a square button...
		else
		{
			// If the rect of the button does not contain the input position, then return false.
			if( !buttonRect.Contains( inputPosition ) )
				return false;
		}

		return true;
	}

	/// <summary>
	/// Resets the button input information and stops any coroutines that might have been running.
	/// </summary>
	void ResetButton ()
	{
		// Set the buttons state to false.
		InputActive = getButtonDown = getButtonUp = false;
		interactPointerId = -10;

		// If the user wants to transition on input...
		if( inputTransition )
		{
			// If the user has tension enabled and the image is assigned, then reset it.
			if( useTension && tensionAccent != null )
				tensionAccent.color = tensionColorDefault;

			// If the user is using alpha fade, then reset the alpha.
			if( useFade && canvasGroup != null )
				canvasGroup.alpha = fadeUntouched;

			// If the user has scale selected, reset it.
			if( useScale )
				buttonBase.rectTransform.localScale = Vector3.one;
		}
	}

	/// <summary>
	/// [INTERNAL] Formats and sends detailed information to the user.
	/// </summary>
	static string FormatDebug ( string error, string solution, string objectName )
	{
		return "<b>Ultimate Button</b>\n" +
			"<color=red><b>×</b></color> <i><b>Error:</b></i> " + error + ".\n" +
			"<color=green><b>√</b></color> <i><b>Solution:</b></i> " + solution + ".\n" +
			"<color=blue><b>∙</b></color> <i><b>Object:</b></i> " + objectName + "\n";
	}

	// --------------------------------------------- *** PUBLIC FUNCTIONS FOR THE USER *** --------------------------------------------- //
	/// <summary>
	/// Updates the button positioning according to the provided screen position percentages.
	/// </summary>
	/// <param name="horizontalPosition">The target percentage horizontal position on the screen. (ex. 50 = 50% = center of the screen)</param>
	/// <param name="verticalPosition">The target percentage horizontal position on the screen. (ex. 50 = 50% = center of the screen)</param>
	/// <param name="size">[OPTIONAL] The target size of the button graphic. (Range 0.0f - 5.0f)</param>
	public void UpdatePositioning ( float horizontalPosition, float verticalPosition, float size = -1.0f )
	{
		// If the size value is assigned, then copy the value.
		if( size > 0.0f )
			buttonSize = Mathf.Clamp( size, 0.0f, 5.0f );

		// Clamp the provided values between 0 and 100.
		positionHorizontal = Mathf.Clamp( horizontalPosition, 0.0f, 100.0f );
		positionVertical = Mathf.Clamp( verticalPosition, 0.0f, 100.0f );

		// If the user wants to be able to override the positioning at runtime then store the updated position values.
		if( overridePositioning )
		{
			buttonSizeOverride = buttonSize;
			buttonPositionOverride = new Vector2( positionHorizontal, positionVertical );
		}

		// Update the positioning with the new values.
		UpdatePositioning();
	}

	/// <summary>
	/// Updates the size and placement of the Ultimate Button. Useful for when applying any options changed at runtime.
	/// </summary>
	public void UpdatePositioning ()
	{
		// If the parent canvas is null, then try to get the parent canvas component.
		if( ParentCanvas == null || ParentCanvasTransform == null || ParentCanvasRaycaster == null )
			OnTransformParentChanged();

		// If it is still null, then log a error and return.
		if( ParentCanvas == null )
		{
#if UNITY_EDITOR
			if( UnityEditor.Selection.activeGameObject != null && UnityEditor.Selection.activeGameObject.scene != null && UnityEditor.Selection.activeGameObject == gameObject )
				Debug.LogError( FormatDebug( "The Ultimate Button is not placed within a Canvas GameObject", "Please ensure that the Ultimate Button is placed within a UI Canvas", gameObject.name ) );
#endif
			return;
		}

		// If the buttonBase is left unassigned, then inform the user and return.
		if( buttonBase == null )
		{
			if( Application.isPlaying )
				Debug.LogError( FormatDebug( "The buttonBase variable has not been assigned", "Please make sure that this variable is assigned in the Button Positioning section", gameObject.name ) );

			return;
		}

		// If the game is running, then reset the button.
		if( Application.isPlaying )
			ResetButton();

		// Store the rect trans size. If the canvas size is ever different, this function will run since the position will need to be updated.
		parentCanvasSize = ParentCanvasTransform.sizeDelta;

		// Configure a size for the image based on the Canvas's size and scale.
		float textureSize = Mathf.Min( parentCanvasSize.y, parentCanvasSize.x ) * ( buttonSize / 10 );

		// If BaseTransform is null, store this object's RectTrans so that it can be positioned.
		if( BaseTransform == null )
			BaseTransform = GetComponent<RectTransform>();

		// Force the anchors and pivot so the button will function correctly. This is also needed here for older versions of the Ultimate Button that didn't use these rect transform settings.
		BaseTransform.anchorMin = Vector2.zero;
		BaseTransform.anchorMax = Vector2.zero;
		BaseTransform.pivot = new Vector2( 0.5f, 0.5f );
		BaseTransform.localScale = Vector3.one;
		BaseTransform.localRotation = Quaternion.identity;

		// Set the anchors of the button base. It is important to have the anchors centered for calculations.
		buttonBase.rectTransform.anchorMin = new Vector2( 0.5f, 0.5f );
		buttonBase.rectTransform.anchorMax = new Vector2( 0.5f, 0.5f );
		buttonBase.rectTransform.pivot = new Vector2( 0.5f, 0.5f );
		buttonBase.rectTransform.localRotation = Quaternion.identity;

		// Configure the position that the user wants the button to be located.
		Vector3 buttonPosition = new Vector2( parentCanvasSize.x * ( positionHorizontal / 100 ) - ( textureSize * ( positionHorizontal / 100 ) ) + ( textureSize / 2 ), parentCanvasSize.y * ( positionVertical / 100 ) - ( textureSize * ( positionVertical / 100 ) ) + ( textureSize / 2 ) ) - ( parentCanvasSize / 2 );

		// If the anchor is to the right, then make the horizontal button position negative.
		if( anchor == Anchor.Right )
			buttonPosition.x = -buttonPosition.x;
		// Else if the user wants the positioning to be relative to another transform, and the transform is assigned...
		else if( ( int )anchor >= 2 && relativeTransform != null )
		{
			// Store the relative transform information for updating the positioning if it changes.
			relativeTransformSize = relativeTransform.sizeDelta;
			relativeTransformPosition = relativeTransform.position;

			// Calculate the center position of the relative transform.
			buttonPosition = ParentCanvas.transform.InverseTransformPoint( relativeTransform.position ) - ( Vector3 )( relativeTransform.sizeDelta * ( relativeTransform.pivot - new Vector2( 0.5f, 0.5f ) ) );

			// Reconfigure the texture size and reference size based on the relative transform.
			textureSize = Mathf.Max( relativeTransform.sizeDelta.x, relativeTransform.sizeDelta.y ) * ( buttonSize / 5 );

			// If the user wants to orbit a transform...
			if( anchor == Anchor.OrbitTransform )
			{
				// Calculate the position of the button according to the user options.
				buttonPosition.x += ( Mathf.Cos( ( -centerAngle * Mathf.Deg2Rad ) + ( 90 * Mathf.Deg2Rad ) ) * ( relativeTransform.sizeDelta.x * orbitDistance ) );
				buttonPosition.y += ( Mathf.Sin( ( -centerAngle * Mathf.Deg2Rad ) + ( 90 * Mathf.Deg2Rad ) ) * ( relativeTransform.sizeDelta.x * orbitDistance ) );
			}
			else
			{
				// Fix the position data to be between -0.5 and 0.5 for easy calculations.
				Vector2 positionData = new Vector2( positionHorizontal - 50, positionVertical - 50 ) / 100;

				// Configure the new button position according to the relative transform.
				buttonPosition += ( Vector3 )( ( ( Vector2.one * Mathf.Max( relativeTransform.sizeDelta.x, relativeTransform.sizeDelta.y ) * 2.0f ) * positionData ) - ( ( Vector2.one * textureSize ) * positionData ) );
			}
		}

		// Apply the button size multiplied by the activation range.
		BaseTransform.sizeDelta = new Vector2( textureSize, textureSize ) * activationRange;

		// Convert the calculated position from local space on the canvas to a world position.
		buttonPosition = ParentCanvasTransform.TransformVector( buttonPosition ) + ParentCanvasTransform.position;

		// Apply the calculated position.
		BaseTransform.position = buttonPosition;

		// Apply the size and position to the buttonBase.
		buttonBase.rectTransform.sizeDelta = new Vector2( textureSize, textureSize );
		buttonBase.rectTransform.localPosition = Vector3.zero;

		// If the game is running and the user wants to allow override positioning from the player...
		if( Application.isPlaying && overridePositioning )
		{
			// If the user wants the players settings stored in PlayerPrefs...
			if( saveAsPlayerPrefs )
			{
				// If there is no stored information, then store some default values to the player pref.
				if( !PlayerPrefs.HasKey( $"UBHPO_{uniqueId}" ) || !PlayerPrefs.HasKey( $"UBVPO_{uniqueId}" ) || !PlayerPrefs.HasKey( $"UBSO_{uniqueId}" ) )
				{
					PlayerPrefs.SetFloat( $"UBHPO_{uniqueId}", -1.0f );
					PlayerPrefs.SetFloat( $"UBVPO_{uniqueId}", -1.0f );
					PlayerPrefs.SetFloat( $"UBSO_{uniqueId}", -1.0f );
				}

				// If there is stored information for this button, then apply the stored values.
				if( PlayerPrefs.GetFloat( $"UBHPO_{uniqueId}" ) >= 0.0f || PlayerPrefs.GetFloat( $"UBVPO_{uniqueId}" ) >= 0.0f || PlayerPrefs.GetFloat( $"UBSO_{uniqueId}" ) >= 0.0f )
				{
					buttonPositionOverride.x = PlayerPrefs.GetFloat( $"UBHPO_{uniqueId}" );
					buttonPositionOverride.y = PlayerPrefs.GetFloat( $"UBVPO_{uniqueId}" );
					buttonSizeOverride = PlayerPrefs.GetFloat( $"UBSO_{uniqueId}" );
				}
			}

			// If the stored override value is assigned to something...
			if( buttonPositionOverride != new Vector2( -1, -1 ) || buttonSizeOverride > 0.0f )
			{
				// Temporary positioning vector for figuring out which values to use to position the button.
				Vector2 positioningToUse = buttonPositionOverride == new Vector2( -1, -1 ) ? new Vector2( positionHorizontal, positionVertical ) : buttonPositionOverride;

				// Configure the target size for the button graphic.
				textureSize = Mathf.Min( parentCanvasSize.y, parentCanvasSize.x ) * ( ( buttonSizeOverride <= 0.0f ? buttonSize : buttonSizeOverride ) / 10 );

				// Configure the position that the user wants the button to be located.
				buttonPosition = new Vector2( parentCanvasSize.x * ( positioningToUse.x / 100 ) - ( textureSize * ( positioningToUse.x / 100 ) ) + ( textureSize / 2 ), parentCanvasSize.y * ( positioningToUse.y / 100 ) - ( textureSize * ( positioningToUse.y / 100 ) ) + ( textureSize / 2 ) ) - ( parentCanvasSize / 2 );

				// If the user wants the button anchored to the right of the screen, then flip the horizontal position.
				if( anchor == Anchor.Right )
					buttonPosition.x = -buttonPosition.x;

				// Apply the button size multiplied by the activation range.
				BaseTransform.sizeDelta = new Vector2( textureSize, textureSize ) * activationRange;

				// Apply the calculated button position.
				BaseTransform.localPosition = buttonPosition;

				// Apply the size and position to the button base.
				buttonBase.rectTransform.sizeDelta = new Vector2( textureSize, textureSize );
				buttonBase.rectTransform.localPosition = Vector3.zero;
			}
		}

		// Configure the actual size delta and position of the base trans regardless of the canvas scaler setting.
		Vector2 baseSizeDelta = BaseTransform.sizeDelta * ParentCanvas.scaleFactor;
		Vector2 baseLocalPosition = BaseTransform.localPosition * ParentCanvas.scaleFactor;

		// Calculate the rect of the base trans.
		buttonRect = new Rect( new Vector2( baseLocalPosition.x - ( baseSizeDelta.x / 2 ), baseLocalPosition.y - ( baseSizeDelta.y / 2 ) ) + ( ( ParentCanvasTransform.sizeDelta * ParentCanvas.scaleFactor ) / 2 ), baseSizeDelta );

		// Notify any subscribers that the UpdatePositioning function has been called.
		if( Application.isPlaying )
			OnUpdatePositioning?.Invoke();
	}

	/// <summary>
	/// Returns true on the frame that the Ultimate Button is pressed down.
	/// </summary>
	public bool GetButtonDown ()
	{
		return getButtonDown;
	}

	/// <summary>
	/// Returns true on the frames that the Ultimate Button is being interacted with.
	/// </summary>
	public bool GetButton ()
	{
		return InputActive;
	}

	/// <summary>
	/// Returns true on the frame that the Ultimate Button is released.
	/// </summary>
	public bool GetButtonUp ()
	{
		return getButtonUp;
	}

	/// <summary>
	/// Disables the Ultimate Button.
	/// </summary>
	public void Disable ()
	{
		// If this game object is already disabled, then return.
		if( !gameObject.activeInHierarchy )
			return;

		// Set the buttons state to false.
		InputActive = getButtonDown = getButtonUp = false;
		interactPointerId = -10;

		// If the user wants to show a transition on the different input states...
		if( inputTransition )
		{
			// Stop the input transition coroutine.
			StopCoroutine( "InputTransition" );

			// If the user wants to display tension, then reset the color.
			if( useTension && tensionAccent != null )
				tensionAccent.color = tensionColorDefault;

			// If the user is displaying a fade, then reset to the untouched state.
			if( useFade && canvasGroup != null )
				canvasGroup.alpha = fadeUntouched;

			// If the user is scaling the button, then reset the scale.
			if( useScale )
				buttonBase.rectTransform.transform.localScale = Vector3.one;
		}

		// If the user wants to display a cooldown, then reset the cooldown.
		if( useCooldown )
			ResetCooldown();
		
		// Disable the gameObject.
		gameObject.SetActive( false );
	}

	/// <summary>
	/// Enables the Ultimate Button.
	/// </summary>
	public void Enable ()
	{
		// If the game object is already active, then return.
		if( gameObject.activeInHierarchy )
			return;

		// Enable the gameObject.
		gameObject.SetActive( true );
	}
	
	/// <summary>
	/// Assigns a new sprite to the button's icon image.
	/// </summary>
	/// <param name="newIcon">The new sprite to assign as the icon for the button.</param>
	public void UpdateIcon ( Sprite newIcon )
	{
		// If the button icon is unassigned, then notify the user and return.
		if( buttonIcon == null )
		{
			Debug.LogError( FormatDebug( "The Button Icon image is not assigned", "Please make sure to assign this image variable before trying to modify the button icon sprite", gameObject.name ) );
			return;
		}

		// Apply the new icon to the button icon.
		buttonIcon.sprite = newIcon;
	}

	/// <summary>
	/// Updates the needed variables to display the current cooldown time of the button.
	/// </summary>
	/// <param name="currentTime">The current time of the cooldown.</param>
	/// <param name="maxTime">The max time of the cooldown.</param>
	public void UpdateCooldown ( float currentTime, float maxTime )
	{
		// If the button is currently interactable, then set InCooldown to false so the player cannot use this button while it is in cooldown.
		if( !InCooldown )
			InCooldown = true;

		// Configure the overall percentage of the cooldown with the values provided.
		float overallCooldownPercentage = currentTime / maxTime;

		// If the cooldown image is assigned, then set the fill amount.
		if( cooldownImage != null )
			cooldownImage.fillAmount = overallCooldownPercentage;

		// If the cooldown text is assigned...
		if( useCooldownText && cooldownText != null )
		{
			// Round the current time value to the nearest int.
			int i = Mathf.RoundToInt( currentTime );

			// If the i value is greater than the current time provided by the user, then reduce i by one. This is because rounding will cause the time to be incorrect.
			if( i > currentTime )
				i -= 1;

			// Configure the string to display on the text by formatting it to a whole number.
			string textToDisplay = ( i + 1 ).ToString( "F0" );

			// If the user wants to show the decimal point however, then format the text for that.
			if( displayDecimalCooldown )
				textToDisplay = currentTime.ToString( "F1" );

			// Apply the configured string.
			cooldownText.text = textToDisplay;

			// Evaluate the animation curve.
			cooldownText.rectTransform.localScale = Vector3.Lerp( Vector3.zero, Vector3.one, cooldownTextScaleCurve.Evaluate( -( currentTime - i ) + 1 ) );
		}

		// If the overall percentage of the cooldown is zero or less, then reset the cooldown since it is done.
		if( overallCooldownPercentage <= 0.0f )
			ResetCooldown();
	}

	/// <summary>
	/// Resets the cooldown of the button.
	/// </summary>
	public void ResetCooldown ()
	{
		// Reset the boolean controller so the player can use this button again.
		InCooldown = false;

		// If the cooldown image is assigned, then reset the fill amount.
		if( cooldownImage != null )
			cooldownImage.fillAmount = 0.0f;

		// If the cooldown text is assigned, then reset it.
		if( cooldownText != null )
			cooldownText.text = "";
	}

	/// <summary>
	/// Disables normal interaction to the button and allows the user to move the button to a new position on the screen.
	/// </summary>
	public void StartOverridePositioning ()
	{
		// If the user has not enabled the override positioning option then inform the user, enable the override positioning, and disable storing the PlayerPrefs.
		if( !overridePositioning )
		{
			Debug.LogWarning( FormatDebug( "You are attempting to override the position of this button, however the Player Position Override option has not been enabled", "Please exit play mode and enable the Player Position Override option on the Ultimate Button component", gameObject.name ) );
			overridePositioning = true;
			saveAsPlayerPrefs = false;
		}

		// If the input is currently active on the button, reset it to avoid unwanted behavior.
		if( InputActive )
			ResetButton();

		// Set IsOverridingPosition to true so that the button's position can be overridden.
		IsOverridingPosition = true;
	}

	/// <summary>
	/// Re-enables normal interaction to the button, saving the position of the button to the position override object.
	/// </summary>
	public void StopOverridePositioning ()
	{
		// If the user has not enabled the override positioning option then inform the user, enable the override positioning, and disable storing the PlayerPrefs.
		if( !overridePositioning )
		{
			Debug.LogWarning( FormatDebug( "You are attempting to override the position of this button, however the Player Position Override option has not been enabled", "Please exit play mode and enable the Player Position Override option on the Ultimate Button component", gameObject.name ) );
			overridePositioning = true;
			saveAsPlayerPrefs = false;
		}

		// Set IsOverridingPosition to false so that the button can be interacted with again.
		IsOverridingPosition = false;

		// Update the stored override information and update the button's positioning.
		UpdatePositioningOverride();
		UpdatePositioning();
	}

	/// <summary>
	/// Updates the stored overrides for the button position if the Player Position Override option is enabled.
	/// </summary>
	/// <param name="horizontalPosition">The target percentage horizontal position on the screen. (ex. 50 = 50% = center of the screen)</param>
	/// <param name="verticalPosition">The target percentage horizontal position on the screen. (ex. 50 = 50% = center of the screen)</param>
	public void SetOverridePosition ( float horizontalPosition, float verticalPosition )
	{
		// If the user has not enabled the override positioning option then inform the user, enable the override positioning, and disable storing the PlayerPrefs.
		if( !overridePositioning )
		{
			Debug.LogWarning( FormatDebug( "You are attempting to override the position of this button, however the Player Position Override option has not been enabled", "Please exit play mode and enable the Player Position Override option on the Ultimate Button component", gameObject.name ) );
			overridePositioning = true;
			saveAsPlayerPrefs = false;
		}

		// Store the position values to override.
		buttonPositionOverride = new Vector2( Mathf.Clamp( horizontalPosition, 0.0f, 100.0f ), Mathf.Clamp( verticalPosition, 0.0f, 100.0f ) );

		// If the user wants to save the size override in PlayerPrefs, then set the value.
		if( saveAsPlayerPrefs )
		{
			PlayerPrefs.SetFloat( $"UBHPO_{uniqueId}", buttonPositionOverride.x );
			PlayerPrefs.SetFloat( $"UBVPO_{uniqueId}", buttonPositionOverride.y );
		}

		// Update the positioning to reflect the new size.
		UpdatePositioning();

		// Inform any subscribers that the position has been overridden.
		OnOverridePositioning?.Invoke( buttonPositionOverride, buttonSizeOverride );
	}

	/// <summary>
	/// Overrides the button with a new size.
	/// </summary>
	/// <param name="newSize">New size of the button.</param>
	public void SetOverrideSize ( float newSize )
	{
		// If the user has not enabled the override positioning option then inform the user, enable the override positioning, and disable storing the PlayerPrefs.
		if( !overridePositioning )
		{
			Debug.LogWarning( FormatDebug( "You are attempting to override the position of this button, however the Player Position Override option has not been enabled", "Please exit play mode and enable the Player Position Override option on the Ultimate Button component", gameObject.name ) );
			overridePositioning = true;
			saveAsPlayerPrefs = false;
		}

		// If the user wants to save the size override in PlayerPrefs, then set the value.
		if( saveAsPlayerPrefs )
			PlayerPrefs.SetFloat( $"UBSO_{uniqueId}", newSize );
		// Otherwise just store the provided size value to use.
		else
			buttonSizeOverride = newSize;

		// Update the positioning to reflect the new size.
		UpdatePositioning();

		// Inform any subscribers that the position has been overridden.
		OnOverridePositioning?.Invoke( buttonPositionOverride, buttonSizeOverride );
	}

	/// <summary>
	/// Resets the stored override values for this Ultimate Button.
	/// </summary>
	public void ResetOverridePositioning ()
	{
		// If the user has not enabled the override positioning option then inform the user, enable the override positioning, and disable storing the PlayerPrefs.
		if( !overridePositioning )
		{
			Debug.LogWarning( FormatDebug( "You are attempting to override the position of this button, however the Player Position Override option has not been enabled", "Please exit play mode and enable the Player Position Override option on the Ultimate Button component", gameObject.name ) );
			overridePositioning = true;
			saveAsPlayerPrefs = false;
		}

		// Reset stored overrides.
		buttonPositionOverride = new Vector2( -1, -1 );
		buttonSizeOverride = -1.0f;

		// Reset relevant PlayerPrefs.
		PlayerPrefs.SetFloat( $"UBHPO_{uniqueId}", -1.0f );
		PlayerPrefs.SetFloat( $"UBVPO_{uniqueId}", -1.0f );
		PlayerPrefs.SetFloat( $"UBSO_{uniqueId}", -1.0f );

		// Update the positioning of the button.
		UpdatePositioning();
	}

	/// <summary>
	/// Updates the stored position override values to apply to the button.
	/// </summary>
	void UpdatePositioningOverride ()
	{
		// Configure the position override according to the local position of the button base on the canvas.
		buttonPositionOverride = ( Vector2 )ParentCanvasTransform.InverseTransformPoint( buttonBase.rectTransform.position ) + ( parentCanvasSize / 2 );

		// Adjust the X and Y values to be percentages for positioning.
		buttonPositionOverride.x = ( ( buttonPositionOverride.x - ( buttonBase.rectTransform.sizeDelta.x / 2 ) ) / ( parentCanvasSize.x - buttonBase.rectTransform.sizeDelta.x ) ) * 100;
		buttonPositionOverride.y = ( ( buttonPositionOverride.y - ( buttonBase.rectTransform.sizeDelta.y / 2 ) ) / ( parentCanvasSize.y - buttonBase.rectTransform.sizeDelta.y ) ) * 100;

		// If the user has this button set to the right, then inverse the position value so that it starts from right to left.
		if( anchor == Anchor.Right )
			buttonPositionOverride.x = -( buttonPositionOverride.x - 100 );

		buttonPositionOverride.x = Mathf.Clamp( buttonPositionOverride.x, repositionConstraintMin.x, repositionConstraintMax.x );
		buttonPositionOverride.y = Mathf.Clamp( buttonPositionOverride.y, repositionConstraintMin.y, repositionConstraintMax.y );

		// If the user wants to store the override positioning to PlayerPrefs, then set them to the overridden values.
		if( saveAsPlayerPrefs )
		{
			PlayerPrefs.SetFloat( $"UBHPO_{uniqueId}", buttonPositionOverride.x );
			PlayerPrefs.SetFloat( $"UBVPO_{uniqueId}", buttonPositionOverride.y );
		}

		// Inform any subscribers that the position has been overridden.
		OnOverridePositioning?.Invoke( buttonPositionOverride, buttonSizeOverride );
	}
	// ------------------------------------------- *** END PUBLIC FUNCTIONS FOR THE USER *** ------------------------------------------- //

	// --------------------------------------------- *** STATIC FUNCTIONS FOR THE USER *** --------------------------------------------- //
	/// <summary>
	/// Returns the targeted Ultimate Button if it exists within the scene.
	/// </summary>
	/// <param name="buttonName">The name of the targeted Ultimate Button.</param>
	public static UltimateButton ReturnComponent ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return null;

		return UltimateButtons[ buttonName ];
	}

	/// <summary>
	/// Returns true on the frame that the targeted Ultimate Button is pressed down.
	/// </summary>
	/// <param name="buttonName">The name of the targeted Ultimate Button.</param>
	public static bool GetButtonDown ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return false;
		
		return UltimateButtons[ buttonName ].getButtonDown;
	}

	/// <summary>
	/// Returns true on the frames that the targeted Ultimate Button is being interacted with.
	/// </summary>
	/// <param name="buttonName">The name of the targeted Ultimate Button.</param>
	public static bool GetButton ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return false;

		return UltimateButtons[ buttonName ].InputActive;
	}

	/// <summary>
	/// Returns true on the frame that the targeted Ultimate Button is released.
	/// </summary>
	/// <param name="buttonName">The name of the targeted Ultimate Button.</param>
	/// <returns></returns>
	public static bool GetButtonUp ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return false;

		return UltimateButtons[ buttonName ].getButtonUp;
	}

	/// <summary>
	/// Disables the targeted Ultimate Button.
	/// </summary>
	/// <param name="buttonName">The name of the desired Ultimate Button.</param>
	public static void Disable ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return;

		UltimateButtons[ buttonName ].Disable();
	}

	/// <summary>
	/// Enables the targeted Ultimate Button.
	/// </summary>
	/// <param name="buttonName">The name of the desired Ultimate Button.</param>
	public static void Enable ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return;

		UltimateButtons[ buttonName ].Enable();
	}

	/// <summary>
	/// Assigns a new sprite icon to the targeted Ultimate Button.
	/// </summary>
	/// <param name="buttonName">The name of the desired Ultimate Button.</param>
	/// <param name="newIcon">The new sprite icon to apply to the targeted Ultimate Button.</param>
	public static void UpdateIcon ( string buttonName, Sprite newIcon )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return;

		UltimateButtons[ buttonName ].UpdateIcon( newIcon );
	}

	/// <summary>
	/// Updates the cooldown of the targeted Ultimate Button.
	/// </summary>
	/// <param name="buttonName">The name of the desired Ultimate Button.</param>
	/// <param name="currentTime">The current cooldown time.</param>
	/// <param name="maxTime">The max cooldown time.</param>
	public static void UpdateCooldown ( string buttonName, float currentTime, float maxTime )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return;

		UltimateButtons[ buttonName ].UpdateCooldown( currentTime, maxTime );
	}

	/// <summary>
	/// Resets the cooldown of the targeted Ultimate Button.
	/// </summary>
	/// <param name="buttonName">The name of the desired Ultimate Button.</param>
	public static void ResetCooldown ( string buttonName )
	{
		// If this button name is not connected with an Ultimate Button component, then return.
		if( !ButtonConfirmed( buttonName ) )
			return;

		UltimateButtons[ buttonName ].ResetCooldown();
	}

	static bool ButtonConfirmed ( string buttonName )
	{
		// If the dictionary does not have a button registered with the provided name, then notify the user and return.
		if( !UltimateButtons.ContainsKey( buttonName ) )
		{
			Debug.LogWarning( FormatDebug( $"No Ultimate Button has been registered with the name: {buttonName}", "Please ensure that the Ultimate Button component you are trying to reference has the Button Name property assigned", "Unknown (User Script)" ) );
			return false;
		}
		return true;
	}
	// ------------------------------------------- *** END STATIC FUNCTIONS FOR THE USER *** ------------------------------------------- //

	// DEPRECATED FUNCTIONS //
	[Obsolete( "Please use the Enable() function instead" )]
	public void EnableButton ()
	{
		Enable();
	}

	[Obsolete( "Please use the Disable() function instead" )]
	public void DisableButton ()
	{
		Disable();
	}

	[Obsolete( "Please use the Enable() function instead" )]
	public static void EnableButton ( string buttonName )
	{
		Enable( buttonName );
	}

	[Obsolete( "Please use the Disable() function instead" )]
	public static void DisableButton ( string buttonName )
	{
		Disable( buttonName );
	}
}