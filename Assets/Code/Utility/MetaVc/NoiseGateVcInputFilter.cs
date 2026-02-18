using System;
using MetaVoiceChat;
using MetaVoiceChat.Input;
using UnityEngine;

namespace Code.Utility.MetaVc
{
    public class NoiseGateVcInputFilter: VcInputFilter
    {
        public MetaVoiceChat.MetaVc metaVc;
        
        [Range(-60f, -10f)]
        public float noiseGateThreshold = -40f;
        
        [Range(0.01f, 1f)]
        public float gateAttackTime = 0.15f;
        
        [Range(0.01f, 1f)]
        public float gateReleaseTime = 0.3f;
        
        private float _gate = 0f;
        
        protected override void Filter(int index, ref float[] samples)
        {
            Process(samples, VcConfig.SamplesPerSecond);
        }
        
        public void Process(ArraySegment<float> inputSamples, int frequency, float strength = 1f)
        {
            float rms = 0f;
            for (int i = 0; i < inputSamples.Count; i++)
            {
                float s = inputSamples.Array[inputSamples.Offset + i];
                rms += s * s;
            }
            rms = Mathf.Sqrt(rms / inputSamples.Count);
            float db = 20f * Mathf.Log10(Mathf.Max(rms, 1e-10f));
            bool gateOpen = db > noiseGateThreshold;

            float deltaTime = inputSamples.Count / (float)frequency;

            if (gateOpen)
            {
                float attackTime = Mathf.Max(gateAttackTime, 0.0001f);
                _gate = Mathf.Clamp01(_gate + (deltaTime / attackTime));
            }
            else
            {
                float releaseTime = Mathf.Max(gateReleaseTime, 0.0001f);
                _gate = Mathf.Clamp01(_gate - (deltaTime / releaseTime));
            }

            float gain = _gate * strength;

            for (int i = 0; i < inputSamples.Count; i++)
                inputSamples.Array[inputSamples.Offset + i] *= gain;
        }
    }
}