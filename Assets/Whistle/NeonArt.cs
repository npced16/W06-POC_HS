using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WhistlePOC
{
    // Decorative scenery never participates in echo detection or combat.
    public sealed class NeonArt
    {
        public static readonly Color Cyan = new Color(.08f, .9f, 1);
        public static readonly Color Pink = new Color(1, .08f, .48f);
        public static readonly Color Yellow = new Color(1, .85f, .12f);
        readonly List<Transform> gates = new List<Transform>();
        readonly List<LineRenderer> lines = new List<LineRenderer>();
        readonly List<Color> colors = new List<Color>();
        readonly Material material;
        readonly VolumeProfile profile;
        readonly Bloom bloom;
        readonly Vignette vignette;
        readonly Camera camera;
        readonly Quaternion homeRotation;
        readonly float homeFov;
        readonly MaterialPropertyBlock tint = new MaterialPropertyBlock();

        public NeonArt(FirstPersonStage stage)
        {
            camera = stage.View; homeRotation = camera.transform.localRotation; homeFov = camera.fieldOfView;
            var root = new GameObject("Neon Pulse Avenue").transform; root.SetParent(stage.transform, false);
            material = new Material(stage.scanWavePrefab.GetComponent<LineRenderer>().sharedMaterial);
            material.name = "Runtime Neon Glow";
            for (int side = -1; side <= 1; side += 2)
            {
                AddLine(root, "Runway", Cyan, .045f, false, new Vector3(side * 3.3f, .015f, 1), new Vector3(side * 3.3f, .015f, 56));
                AddLine(root, "Outer Runway", Pink, .025f, false, new Vector3(side * 4.8f, .01f, 1), new Vector3(side * 4.8f, .01f, 56));
            }
            for (int i = 0; i < 12; i++)
            {
                var gate = new GameObject("Pulse Gate " + i).transform; gate.SetParent(root, false); gates.Add(gate);
                var color = i % 2 == 0 ? Cyan : Pink;
                AddLine(gate, "Arch", color, .045f, false, new Vector3(-5, 0, 0), new Vector3(-5, 5.4f, 0), new Vector3(5, 5.4f, 0), new Vector3(5, 0, 0));
                AddLine(gate, "Floor Beat", color, .018f, false, new Vector3(-4.8f, .01f, 0), new Vector3(4.8f, .01f, 0));
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 6.2f;
                    AddLine(gate, "Pop Diamond", i % 3 == 0 ? Yellow : color, .05f, true,
                        new Vector3(x, 1.8f, 0), new Vector3(x + .7f, 2.5f, 0), new Vector3(x, 3.2f, 0), new Vector3(x - .7f, 2.5f, 0));
                    AddLine(gate, "Arrow", color, .06f, false, new Vector3(side * 3.8f, .015f, .6f), new Vector3(side * 4.25f, .015f, 0), new Vector3(side * 3.8f, .015f, -.6f));
                }
            }
            var sign = new GameObject("ECHO Neon Sign").transform; sign.SetParent(root, false); sign.localPosition = new Vector3(0, 4.1f, 40);
            var text = sign.gameObject.AddComponent<TextMeshPro>(); text.text = "E C H O"; text.fontSize = 16; text.alignment = TextAlignmentOptions.Center; text.color = Pink;
            text.rectTransform.sizeDelta = new Vector2(18, 3);
            AddLine(root, "Horizon Diamond", Pink, .07f, true, new Vector3(0, 1, 48), new Vector3(3, 4, 48), new Vector3(0, 7, 48), new Vector3(-3, 4, 48));

            if (UniversalRenderPipeline.asset != null)
            {
                camera.allowHDR = true;
                var data = camera.GetComponent<UniversalAdditionalCameraData>() ?? camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true; data.volumeLayerMask |= 1;
                var volume = root.gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 20;
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); volume.sharedProfile = profile;
                bloom = profile.Add<Bloom>(true); bloom.threshold.value = .65f; bloom.intensity.value = .85f; bloom.scatter.value = .65f;
                vignette = profile.Add<Vignette>(true); vignette.intensity.value = .25f; vignette.smoothness.value = .65f; vignette.color.value = new Color(.025f, .005f, .055f);
            }
            foreach (var label in stage.Game.GetComponentsInChildren<TMP_Text>(true))
                label.color = label.name.Contains("Title") ? Pink : new Color(.88f, .94f, 1);
            foreach (var image in stage.Game.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                bool button = image.GetComponent<UnityEngine.UI.Button>() != null;
                image.color = button ? new Color(.28f, .04f, .3f, .98f) : image.type == UnityEngine.UI.Image.Type.Filled ? Cyan : new Color(.035f, .018f, .09f, .96f);
            }
        }

        void AddLine(Transform parent, string name, Color color, float width, bool loop, params Vector3[] points)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>(); line.transform.SetParent(parent, false);
            line.sharedMaterial = material; line.useWorldSpace = false; line.loop = loop; line.positionCount = points.Length; line.SetPositions(points);
            line.widthMultiplier = width; line.numCornerVertices = 2; line.startColor = line.endColor = Color.white;
            lines.Add(line); colors.Add(color);
        }

        public void Tick(WhistleGame game, float swing)
        {
            bool walking = game.State == JourneyState.Walking;
            float beat = Mathf.Pow(.5f + .5f * Mathf.Cos(game.Clock * Mathf.PI * 4), 5);
            float energy = .26f + beat * .1f + (game.Revealed ? .35f : 0) + game.PerfectFlash * .4f;
            for (int i = 0; i < gates.Count; i++) gates[i].localPosition = new Vector3(0, 0, 4 + Mathf.Repeat(i * 4.5f - game.Progress * 70, 54));
            for (int i = 0; i < lines.Count; i++)
            {
                var c = Color.Lerp(colors[i], Yellow, game.PerfectFlash * .6f) * (energy * 2.4f); c.a = 1;
                tint.SetColor("_BaseColor", c); tint.SetColor("_Color", c); lines[i].SetPropertyBlock(tint);
            }
            camera.backgroundColor = Color.Lerp(new Color(.012f, .004f, .035f), new Color(.025f, .008f, .075f), game.Revealed ? 1 : 0);
            float blend = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10);
            camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, homeFov + (walking ? beat * .55f : 0) - (game.Revealed ? 2 : 0) + swing * 2 + game.PerfectFlash * 3, blend);
            float roll = walking ? Mathf.Sin(game.Clock * 2.1f) * .18f + swing * .65f : 0;
            if (game.HitFlash > .25f) roll += Mathf.Sin(game.Clock * 55) * game.HitFlash * .9f;
            camera.transform.localRotation = homeRotation * Quaternion.Euler(0, 0, roll);
            if (bloom != null) bloom.intensity.value = .75f + (game.Revealed ? .35f : 0) + game.PerfectFlash * .7f;
            if (vignette != null) vignette.intensity.value = .25f + game.HitFlash * .12f;
        }

        public void Dispose()
        {
            Object.Destroy(material);
            if (profile != null) { foreach (var component in profile.components) Object.Destroy(component); Object.Destroy(profile); }
        }
    }
}
