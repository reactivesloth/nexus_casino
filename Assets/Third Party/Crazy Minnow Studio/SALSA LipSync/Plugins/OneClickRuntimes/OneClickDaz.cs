using UnityEngine;

namespace CrazyMinnow.SALSA.OneClicks
{
	public class OneClickDaz : OneClickBase
	{
		/// <summary>
		/// Setup and run OneClick operation on the supplied GameObject.
		/// </summary>
		/// <param name="gameObject">Root OneClick GameObject.</param>
		/// <param name="clip">AudioClip (can be null).</param>
		public static void Setup(GameObject gameObject)
		{
			////////////////////////////////////////////////////////////////////////////////////////////////////////////
			//	SETUP Requirements:
			//		use NewViseme("viseme name") to start a new viseme.
			//		use AddShapeComponent to add blendshape configurations, passing:
			//			- string array of shape names to look for.
			//			- optional string name prefix for the component.
			//			- optional blend amount (default = 1.0f).

			Init();

			#region SALSA-Configuration

			NewConfiguration(OneClickConfiguration.ConfigType.Salsa);
			{
				////////////////////////////////////////////////////////
				// SMR regex searches (enable/disable/add as required).
				AddSmrSearch("^genesis([238])?(fe)?(male)?\\.shape$");
				AddSmrSearch("^emotiguy.*\\.shape$");
				AddSmrSearch(".*beard.*\\.shape$");

				////////////////////////////////////////////////////////
				// Adjust SALSA settings to taste...
				// - data analysis settings
				autoAdjustAnalysis = true;
				autoAdjustMicrophone = false;
				audioUpdateDelay = 0.09282f;

				// - advanced dynamics settings
				loCutoff = 0.015f;
				hiCutoff = 0.87f;
				useAdvDyn = true;
				advDynPrimaryBias = 0.5f;
				useAdvDynJitter = true;
				advDynJitterAmount = 0.1f;
				advDynJitterProb = 0.25f;
				advDynSecondaryMix = 0f;
				emphasizerTrigger = 0f;


				////////////////////////////////////////////////////////
				// Viseme setup...

				NewExpression("w");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)W$"}, 0.11f, 0f, 0.07f, "viseme w", 1f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_PuckerLipsWide$"}, 0.11f, 0f, 0.07f, "PuckerLipsWide", 1f, true);

				NewExpression("f");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)F$"}, 0.11f, 0f, 0.07f, "viseme f", 1f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_MouthF"}, 0.11f, 0f, 0.07f, "MouthF", 1f, true);
				AddShapeComponent(new[] {"^.*_MouthTH"}, 0.11f, 0f, 0.07f, "MouthTH", 0.231f, true);


				NewExpression("t");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)T$"}, 0.11f, 0f, 0.07f, "viseme t", 1f, true);
				// gen3
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)W$"}, 0.11f, 0f, 0.07f, "viseme w", 0.40f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_MouthTH"}, 0.11f, 0f, 0.07f, "MouthTH", 0.709f, true);
				AddShapeComponent(new[] {"^.*_PuckerLips"}, 0.11f, 0f, 0.07f, "PuckerLips", 0.531f, true);


				NewExpression("th");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)TH$"}, 0.11f, 0f, 0.07f, "viseme TH", 1f, true);
				// gen3
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthSmile$"}, 0.11f, 0f, 0.07f, "smile", 0.27f, true);
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)EE$", "^.*_(eCTRLv|CTRLVSM|VSM)EH$"}, 0.11f, 0f, 0.07f, "viseme EE", 0.32f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_PuckerLips"}, 0.11f, 0f, 0.07f, "PuckerLips", 0.419f, true);
				AddShapeComponent(new[] {"^.*_MouthTH"}, 0.11f, 0f, 0.07f, "MouthTH", 1f, true);


				NewExpression("ow");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)OW$"}, 0.11f, 0f, 0.07f, "viseme OW", 1.0f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)LipsPart$"}, 0.11f, 0f, 0.07f, "lips part", 0.54f, true);
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)AA$"}, 0.11f, 0f, 0.07f, "eCTRLvAA", 0.33f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_MouthO"}, 0.11f, 0f, 0.07f, "MouthO", 1f, true);
				AddShapeComponent(new[] {"^.*_StretchLips"}, 0.11f, 0f, 0.07f, "StretchLips", 0.538f, true);


				NewExpression("ee");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)EE$", "^.*_(eCTRLv|CTRLVSM|VSM)EH$"}, 0.11f, 0f, 0.07f, "viseme EE", 1f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthSmile(Simple)?$"}, 0.11f, 0f, 0.07f, "smile simple", 0.45f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)LipsPart$"}, 0.11f, 0f, 0.07f, "lips part", 0.30f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_PuckerLipsWide"}, 0.11f, 0f, 0.07f, "PuckerLipsWide", 0.762f, true);
				AddShapeComponent(new[] {"^.*_StretchLips"}, 0.11f, 0f, 0.07f, "StretchLips", 1f, true);
				AddShapeComponent(new[] {"^.*_MouthSpeak"}, 0.11f, 0f, 0.07f, "MouthSpeak", 0.36f, true);


				NewExpression("oo");
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)OW$"}, 0.11f, 0f, 0.07f, "visem OW", 0.62f, true);
				AddShapeComponent(new[] {"^.*_(eCTRLv|CTRLVSM|VSM)AA$"}, 0.11f, 0f, 0.07f, "eCTRLvAA", 0.45f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)LipsPart$"}, 0.11f, 0f, 0.07f, "lips part", 0.85f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthOpen$"}, 0.11f, 0f, 0.07f, "mouth open", 0.40f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthSmile$"}, 0.11f, 0f, 0.07f, "smile", 0.46f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_PuckerLipsOOO"}, 0.11f, 0f, 0.07f, "PuckerLipsOOO", 0.537f, true);
				AddShapeComponent(new[] {"^.*_MouthYell"}, 0.11f, 0f, 0.07f, "MouthYell", 0.795f, true);

			}
			#endregion // SALSA-configuration

			#region EmoteR-Configuration

			NewConfiguration(OneClickConfiguration.ConfigType.Emoter);
			{
				////////////////////////////////////////////////////////
				// SMR regex searches (enable/disable/add as required).
				AddSmrSearch("^genesis([238])?(fe)?(male)?\\.shape$");
				AddSmrSearch("^genesis[238](fe)?maleeyelashes\\.shape$");
				AddSmrSearch("^emotiguy.*\\.shape$");

				useRandomEmotes = true;
				isChancePerEmote = true;
				numRandomEmotesPerCycle = 0;
				randomEmoteMinTimer = 1f;
				randomEmoteMaxTimer = 2f;
				randomChance = 0.5f;
				useRandomFrac = false;
				randomFracBias = 0.5f;
				useRandomHoldDuration = false;
				randomHoldDurationMin = 0.1f;
				randomHoldDurationMax = 0.5f;


				NewExpression("exasper");
				AddEmoteFlags(false, true, false, 1f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)CheeksBalloonPucker$"}, 0.25f, 0.1f, 0.5f, "cheek pucker", 0.35f, true);
				AddShapeComponent(new[] {"^.*_(e)?CTRLCheek(s)?Flex(-Slack)?$"}, 0.25f, 0.1f, 0.5f, "cheeks flex", 0.50f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_EyesWide"}, 0.25f, 0.1f, 0.5f, "^.*_EyesWide", 0.6f, true);


				NewExpression("soften");
				AddEmoteFlags(false, true, false, 0.751f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthSmile(Simple)?$"}, 0.25f, 0.1f, 0.5f, "smile", 0.65f, true);
				AddShapeComponent(new[] {"^.*_(e)?CTRLEyeLids(BottomUp|LowerUpDown)$"}, 0.25f, 0.1f, 0.5f, "bottom lids up", .50f, true);
				// emotiguy
				AddEmoteFlags(false, true, false, 0.694f);
				AddShapeComponent(new[] {"^.*_Smile"}, 0.25f, 0.1f, 0.5f, "smile", 0.6f, true);


				NewExpression("browsUp");
				AddEmoteFlags(false, true, false, 0.6f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)Shock(_HD)?$", "^.*_(e)?CTRLBrowUp(-Down)?$"}, 0.25f, 0.1f, 0.5f, "brows up", 0.55f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_DontTell"}, 0.25f, 0.1f, 0.5f, "^.*_DontTell", 0.43f, true);


				NewExpression("browUp");
				AddEmoteFlags(false, true, false, .808f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)BrowUp(-Down)?R$"}, 0.2f, 0.1f, 0.5f, "browR up", 0.45f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)EyesSquint(-Widen)?L$"}, 0.2f, 0.1f, 0.5f, "eye squintL", 0.30f, true);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)MouthSmileSimpleL$"}, 0.2f, 0.1f, 0.5f, "smileL", 0.75f, true);
				// not avail on gen3
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)Eyelids(Top|Upper)Up(-Down)?R$"}, 0.2f, 0.1f, 0.5f, "lidR up", 0.20f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_Brows-Tilt_r"}, 0.25f, 0.1f, 0.5f, "Brows-Tilt_r", 0.569f, true);
				AddShapeComponent(new[] {"^.*_Brows-Tilt_l"}, 0.25f, 0.1f, 0.5f, "Brows-Tilt_l", 0.652f, true);
				AddShapeComponent(new[] {"^.*_Brows-UpDown_r"}, 0.25f, 0.1f, 0.5f, "Brows-UpDown_r", 0.777f, true);
				AddShapeComponent(new[] {"^.*_EyesWide"}, 0.25f, 0.5f, 0.2f, "EyesWide", 0.229f, true);


				NewExpression("squint");
				AddEmoteFlags(false, true, false, 1f);
				AddShapeComponent(new[] {"^.*_(e)?CTRLEyesSquint(-Widen)?$"}, 0.2f, 0.1f, 0.5f,"eyes squint", 0.4f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_Wink L"}, 0.25f, 0.1f, 0.5f, "Wink L", 0.174f, true);
				AddShapeComponent(new[] {"^.*_Wink R"}, 0.25f, 0.1f, 0.5f, "Wink R", 0.247f, true);


				NewExpression("focus");
				AddEmoteFlags(false, true, false, 0.766f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)Cheek(s)?EyeFlex$"}, 0.2f, 0.1f, 0.5f, "eyes flex",0.85f, true);
				// emotiguy
				AddShapeComponent(new[] {"^.*_Nerd"}, 0.25f, 0.1f, 0.5f, "Nerd", 0.6f, true);


				NewExpression("scrunch");
				AddEmoteFlags(false, true, false, 1f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)NoseWrinkle$"}, 0.2f, 0.1f, 0.5f,"nose wrinkle", .45f, true);
				// emotiguy none


				NewExpression("flare");
				AddEmoteFlags(false, true, false, 1f);
				AddShapeComponent(new[] {"^.*_(e)?(CTRL|PHM)Nostrils(Flex|Flare)$"}, 0.2f, 0.1f, 0.5f, "nostrils flex", 0.60f, true);
				AddShapeComponent(new[] {"^.*_(e)?CTRLCheek(s)?Flex(-Slack)?$"}, 0.25f, 0.1f, 0.5f, "cheeks flex", 0.50f, true);
				// emotiguy none

			}
			#endregion // EmoteR-configuration

			DoOneClickiness(gameObject);

			if (selectedObject.GetComponent<SalsaAdvancedDynamicsSilenceAnalyzer>() == null)
				selectedObject.AddComponent<SalsaAdvancedDynamicsSilenceAnalyzer>();

			//Darrin's Tweaks
			salsa.globalFrac = 1f;
			salsa.useTimingsOverride = true;
			salsa.globalDurON = 0.11f;
			salsa.globalDurOffBalance = -0.09f;
			salsa.globalNuanceBalance = -0.09f;


			emoter.NumRandomEmphasizersPerCycle = 2;
			EmoteExpression emote;
			emote = emoter.FindEmote("soften");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("browsUp");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("browUp");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("puff");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("scrunch");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("squint");
			if (emote != null)
				emote.frac = 1f;
			emote = emoter.FindEmote("focus");
			if (emote != null)
				emote.frac = 1f;

			var silenceAnalyzer = selectedObject.GetComponent<SalsaAdvancedDynamicsSilenceAnalyzer>();
			if (silenceAnalyzer)
			{
				silenceAnalyzer.silenceThreshold = 0.9f;
				silenceAnalyzer.timingStartPoint = 0.25f;
				silenceAnalyzer.timingEndVariance = 0.8f;
				silenceAnalyzer.silenceSampleWeight = 0.95f;
				silenceAnalyzer.bufferSize = 512;
			}
		}
	}
}