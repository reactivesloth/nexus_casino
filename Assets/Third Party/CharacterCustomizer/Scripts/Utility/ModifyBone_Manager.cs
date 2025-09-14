using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    [DefaultExecutionOrder(50)]
    public class ModifyBone_Manager : MonoBehaviour
    {
        public float maxUpdateDistance = 10;
        public bool enableUpdate = true;

        private CharacterCustomization customizer;
        private Dictionary<CC_ModifyType, List<ModifyBone>> _byType;
        private static readonly List<ModifyBone> _emptyList = new List<ModifyBone>(0);

        private float hipScale;
        private float waistScale;
        private float shoulderWidth;
        private float height = 1f;
        private float headScale = 1f;

        private void Awake() { EnsureInitialized(); }
        private void OnEnable() { _byType = null; }

        private void EnsureInitialized()
        {
            if (customizer == null) customizer = GetComponentInParent<CharacterCustomization>();
            if (_byType == null) RebuildCache();
        }

        public void RebuildCache()
        {
            var modifyBones = GetComponentsInChildren<ModifyBone>(true);
            var dict = new Dictionary<CC_ModifyType, List<ModifyBone>>(32);
            for (int i = 0; i < modifyBones.Length; i++)
            {
                var mb = modifyBones[i];
                if (mb == null) continue;
                var t = mb.Type;
                if (!dict.TryGetValue(t, out var list)) { list = new List<ModifyBone>(4); dict.Add(t, list); }
                list.Add(mb);
            }
            _byType = dict;
        }

        private List<ModifyBone> GetModifyScripts(CC_ModifyType type)
        {
            EnsureInitialized();
            return _byType.TryGetValue(type, out var list) ? list : _emptyList;
        }

        public void setModifyValue(string modifyType, float value)
        {
            EnsureInitialized();
            switch (modifyType)
            {
                case "BodyCustomization_WaistSize":
                    waistScale = value;
                    float waistLerped = lerp2(1, 1.4f, waistScale);
                    foreach (var item in GetModifyScripts(CC_ModifyType.MidWaistSize)) { item.currentValue = waistLerped; item.Modify(); }
                    //Lower waist is blend between waist size and hip width
                    foreach (var item in GetModifyScripts(CC_ModifyType.LowerWaistSize)) { item.currentValue = Mathf.Lerp(lerp2(1, 1.4f, hipScale), Mathf.Clamp(waistLerped, 1, 1.4f), 0.5f); item.Modify(); }
                    //Upper waist is blend between waist size and shoulder width
                    foreach (var item in GetModifyScripts(CC_ModifyType.UpperWaistSize)) { item.currentValue = Mathf.Lerp(lerp2(1, 1.4f, shoulderWidth), Mathf.Clamp(waistLerped, 1, 1.4f), 0.5f); item.Modify(); }
                    break;

                case "BodyCustomization_HipWidth":
                    hipScale = value;
                    float hipLerped = lerp2(1, 1.2f, hipScale);
                    foreach (var item in GetModifyScripts(CC_ModifyType.HipWidth)) { item.currentValue = hipLerped; item.Modify(); }
                    //Lower waist is blend between waist size and hip width
                    foreach (var item in GetModifyScripts(CC_ModifyType.LowerWaistSize)) { item.currentValue = Mathf.Lerp(hipLerped, Mathf.Lerp(1, 1.4f, waistScale), 0.5f); item.Modify(); }
                    //Leg width
                    foreach (var item in GetModifyScripts(CC_ModifyType.LegsWidth)) { item.currentValue = Mathf.Clamp(lerp2(0, 2.5f, hipScale), -2.5f, 1f); item.Modify(); }
                    break;

                case "BodyCustomization_NeckScale":
                    customizer.setBlendshapeByName("mod_neck_fat", Mathf.Clamp01(value));
                    foreach (var item in GetModifyScripts(CC_ModifyType.NeckScale)) { item.currentValue = lerp2(1, 1.2f, value); item.Modify(); }
                    break;

                case "BodyCustomization_ThighScale":
                    foreach (var item in GetModifyScripts(CC_ModifyType.ThighScale)) { item.currentValue = lerp2(1, 1.2f, value); item.Modify(); }
                    break;

                case "BodyCustomization_CalfScale":
                    foreach (var item in GetModifyScripts(CC_ModifyType.CalfScale)) { item.currentValue = lerp2(1, 1.2f, value); item.Modify(); }
                    break;

                case "BodyCustomization_UpperArmScale":
                    foreach (var item in GetModifyScripts(CC_ModifyType.UpperArmScale)) { item.currentValue = lerp2(1, 1.2f, value); item.Modify(); }
                    break;

                case "BodyCustomization_LowerArmScale":
                    foreach (var item in GetModifyScripts(CC_ModifyType.LowerArmScale)) { item.currentValue = lerp2(1, 1.2f, value); item.Modify(); }
                    break;

                case "BodyCustomization_ButtSize":
                    foreach (var item in GetModifyScripts(CC_ModifyType.ButtSize)) { item.currentValue = Mathf.Lerp(1, 1.4f, value); item.Modify(); }
                    break;

                case "BodyCustomization_BreastSize":
                    foreach (var item in GetModifyScripts(CC_ModifyType.BreastSize)) { item.currentValue = Mathf.Lerp(1, 1.5f, value); item.Modify(); }
                    break;

                case "BodyCustomization_ShoulderWidth":
                    shoulderWidth = value;
                    foreach (var item in GetModifyScripts(CC_ModifyType.ShoulderWidth)) { item.currentValue = lerp2(0, 1.5f, value); item.Modify(); }
                    foreach (var item in GetModifyScripts(CC_ModifyType.UpperTorsoSize)) { item.currentValue = lerp2(1, 1.1f, value); item.Modify(); }
                    //Upper waist is blend between waist size and shoulder width
                    foreach (var item in GetModifyScripts(CC_ModifyType.UpperWaistSize)) { item.currentValue = Mathf.Lerp(lerp2(1, 1.4f, shoulderWidth), Mathf.Lerp(1, 1.4f, waistScale), 0.5f); item.Modify(); }
                    break;

                case "BodyCustomization_TorsoHeight":
                    foreach (var item in GetModifyScripts(CC_ModifyType.TorsoHeight)) { item.currentValue = Mathf.Lerp(0, 2, value); item.Modify(); }
                    break;

                case "BodyCustomization_FootRotation":
                    foreach (var item in GetModifyScripts(CC_ModifyType.FootRotation)) { item.currentValue = value; item.Modify(); }
                    break;

                case "BodyCustomization_BallRotation":
                    foreach (var item in GetModifyScripts(CC_ModifyType.BallRotation)) { item.currentValue = value; item.Modify(); }
                    break;

                case "BodyCustomization_HeightOffset":
                    foreach (var item in GetModifyScripts(CC_ModifyType.HeightOffset)) { item.currentValue = value; item.Modify(); }
                    break;

                case "BodyCustomization_NeckLength":
                    foreach (var item in GetModifyScripts(CC_ModifyType.NeckLength)) { item.currentValue = lerp2(0, 1, value); item.Modify(); }
                    break;

                case "BodyCustomization_HeadScale":
                case "BodyCustomization_Height":
                    //Height (root scale)
                    if (modifyType.Contains("Height"))
                    {
                        height = lerp2(1, 1.05f, value);
                    }
                    //Scale head by head size and also by inverse height
                    else
                    {
                        headScale = lerp2(1, 1.1f, value);
                    }
                    float headscaleSet = 1 / height * headScale;
                    foreach (var item in GetModifyScripts(CC_ModifyType.HeadSize)) { item.currentValue = headscaleSet; item.Modify(); }
                    foreach (var item in GetModifyScripts(CC_ModifyType.Height)) { item.currentValue = height; item.Modify(); }
                    break;

                case "BodyCustomization_Weight":
                    customizer.setBlendshapeByName("BodyCustomization_WaistSize", Mathf.Clamp(lerp2(0, 1, value), -0.6f, 1), true);
                    customizer.setBlendshapeByName("BodyCustomization_ShoulderWidth", Mathf.Clamp(lerp2(0, 1, value), -0.6f, 0.15f), true);
                    customizer.setBlendshapeByName("BodyCustomization_HipWidth", Mathf.Clamp(lerp2(0, 1, value), -0.2f, 0.4f), true);
                    customizer.setBlendshapeByName("BodyCustomization_ThighScale", Mathf.Clamp(lerp2(0, 1, value), -0.5f, 0.75f), true);
                    customizer.setBlendshapeByName("BodyCustomization_CalfScale", Mathf.Clamp(lerp2(0, 1, value), -0.5f, 0.75f), true);
                    customizer.setBlendshapeByName("BodyCustomization_UpperArmScale", Mathf.Clamp(lerp2(0, 1, value), -0.5f, 0.75f), true);
                    customizer.setBlendshapeByName("BodyCustomization_LowerArmScale", Mathf.Clamp(lerp2(0, 1, value), -0.5f, 0.75f), true);
                    customizer.setBlendshapeByName("BodyCustomization_NeckScale", Mathf.Clamp(lerp2(0, 1, value), -0.25f, 0.75f), true);
                    customizer.setBlendshapeByName("BodyCustomization_TorsoHeight", 0, true);
                    customizer.setBlendshapeByName("BodyCustomization_BreastSize", 0, true);
                    customizer.setBlendshapeByName("BodyCustomization_ButtSize", 0, true);
                    break;
            }
        }

        private float lerp2(float a, float b, float t) => a + (b - a) * t;
    }
}