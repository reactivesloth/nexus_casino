using UnityEngine;

namespace CrazyMinnow.SALSA.OneClicks
{
	public class OneClickDazEyes : MonoBehaviour
	{
		public static void Setup(GameObject go)
		{
			string emotibody = "^emotiguy.*.shape$";
			string body = "^genesis(?:[238])?(fe)?(male)?.shape$";
			string eyelash = "^genesis(?:[238])?(fe)?(male)?eyelashes.shape$";
			string[] eyelidtopLNames = new string[] {"blink_l", "eyelidstopdownl", "eyelidsupperdownupl", "eyesclosedl", "eyeblinkleft"};
			string[] eyelidbotLNames = new string[] {"eyelidsbottomupl", "eyelidslowerupdownl"};
			string[] eyelidtopRNames = new string[] {"blink_r", "eyelidstopdownr", "eyelidsupperdownupr", "eyesclosedr", "eyeblinkright"};
			string[] eyelidbotRNames = new string[] {"eyelidsbottomupr", "eyelidslowerupdownr"};
			string eyelidtopL = "";
			string eyelidbotL = "";
			string eyelidtopR = "";
			string eyelidbotR = "";
			float blinkMax = 1f;
			int gen = -1; // 0=Emotiguy, 1=Genesis1, 2=Genesis2, 3=Genesis3, 8=Genesis8, 81=Genesis8.1
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

				// Get gen
				Transform smrObj = Eyes.FindTransform(eyes.characterRoot, emotibody);
				SkinnedMeshRenderer smr;
				if (smrObj)
				{
					smr = smrObj.GetComponent<SkinnedMeshRenderer>();
					if (smr)
						if (smr.name.ToLower().Contains("emotiguy")) gen = 0;
				}
				else
				{
					smrObj = Eyes.FindTransform(eyes.characterRoot, body);
					if (smrObj)
					{
						smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
						if (smr)
						{
							if (smr.name.ToLower().Contains("genesis")) gen = 1;
							else if (smr.name.ToLower().Contains("genesis2")) gen = 2;
							else if (smr.name.ToLower().Contains("genesis3")) gen = 3;
							else if (smr.name.ToLower().Contains("genesis8")) gen = 8;
							else
								Debug.LogError(warningMessage + "We did not find a proper DAZ SMR for this OneClick.");
						}
					}
				}

				if (gen > -1)
				{
					// Heads - Bone_Rotation
					eyes.BuildHeadTemplate(Eyes.HeadTemplates.Bone_Rotation_XY);
					eyes.heads[0].expData.controllerVars[0].bone = Eyes.FindTransform(eyes.characterRoot,"^head$");
					eyes.heads[0].expData.name = "head";
					eyes.heads[0].expData.components[0].name = "head";
					eyes.headTargetOffset.y = 0.058f;
					eyes.CaptureMin(ref eyes.heads);
					eyes.CaptureMax(ref eyes.heads);
					if (eyes.heads[0].expData.controllerVars[0].bone == null)
						Debug.LogWarning(warningMessage + "Head not found for: " + eyes.heads[0].expData.name);

					// Eyes - Bone_Rotation
					eyes.BuildEyeTemplate(Eyes.EyeTemplates.Bone_Rotation);
					eyes.eyes[0].expData.controllerVars[0].bone = Eyes.FindTransform(eyes.characterRoot,"^lEye$");
					eyes.eyes[0].expData.name = "eyeL";
					eyes.eyes[0].expData.components[0].name = "eyeL";
					eyes.eyes[1].expData.controllerVars[0].bone = Eyes.FindTransform(eyes.characterRoot,"^rEye$");
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
					switch (gen)
					{
						case 0: // Emotiguy
							eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Upper);
							eyes.headTargetOffset.y = 0.85f;
							eyes.headRandDistRange = new Vector2(3f, 3f);
							eyes.eyeRandDistRange = new Vector2(3f, 3f);
							eyelidtopL = eyelidtopLNames[0];
							eyelidtopR = eyelidtopRNames[0];
							// Left upper eyelid
							eyes.blinklids[0].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, emotibody).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[0].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[0].smr, eyelidtopL);
							eyes.blinklids[0].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[0].expData.name = "eyelidL";
							// Right upper eyelid
							eyes.blinklids[1].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, emotibody).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[1].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[0].smr, eyelidtopR);
							eyes.blinklids[1].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[1].expData.name = "eyelidR";
							// Tracklids
							eyes.CopyBlinkToTrack();
							eyes.tracklids[0].referenceIdx = 0; // left eye
							eyes.tracklids[1].referenceIdx = 1; // right eye
							break;
						case 1: // Genesis 1
							eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Upper);
							eyelidtopL = eyelidtopLNames[3];
							eyelidtopR = eyelidtopRNames[3];
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
							// Tracklids
							eyes.CopyBlinkToTrack();
							eyes.tracklids[0].referenceIdx = 0; // left eye
							eyes.tracklids[1].referenceIdx = 1; // right eye
							break;
						case 2: // Genesis 2
							eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Both);
							eyelidtopL = eyelidtopLNames[1];
							eyelidbotL = eyelidbotLNames[0];
							eyelidtopR = eyelidtopRNames[1];
							eyelidbotR = eyelidbotRNames[0];
							// Left upper eyelid
							eyes.blinklids[0].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[0].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[0].smr, eyelidtopL);
							eyes.blinklids[0].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[0].expData.name = "eyelidL";
							// Left lower eyelid
							eyes.blinklids[0].expData.controllerVars[1].smr = eyes.blinklids[0].expData.controllerVars[0].smr;
							eyes.blinklids[0].expData.controllerVars[1].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[1].smr, eyelidbotL);
							eyes.blinklids[0].expData.controllerVars[1].maxShape = blinkMax;
							// Right upper eyelid
							eyes.blinklids[1].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[1].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[0].smr, eyelidtopR);
							eyes.blinklids[1].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[1].expData.name = "eyelidR";
							// Right lower eyelid
							eyes.blinklids[1].expData.controllerVars[1].smr = eyes.blinklids[1].expData.controllerVars[0].smr;
							eyes.blinklids[1].expData.controllerVars[1].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[1].smr, eyelidbotR);
							eyes.blinklids[1].expData.controllerVars[1].maxShape = blinkMax;
							// Tracklids
							eyes.CopyBlinkToTrack();
							eyes.tracklids[0].referenceIdx = 0; // left eye
							eyes.tracklids[1].referenceIdx = 1; // right eye
							break;
						case 3: // Genesis 3
							eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Both);
							eyelidtopL = eyelidtopLNames[2];
							eyelidbotL = eyelidbotLNames[1];
							eyelidtopR = eyelidtopRNames[2];
							eyelidbotR = eyelidbotRNames[1];
							// Left upper eyelid
							eyes.blinklids[0].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[0].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[0].smr, eyelidtopL);
							eyes.blinklids[0].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[0].expData.name = "eyelidL";
							// Left lower eyelid
							eyes.blinklids[0].expData.controllerVars[1].smr = eyes.blinklids[0].expData.controllerVars[0].smr;
							eyes.blinklids[0].expData.controllerVars[1].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[0].expData.controllerVars[1].smr, eyelidbotL);
							eyes.blinklids[0].expData.controllerVars[1].maxShape = blinkMax;
							// Right upper eyelid
							eyes.blinklids[1].expData.controllerVars[0].smr = Eyes.FindTransform(eyes.characterRoot, body).GetComponent<SkinnedMeshRenderer>();
							eyes.blinklids[1].expData.controllerVars[0].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[0].smr, eyelidtopR);
							eyes.blinklids[1].expData.controllerVars[0].maxShape = blinkMax;
							eyes.blinklids[1].expData.name = "eyelidR";
							// Right lower eyelid
							eyes.blinklids[1].expData.controllerVars[1].smr = eyes.blinklids[0].expData.controllerVars[0].smr;
							eyes.blinklids[1].expData.controllerVars[1].blendIndex = Eyes.FindBlendIndex(eyes.blinklids[1].expData.controllerVars[1].smr, eyelidbotR);
							eyes.blinklids[1].expData.controllerVars[1].maxShape = blinkMax;
							// Tracklids
							eyes.CopyBlinkToTrack();
							eyes.tracklids[0].referenceIdx = 0; // left eye
							eyes.tracklids[1].referenceIdx = 1; // right eye
							break;
						case 8: // Genesis 8
							eyes.BuildEyelidTemplate(Eyes.EyelidTemplates.BlendShapes, Eyes.EyelidSelection.Upper);
							eyes.AddEyelidShapeExpression(ref eyes.blinklids);
							eyes.AddEyelidShapeExpression(ref eyes.blinklids);
							eyelidtopL = eyelidtopLNames[3];
							eyelidtopR = eyelidtopRNames[3];
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
							break;
					}

					foreach (var blinkshape in eyes.blinklids)
					{
						foreach (var controller in blinkshape.expData.controllerVars)
						{
							if (controller.blendIndex == -1)
								Debug.LogWarning(warningMessage + "Blendshape not found for: " +
								                 blinkshape.expData.name +
								                 " Ensure you are using the correct shapes and the available morph presets (for Gen8 models) for DAZ Studio (from the Minnow downloads portal). "+
								                 "See the DAZ OneClick documentation for more info: https://crazyminnowstudio.com/docs/salsa-lip-sync/addons/one-clicks/#daz3d-for-genesis-models");
						}
					}


					// Initialize the Eyes module
					eyes.Initialize();
				}
			}
		}
	}
}