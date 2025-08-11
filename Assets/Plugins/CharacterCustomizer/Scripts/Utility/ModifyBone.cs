using UnityEngine;
using UnityEngine.Animations;

namespace CC
{
    [DefaultExecutionOrder(50)]
    public class ModifyBone : MonoBehaviour
    {
        public CC_ModifyType Type;
        public bool Inverted = false;
        public float currentValue = 1f;
        [Range(0f, 1f)] public float alpha = 1f;
        public bool updates;

        private Animator _animator;
        public bool ragdolling;

        public Transform constraintRoot;
        private Transform _constraintObj;
        private PositionConstraint _posConstraint;
        private RotationConstraint _rotConstraint;

        public bool xAxis;
        public bool yAxis;
        public bool zAxis;

        private void Awake()
        {
            _animator = GetComponentInParent<Animator>(true);
        }

        public void Modify()
        {
            switch (Type)
            {
                case CC_ModifyType.ButtSize:
                case CC_ModifyType.HeadSize:
                case CC_ModifyType.Height:
                    transform.localScale = new Vector3(currentValue, currentValue, currentValue);
                    break;

                case CC_ModifyType.TorsoHeight:
                case CC_ModifyType.NeckLength:
                case CC_ModifyType.LegsWidth:
                case CC_ModifyType.ShoulderWidth:
                case CC_ModifyType.HeightOffset:
                {
                    float val = currentValue / 100f * (Inverted ? 1f : -1f);
                    EnsurePosConstraint();
                    TogglePosConstraint();
                    if (_constraintObj != null)
                        _constraintObj.localPosition = new Vector3(val * (xAxis ? 1 : 0), val * (yAxis ? 1 : 0), val * (zAxis ? 1 : 0));
                    break;
                }

                case CC_ModifyType.BreastSize:
                    transform.localScale = new Vector3(currentValue, currentValue, currentValue);
                    // slight forward offset intentionally left out (positionOffset was not applied in original)
                    break;

                case CC_ModifyType.LowerWaistSize:
                case CC_ModifyType.MidWaistSize:
                case CC_ModifyType.UpperWaistSize:
                case CC_ModifyType.UpperTorsoSize:
                case CC_ModifyType.ThighScale:
                case CC_ModifyType.CalfScale:
                case CC_ModifyType.UpperArmScale:
                case CC_ModifyType.LowerArmScale:
                case CC_ModifyType.NeckScale:
                case CC_ModifyType.HipWidth:
                    transform.localScale = new Vector3(1f, currentValue, currentValue);
                    break;

                case CC_ModifyType.FootRotation:
                case CC_ModifyType.BallRotation:
                {
                    float rot = currentValue * (Inverted ? -1f : 1f);
                    EnsureRotConstraint();
                    ToggleRotConstraint();
                    if (_constraintObj != null)
                        _constraintObj.localEulerAngles = new Vector3(rot * (xAxis ? 1 : 0), rot * (yAxis ? 1 : 0), rot * (zAxis ? 1 : 0));
                    break;
                }
            }
        }

        private void EnsurePosConstraint()
        {
            if (constraintRoot == null) return;
            CreateConstraintObj();
            if (_posConstraint == null)
            {
                _posConstraint = GetComponent<PositionConstraint>();
                if (_posConstraint == null) _posConstraint = gameObject.AddComponent<PositionConstraint>();
            }

            if (_constraintObj == null) return;

            _posConstraint.translationOffset = _constraintObj.InverseTransformVector(transform.position - _constraintObj.position);
            _posConstraint.weight = alpha;
            var src = new ConstraintSource { sourceTransform = _constraintObj, weight = 1f };
            if (_posConstraint.sourceCount == 0) _posConstraint.AddSource(src); else _posConstraint.SetSource(0, src);
            _posConstraint.locked = true;
            _posConstraint.constraintActive = true;
        }

        private void EnsureRotConstraint()
        {
            if (constraintRoot == null) return;
            CreateConstraintObj();
            if (_rotConstraint == null)
            {
                _rotConstraint = GetComponent<RotationConstraint>();
                if (_rotConstraint == null) _rotConstraint = gameObject.AddComponent<RotationConstraint>();
            }

            if (_constraintObj == null) return;

            _rotConstraint.rotationOffset = transform.localEulerAngles;
            _rotConstraint.weight = alpha;
            var src = new ConstraintSource { sourceTransform = _constraintObj, weight = 1f };
            if (_rotConstraint.sourceCount == 0) _rotConstraint.AddSource(src); else _rotConstraint.SetSource(0, src);
            _rotConstraint.locked = true;
            _rotConstraint.constraintActive = true;
        }

        private void CreateConstraintObj()
        {
            if (_constraintObj != null) return;
            if (constraintRoot == null) return;

            var name = transform.name + "Constraint";
            var t = constraintRoot.Find(name);
            if (t == null)
            {
                var go = new GameObject(name);
                _constraintObj = go.transform;
                _constraintObj.SetParent(constraintRoot);
                _constraintObj.localPosition = Vector3.zero;
                _constraintObj.localRotation = Quaternion.identity;
                _constraintObj.localScale = Vector3.one;
            }
            else _constraintObj = t;
        }

        private void ToggleRotConstraint()
        {
            if (_rotConstraint != null)
                _rotConstraint.enabled = (currentValue != 0f && updates);
        }

        private void TogglePosConstraint()
        {
            if (_posConstraint != null)
                _posConstraint.enabled = (currentValue != 0f && updates);
        }

        public void onSimulate(bool value) { /* kept for compatibility */ }
    }
}