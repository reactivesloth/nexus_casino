using UnityEngine;
using UnityEngine.EventSystems;

namespace CC
{
    [DefaultExecutionOrder(100)]
    public class CameraController : MonoBehaviour
    {
        public static CameraController instance;

        public float ZoomMin = -0.6f;
        public float ZoomMax = -3.1f;
        public float ZoomPanScale = 0.5f; // (kept for compatibility; not used directly)
        public float zoomTarget = 0.5f;

        [SerializeField] private float rotateSpeed = 5f;
        [SerializeField] private float panSpeed = 3f;

        public Vector3 cameraOffsetMin = new Vector3(-0.15f, 0.5f, 0);
        public Vector3 cameraOffsetMax = new Vector3(-0.3f, -0.1f, 0);

        public float defaultHeadLevel = 1.8f;
        public GameObject headLevelObject; // can be assigned in inspector

        private Camera _camera;
        private Transform _cameraRoot;

        private Vector3 _mouseOldPos;
        private Vector3 _cameraRotationTarget = new Vector3(10, -5, 0);
        private Vector3 _cameraRotationDefault;

        private bool _dragging;
        private bool _panning;

        private Vector3 _cameraOffset;
        private Vector3 _panOffset;

        // reduce expensive FindGameObjectWithTag calls
        private float _headProbeCooldown;
        private const float HEAD_PROBE_PERIOD = 0.5f;

        private void Awake()
        {
            if (instance == null) instance = this; else { Destroy(gameObject); return; }
        }

        private void Start()
        {
            _camera = GetComponentInChildren<Camera>(true);
            if (_camera == null)
            {
                Debug.LogError("CameraController: no Camera found in children.");
                enabled = false; return;
            }

            _cameraRoot = transform;
            _cameraRotationDefault = _cameraRoot.localRotation.eulerAngles;
            _cameraRotationTarget = _cameraRotationDefault;
        }

        public void ResetCamera()
        {
            _cameraRotationTarget = _cameraRotationDefault;
            _panOffset = Vector3.zero;
        }

        private void TryUpdateHeadLevel()
        {
            // probe not more than twice per second
            _headProbeCooldown -= Time.deltaTime;
            if (_headProbeCooldown > 0f) return;
            _headProbeCooldown = HEAD_PROBE_PERIOD;

            if (headLevelObject == null || !headLevelObject.activeInHierarchy)
            {
                var es = GameObject.FindGameObjectWithTag("HeadLevel");
                if (es != null) headLevelObject = es;
            }
        }

        private float GetHeadAdjust()
        {
            if (headLevelObject == null || headLevelObject.transform == null) return 0f;
            var parent = transform.parent;
            var parentY = parent != null ? parent.position.y : 0f;
            return defaultHeadLevel - (headLevelObject.transform.position.y - parentY);
        }

        private static bool IsPointerOverUI()
        {
            // guard – EventSystem might be missing in some scenes
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void LateUpdate()
        {
            TryUpdateHeadLevel();
        }

        private void Update()
        {
            if (_camera == null || !_camera.gameObject.activeSelf) return;

            if (!IsPointerOverUI())
            {
                var scrollDelta = Input.mouseScrollDelta.y;
                if (scrollDelta < 0)
                    zoomTarget = Mathf.Clamp(zoomTarget * 1.2f, 0.05f, 1f);
                else if (scrollDelta > 0)
                    zoomTarget = Mathf.Clamp01(zoomTarget * 0.8f);

                if (scrollDelta != 0f) _panOffset = Vector3.Lerp(_panOffset, Vector3.zero, 0.1f);

                if (Input.GetMouseButtonDown(1)) { _mouseOldPos = Input.mousePosition; _dragging = true; }
                if (Input.GetMouseButtonDown(2)) { _mouseOldPos = Input.mousePosition; _panning = true; }
            }

            if (Input.GetMouseButton(1) && _dragging)
            {
                Vector3 mouseDelta = _mouseOldPos - Input.mousePosition;
                _cameraRotationTarget.x += mouseDelta.y / 5f;
                _cameraRotationTarget.y -= mouseDelta.x / 5f;
                _mouseOldPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(1)) _dragging = false;

            if (Input.GetMouseButton(2) && _panning)
            {
                Vector3 mouseDelta = _mouseOldPos - Input.mousePosition;
                _panOffset -= mouseDelta / 500f;
                _mouseOldPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2)) _panning = false;

            if (Input.GetKeyDown(KeyCode.F)) ResetCamera();

            float headAdjust = GetHeadAdjust();

            _cameraOffset = Vector3.Lerp(cameraOffsetMin, cameraOffsetMax, zoomTarget) + _panOffset;
            _cameraOffset.z = Mathf.Lerp(ZoomMin, ZoomMax, Mathf.Clamp01(zoomTarget));
            _cameraOffset.y -= headAdjust;

            _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition, _cameraOffset, Time.deltaTime * panSpeed);

            if (Mathf.Approximately(rotateSpeed, 0f))
                _cameraRoot.localRotation = Quaternion.Euler(_cameraRotationTarget);
            else
                _cameraRoot.localRotation = Quaternion.Slerp(_cameraRoot.localRotation, Quaternion.Euler(_cameraRotationTarget), Time.deltaTime * rotateSpeed);
        }
    }
}