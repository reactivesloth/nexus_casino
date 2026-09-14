using System;
using UnityEngine;

namespace CrazyMinnow.SALSA.OneClicks
{
	public class OneClickDazEyesGen9 : MonoBehaviour
	{
		public static void Setup(GameObject go)
		{
			string body = "^Genesis(8_1(fe)?male|9)\\.Shape$";
			string eyelash = "Genesis(8_1(fe)?male|9)Eyelashes\\.Shape$";
			string eyelidtopL = "eyeblinkleft";
			string eyelidtopR = "eyeblinkright";
			float blinkMax = 1f;
			string warningMessage = "Eyes module on " + go.name + " is not properly configured: ";

			if (go)
			{
				Eyes eyes = go.GetComponent<Eyes>();
				if (eyes == null)
				{
					eyes = go.AddComponent<Eyes>();
				}
				else
				{
					DestroyImmediate(eyes);
					eyes = go.AddComponent<Eyes>();
				}

				// System Properties
                eyes.characterRoot = go.transform;

				// Heads - Bone_Rotation
				eyes.BuildHeadTemplate(Eyes.HeadTemplates.Bone_Rotation_XY);
				var headObjects = Array.FindAll(go.GetComponentsInChildren<Transform>(), head => head.name.ToLower().Contains("head") && !head.name.ToLower().Contains("gizmo"));
				eyes.heads[0].expData.controllerVars[0].bone = headObjects[0];
				eyes.heads[0].expData.name = "head";
				eyes.heads[0].expData.components[0].name = "head";
				for (int h = 1; h < headObjects.Length; h++)
				{
					eyes.AddComponent(ref eyes.heads, 0, ExpressionComponent.ExpressionType.Head, ExpressionComponent.ControlType.Bone, false, true, false);
					eyes.heads[0].expData.controllerVars[h].bone = headObjects[h];
					eyes.heads[0].expData.name = "head";
					eyes.heads[0].expData.components[h].name = "head";
				}
				eyes.headTargetOffset.y = 0.058f;
				eyes.CaptureMin(ref eyes.heads);
				eyes.CaptureMax(ref eyes.heads);
				if (eyes.heads[0].expData.controllerVars[0].bone == null)
					Debug.LogWarning(warningMessage + "Head not found for: " + eyes.heads[0].expData.name);

				// Eyes - Bone_Rotation
				eyes.BuildEyeTemplate(Eyes.EyeTemplates.Bone_Rotation);
				eyes.eyes[0].expData.controllerVars[0].bone = Eyes.FindTransform(eyes.characterRoot,"^(l|l_)?Eye$");
				eyes.eyes[0].expData.name = "eyeL";
				eyes.eyes[0].expData.components[0].name = "eyeL";
				eyes.eyes[1].expData.controllerVars[0].bone = Eyes.FindTransform(eyes.characterRoot,"^(r|r_)?Eye$");
				eyes.eyes[1].expData.name = "eyeR";
				eyes.eyes[1].expData.components[0].name = "eyeR";
				eyes.CaptureMin(ref eyes.eyes);
				eyes.CaptureMax(ref eyes.eyes);
				foreach (var eye in eyes.eyes)
				{
					foreach (var controller in eye.expData.controllerVars)
					{
						if (controller.bone == null)
							Debug.LogWarning(warningMessage + "Bone not found for: " + eye.expData.name);
					}
				}

				// Eyelids - Bone_Rotation
				eyes.eyelidPercentEyes = 1f;
				blinkMax = 1f;
				eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Upper);
				eyes.AddEyelidShapeExpression(ref eyes.blinklids);
				eyes.AddEyelidShapeExpression(ref eyes.blinklids);
				// Left upper eyelid
				eyes.blinklids[0].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
				eyes.blinklids[0].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[0].smr, eyelidtopL);
				eyes.blinklids[0].expData.controllerVars[0].maxShape = blinkMax;
				eyes.blinklids[0].expData.name = "eyelidL";
				// Right upper eyelid
				eyes.blinklids[1].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
				eyes.blinklids[1].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[0].smr, eyelidtopR);
				eyes.blinklids[1].expData.controllerVars[0].maxShape = blinkMax;
				eyes.blinklids[1].expData.name = "eyelidR";
				// Left upper eyelash
				eyes.blinklids[2].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, eyelash).GetComponent<SkinnedMeshRenderer>();
				eyes.blinklids[2].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[2].expData.controllerVars[0].smr, eyelidtopL);
				eyes.blinklids[2].expData.controllerVars[0].maxShape = blinkMax;
				eyes.blinklids[2].expData.name = "eyelashL";
				// Right upper eyelash
				eyes.blinklids[3].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, eyelash).GetComponent<SkinnedMeshRenderer>();
				eyes.blinklids[3].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[3].expData.controllerVars[0].smr, eyelidtopR);
				eyes.blinklids[3].expData.controllerVars[0].maxShape = blinkMax;
				eyes.blinklids[3].expData.name = "eyelashR";
				// Tracklids
				eyes.CopyBlinkToTrack();
				eyes.tracklids[0].referenceIdx = 0; // left eye
				eyes.tracklids[1].referenceIdx = 1; // right eye
				eyes.tracklids[2].referenceIdx = 0; // left eye
				eyes.tracklids[3].referenceIdx = 1; // right eye

				foreach (var blinkshape in eyes.blinklids)
				{
					foreach (var controller in blinkshape.expData.controllerVars)
					{
						if (controller.blendIndex == -1)
							Debug.LogWarning(warningMessage + "Blendshape not found for: " +
							                 blinkshape.expData.name +
							                 " Ensure you are using the correct shapes and the available morph presets for DAZ Studio (from the Minnow downloads portal). "+
							                 "See the DAZ OneClick documentation for more info: https://crazyminnowstudio.com/docs/salsa-lip-sync/addons/one-clicks/#daz3d-for-genesis-models");
					}
				}

				// Initialize the Eyes module
				eyes.Initialize();
			}
		}
	}
}