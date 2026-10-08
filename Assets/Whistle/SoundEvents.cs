using System;
using UnityEngine;

namespace WhistlePOC
{
    // Any actor or environment object can publish a sound without knowing the visual system.
    public static class SoundEvents
    {
        public static event Action<Vector3, float> Emitted;
        public static event Action<Vector3, float, float> EmittedWithRange;

        /// <param name="strength">Sound strength from 0 (silent) to 1 (loudest).</param>
        public static void Emit(Vector3 position, float strength) => Emit(position, strength, 0);

        public static void Emit(Vector3 position, float strength, float radiusLimit)
        {
            strength = Mathf.Clamp01(strength);
            if (strength > 0) { Emitted?.Invoke(position, strength); EmittedWithRange?.Invoke(position, strength, radiusLimit); }
        }

        // Keep playback and its reveal event together, using the same sound strength.
        public static void Play(AudioSource source, AudioClip clip, Vector3 position, float strength) => Play(source, clip, position, strength, 0);

        public static void Play(AudioSource source, AudioClip clip, Vector3 position, float strength, float radiusLimit)
        {
            strength = Mathf.Clamp01(strength);
            if (source == null || clip == null || strength <= 0) return;
            source.PlayOneShot(clip, strength); Emit(position, strength, radiusLimit);
        }
    }
}
