using System.Collections;
using CrazyMinnow.SALSA;
using UnityEngine;

namespace CrazyMinnow
{
    [AddComponentMenu( "Crazy Minnow Studio/SALSA LipSync/Add-ons/SalsaVoiceLink" )]
    public class SalsaVoiceLink : MonoBehaviour
    {
        public bool isDebug = false;
        public bool useLocalLipSync = false;
        [Range(0f, 10f)] public float amplifyMultipleExperimental = 1.0f;

        //private VoicePlayerState playerState;
        private bool isPlayerStateReady = false;
        //private DissonanceComms dissonanceComms;
        //private IDissonancePlayer dissonancePlayer;
        private Salsa salsa;
        private IEnumerator coroAudioSourceLinkage;
        private const float PollTimer = .5f;

        private void OnEnable()
        {
            salsa = transform.GetComponentInChildren<Salsa>();
            salsa.useExternalAnalysis = true;
            //dissonancePlayer = transform.GetComponentInChildren<IDissonancePlayer>();
            //dissonanceComms = FindObjectOfType<DissonanceComms>();

            if ( !salsa )
                Debug.LogError( "[" + GetType().Name + "] SALSA was not found on the player object." );
            //if ( dissonancePlayer == null )
            //    Debug.LogError( "[" + GetType().Name + "] an IDissonancePlayer component was not found on the player object." );
            //if ( dissonanceComms == null )
            //    Debug.LogError( "[" + GetType().Name + "] the DissonanceComms component was not found in the scene." );

            salsa.getExternalAnalysis = SalsaDissonanceLinkExternalAnalysis;


            if ( coroAudioSourceLinkage != null )
            {
                StopCoroutine(coroAudioSourceLinkage);
            }
            coroAudioSourceLinkage = WaitSalsaDissonanceLink();
            StartCoroutine(coroAudioSourceLinkage);
        }

        /// <summary>
        /// SALSA will poll this computation according to its normal update delay cycle.
        /// </summary>
        /// <returns></returns>
        private float SalsaDissonanceLinkExternalAnalysis()
        {
            if (!isPlayerStateReady)
                return 0f;

            //if (!playerState.IsSpeaking)
            //    return 0f;
            
            //if ( dissonancePlayer.Type == NetworkPlayerType.Local && !useLocalLipSync )
            //    return 0f;

            //return playerState.Amplitude * amplifyMultipleExperimental;
            return 0f;
        }

        private IEnumerator WaitSalsaDissonanceLink()
        {
            var timeCheck = Time.time;

            while ( !isPlayerStateReady )
            {
                if ( Time.time - timeCheck > PollTimer )
                {
                    timeCheck = Time.time;

                    //if ( isDebug )
                    //    Debug.Log("[" + GetType().Name + "] - Looking for PlayerState for player ID:  " + dissonancePlayer.PlayerId);

                    //if (dissonancePlayer.PlayerId == null)
                        continue;

                    //playerState = dissonanceComms.FindPlayer(dissonancePlayer.PlayerId);
                    //if (playerState != null)
                    //    isPlayerStateReady = true;
                }

                yield return null;
            }

            if ( isDebug )
                Debug.Log("[" + GetType().Name + "] - SALSA and Dissonance are linked.");
        }
    }
}
