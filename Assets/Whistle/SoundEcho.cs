using UnityEngine;

namespace WhistlePOC
{
    // Each sound emits once. Geometry appears when the wavefront reaches its position.
    public sealed class SoundEcho : MonoBehaviour
    {
        public const int Capacity = 24;
        public Material mapMaterial;
        public LineRenderer[] waves;
        [Header("Sound strength and reveal radius")]
        [Min(.1f)] public float minimumRadius = .5f;
        [Min(.1f)] public float maximumRadius = 4.5f;
        [Min(.1f)] public float speed = 12f;
        [Min(0)] public float holdDuration = .04f;
        [Min(.01f)] public float fadeDuration = .2f;
        [Range(0, 1)] public float idleVisibility = 0;
        readonly Vector4[] origins = new Vector4[Capacity];
        readonly Vector4[] properties = new Vector4[Capacity];
        Material instance;
        float clock;
        int next, count;
        public bool Paused { get; set; }

        void OnEnable() => SoundEvents.EmittedWithRange += EmitSound;
        void OnDisable() => SoundEvents.EmittedWithRange -= EmitSound;
        void Update() { if (instance != null && !Paused) Tick(Time.deltaTime); }

        public void Initialize(LineRenderer[] contours, BoxCollider[] blockers = null)
        {
            if (instance == null) instance = new Material(mapMaterial);
            foreach (var contour in contours) contour.sharedMaterial = instance;
            var minimums = new Vector4[64]; var maximums = new Vector4[64]; int blockerCount = 0;
            if (blockers != null) foreach (var blocker in blockers)
            {
                if (blocker == null || !blocker.enabled || blockerCount == 64) continue;
                Bounds bounds = blocker.bounds; minimums[blockerCount] = bounds.min; maximums[blockerCount] = bounds.max; blockerCount++;
            }
            instance.SetVectorArray("_BlockerMin", minimums); instance.SetVectorArray("_BlockerMax", maximums); instance.SetInt("_BlockerCount", blockerCount);
        }
        public void ResetWaves()
        {
            clock = 0; next = count = 0;
            for (int i = 0; i < Capacity; i++) { origins[i] = new Vector4(0, 0, 0, -1000); properties[i] = Vector4.zero; }
            foreach (var wave in waves) wave.enabled = false;
            SetScanStrength(0); Upload();
        }
        public void EmitSound(Vector3 position, float strength, float radiusLimit = 0)
        {
            if (instance == null || strength <= 0) return;
            strength = Mathf.Clamp01(strength);
            float radius = Mathf.Lerp(minimumRadius, Mathf.Max(minimumRadius, maximumRadius), strength);
            if (radiusLimit > 0) radius = Mathf.Min(radius, radiusLimit);
            origins[next] = new Vector4(position.x, position.y, position.z, clock);
            properties[next] = new Vector4(radius, Mathf.Lerp(.55f, 1f, strength), 0, 0);
            var wave = waves[next]; wave.transform.position = position + Vector3.up * .03f;
            wave.transform.rotation = Quaternion.identity; wave.transform.localScale = new Vector3(.001f, 1, .001f);
            wave.enabled = true; next = (next + 1) % Capacity; count = Mathf.Min(count + 1, Capacity);
            Upload();
        }
        public void Tick(float dt)
        {
            clock += dt;
            for (int i = 0; i < waves.Length; i++)
            {
                float age = clock - origins[i].w;
                float expansion = age * speed;
                float radius = properties[i].x;
                var wave = waves[i]; wave.enabled = radius > 0 && age >= 0 && expansion < radius;
                if (!wave.enabled) continue;
                wave.transform.localScale = new Vector3(expansion, 1, expansion);
                float alpha = Mathf.Clamp01(age / .035f) * Mathf.Clamp01((radius - expansion) / Mathf.Max(.1f, radius * .3f));
                wave.startColor = wave.endColor = new Color(1, 1, 1, alpha * .65f * properties[i].y);
            }
            Upload();
        }
        public void SetScanStrength(float strength)
        { if (instance != null) instance.SetFloat("_ScanStrength", strength); }
        void Upload()
        {
            if (instance == null) return;
            instance.SetVectorArray("_SoundOrigins", origins); instance.SetVectorArray("_SoundProperties", properties);
            instance.SetInt("_WaveCount", count); instance.SetFloat("_WaveClock", clock);
            instance.SetFloat("_IdleVisibility", idleVisibility);
            instance.SetVector("_WaveParameters", new Vector4(0, speed, holdDuration, fadeDuration));
        }
        void OnDestroy() { if (instance != null) Destroy(instance); }
    }
}
