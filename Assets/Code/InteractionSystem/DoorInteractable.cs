using System;
using System.Collections;
using Code.InteractionSystem;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;

namespace Code.Doors
{
    public enum DoorMode
    {
        ManualOnly,
        AutoOnly,
        AutoAndManual
    }

    [Serializable]
    public class DoorElement
    {
        public Transform transform;
        public Vector3 closedRot;
        public Vector3 openRot;
    }

    /// <summary>
    /// Универсальная дверь с корректной синхронизацией состояния для late join:
    /// - Server хранит “истину” (целевую степень открытия) в SyncVar _targetOpen [0..1].
    /// - Клиентская визуализация тянется к Evaluate(curve, target) с duration.
    /// - Авто-логика (по дистанции игроков) только на сервере.
    /// - Manual-тоггл изменяет цель на сервере плавной корутиной.
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        [Header("Mode")]
        [SerializeField] private DoorMode mode = DoorMode.ManualOnly;

        [Header("Door Elements (multiple panels supported)")]
        [SerializeField] private DoorElement[] elements = Array.Empty<DoorElement>();

        [Header("Manual settings")]
        [Tooltip("Скорость изменения степени открытия при Manual (доля в сек).")]
        [SerializeField] private float manualSpeed = 2f;

        [Header("Auto settings")]
        [Tooltip("Дистанция, на которой дверь полностью закрыта / полностью открыта.")]
        [SerializeField] private float closeDistance = 10f;
        [SerializeField] private float fullOpenDistance = 1f;

        [Tooltip("Порог инерции смены направления (0.01..0.1).")]
        [SerializeField] private float sensitivity = 0.05f;

        [Header("Client visuals")]
        [SerializeField] private AnimationCurve openCurve  = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private AnimationCurve closeCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField, Tooltip("Время анимации на клиенте, сек")]
        private float animationDuration = 0.5f;

        private readonly SyncVar<float> _targetOpen = new(new SyncTypeSettings
        {
            WritePermission = WritePermission.ServerOnly,
            ReadPermission  = ReadPermission.Observers
        });

        private float _visualDegree;
        private AnimationCurve _currentCurve;

        private float _prevTarget;
        private int   _prevDir;
        private float _accDelta;

        private Transform[] _players = Array.Empty<Transform>();
        private float _scanTimer;

        private Coroutine _manualRoutine;
        private bool _isOpen;

        private bool _initedDoor;

        private void EnsureInit()
        {
            if (_initedDoor) return;
            _initedDoor = true;

            if (elements == null) elements = Array.Empty<DoorElement>();
            if (sensitivity < 0.001f) sensitivity = 0.001f;
            if (manualSpeed < 0f) manualSpeed = 0f;

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void OnEnable()
        {
            EnsureInit();
            _targetOpen.OnChange += OnTargetChanged;
        }

        private void OnDisable()
        {
            _targetOpen.OnChange -= OnTargetChanged;

            if (_manualRoutine != null)
            {
                StopCoroutine(_manualRoutine);
                _manualRoutine = null;
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            EnsureInit();

            _visualDegree = EvaluateByCurve(_targetOpen.Value);
            ApplyToElements(_visualDegree);

            _prevTarget = _targetOpen.Value;
        }

        private void Update()
        {
            if (IsServer && (mode == DoorMode.AutoOnly || mode == DoorMode.AutoAndManual))
                Server_AutoTick();

            float targetCurve = EvaluateByCurve(_targetOpen.Value);
            float step = (animationDuration > 0f) ? Time.deltaTime / animationDuration : 1f;
            _visualDegree = Mathf.MoveTowards(_visualDegree, targetCurve, step);
            ApplyToElements(_visualDegree);
        }

        #region Interactable (Manual)
        protected internal override void OnInteract_Server(NetworkConnection conn, bool force = false)
        {
            base.OnInteract_Server(conn, force);
            if (!IsServer) return;
            if (mode == DoorMode.AutoOnly) return;

            bool wantOpen = !_isOpen;
            if (_manualRoutine != null) StopCoroutine(_manualRoutine);
            _manualRoutine = StartCoroutine(Server_ManualSet(wantOpen));
        }

        private IEnumerator Server_ManualSet(bool open)
        {
            _isOpen = open;
            float target = open ? 1f : 0f;

            float t = _targetOpen.Value;
            float speed = Mathf.Max(0.0001f, manualSpeed);

            while (!Mathf.Approximately(t, target))
            {
                float dir = Mathf.Sign(target - t);
                t += dir * speed * Time.deltaTime;
                t = Mathf.Clamp01(t);
                if (!Mathf.Approximately(_targetOpen.Value, t))
                    _targetOpen.Value = t;
                yield return null;
            }

            _manualRoutine = null;
        }
        #endregion

        #region Server: auto-logic
        private void Server_AutoTick()
        {
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = 0.25f;
                var list = FindObjectsByType<Code.Player.PlayerMovementController>(FindObjectsSortMode.None);
                int count = (list != null) ? list.Length : 0;
                if (count == 0) _players = Array.Empty<Transform>();
                else
                {
                    if (_players.Length != count) _players = new Transform[count];
                    for (int i = 0; i < count; i++)
                        _players[i] = list[i] != null ? list[i].transform : null;
                }
            }

            float nearest = float.MaxValue;
            for (int i = 0; i < _players.Length; i++)
            {
                var t = _players[i];
                if (t == null) continue;
                float d = Vector3.Distance(t.position, transform.position);
                if (d < nearest) nearest = d;
            }

            if (_players.Length == 0 || nearest == float.MaxValue)
                nearest = closeDistance + 1f;

            float denom = Mathf.Max(0.0001f, (closeDistance - fullOpenDistance));
            float newTarget = 1f - Mathf.Clamp01((nearest - fullOpenDistance) / denom);

            int dir = Math.Sign(newTarget - _prevTarget);
            if (_prevDir != 0 && dir != 0 && dir != _prevDir)
            {
                _accDelta += newTarget - _prevTarget;
                if (Mathf.Abs(_accDelta) < sensitivity)
                {
                    newTarget = _prevTarget;
                    dir = _prevDir;
                }
                else
                {
                    _prevDir = dir;
                    _accDelta = 0f;
                }
            }
            else
            {
                _prevDir = dir;
                _accDelta = 0f;
            }

            _prevTarget = newTarget;

            if (!Mathf.Approximately(_targetOpen.Value, newTarget))
                _targetOpen.Value = newTarget;
        }
        #endregion

        #region Client visuals helpers
        private void OnTargetChanged(float prev, float next, bool asServer)
        {
            EnsureInit();
            if (next > prev)      _currentCurve = openCurve;
            else if (next < prev) _currentCurve = closeCurve;
        }

        private float EvaluateByCurve(float degree01)
        {
            var curve = _currentCurve;
            if (curve == null)
                curve = (degree01 >= _visualDegree) ? openCurve : closeCurve;
            degree01 = Mathf.Clamp01(degree01);
            return curve != null ? curve.Evaluate(degree01) : degree01;
        }

        private void ApplyToElements(float curveValue)
        {
            if (elements == null) return;
            for (int i = 0; i < elements.Length; i++)
            {
                var e = elements[i];
                if (e == null || e.transform == null) continue;

                Vector3 rot = Vector3.Lerp(e.closedRot, e.openRot, curveValue);
                e.transform.localRotation = Quaternion.Lerp(e.transform.localRotation, Quaternion.Euler(rot), Time.deltaTime * 3);
            }
        }
        #endregion

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (elements == null) elements = Array.Empty<DoorElement>();
            if (sensitivity < 0.001f) sensitivity = 0.001f;
            if (manualSpeed < 0f) manualSpeed = 0f;

            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }
#endif
    }
}