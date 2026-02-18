using Adrenak.RNNoise4Unity;
using UnityEngine;
using System;

using MetaVoiceChat.Input;

namespace MetaVoiceChat.Rnnoise
{
    public class RnnoiseVcInputFilter : VcInputFilter
    {
        [SerializeField] private int filterCount = 5;
        
        public MetaVc metaVc;

        private const int DenoiserFramesize = 480;

        private Denoiser denoiser;
        private int multiples = 0;

        private readonly float[] buffer = new float[DenoiserFramesize];

        private void OnEnable()
        {
            if (metaVc == null)
            {
                Debug.LogError("MetaVc is not assigned. Please assign it in the inspector.");
                return;
            }

            var config = metaVc.config;
            if (config.samplesPerFrame % DenoiserFramesize != 0)
            {
                Debug.LogError($"RnnoiseVcInputFilter requires samplesPerFrame to be a multiple of {DenoiserFramesize}. Please adjust the configuration.");
                return;
            }

            multiples = config.samplesPerFrame / DenoiserFramesize;
            denoiser = new Denoiser();
        }

        private void OnDisable()
        {
            denoiser?.Dispose();
            denoiser = null;
            multiples = 0;
        }

        protected override void Filter(int index, ref float[] samples)
        {
            if (denoiser == null || multiples == 0)
            {
                return;
            }

            if (samples == null || samples.Length == 0)
            {
                return;
            }

            if (samples.Length != multiples * DenoiserFramesize)
            {
                Debug.LogWarning($"RnnoiseVcInputFilter requires samples to be of length {multiples * DenoiserFramesize}. Please adjust the configuration.");
                return;
            }

            for (int iteration = 0; iteration < filterCount; iteration++)
            {
                for (int i = 0; i < multiples; i++)
                {
                    //var buffer = FixedLengthArrayPool<float>.Rent(DenoiserFramesize);

                    // Copy the samples into the buffer
                    Array.Copy(samples, i * DenoiserFramesize, buffer, 0, DenoiserFramesize);

                    denoiser.Denoise(buffer);

                    // Copy the denoised samples back to the original samples array
                    Array.Copy(buffer, 0, samples, i * DenoiserFramesize, DenoiserFramesize);

                    //FixedLengthArrayPool<float>.Return(buffer);
                }
            }
        }
    }
}