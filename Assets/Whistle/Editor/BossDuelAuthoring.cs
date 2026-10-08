using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace WhistlePOC.Editor
{
    public static class BossDuelAuthoring
    {
        const string ScenePath = "Assets/Scenes/WhistlePOC.unity";
        const string AssetRoot = "Assets/Whistle/BossAssets";
        static Material architecture, ink, bossMaterial;
        static readonly List<BoxCollider> walls = new List<BoxCollider>();
        static readonly List<Renderer> building = new List<Renderer>();
        static readonly List<LineRenderer> buildingLines = new List<LineRenderer>();

        [MenuItem("Whistle POC/Open boss duel")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEngine.Object.FindFirstObjectByType<BossDuel>() != null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Whistle POC/Rebuild boss duel")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            if (SceneManager.GetActiveScene().isDirty) throw new InvalidOperationException("Save current scene before rebuilding.");
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/WhistleEscort.unity"))
                AssetDatabase.CopyAsset(ScenePath, "Assets/Scenes/WhistleEscort.unity");
            Directory.CreateDirectory(AssetRoot); AssetDatabase.Refresh();
            architecture = MakeMaterial("Building", Color.black);
            bossMaterial = MakeMaterial("Hunter", Color.black);
            ink = MakeLineMaterial();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Boss Duel"); var game = root.AddComponent<BossDuel>();
            var input = Child(root.transform, "Duel Input System").AddComponent<DuelInputSystem>();
            input.actions = MakeInputActions(); game.input = input;
            var savedMap = AssetDatabase.LoadAssetAtPath<GameObject>(DuelMapPrefabAuthoring.MapPath);
            if (savedMap != null)
            {
                var mapInstance = (GameObject)PrefabUtility.InstantiatePrefab(savedMap, root.transform);
                game.map = mapInstance.GetComponent<DuelMap>(); game.map.Bind(game);
            }
            else
            {
            var map = Child(root.transform, "Building — four rooms and connecting doors");
            walls.Clear(); building.Clear(); buildingLines.Clear();
            Box(map.transform, "Floor", new Vector3(0, -.2f, 0), new Vector3(24, .4f, 24), false);
            Box(map.transform, "Ceiling", new Vector3(0, 4.4f, 0), new Vector3(24, .4f, 24), false);
            Box(map.transform, "West wall", new Vector3(-12, 2, 0), new Vector3(.4f, 4.4f, 24), true);
            Box(map.transform, "East wall", new Vector3(12, 2, 0), new Vector3(.4f, 4.4f, 24), true);
            Box(map.transform, "North wall", new Vector3(0, 2, 12), new Vector3(24, 4.4f, .4f), true);
            Box(map.transform, "South wall", new Vector3(0, 2, -12), new Vector3(24, 4.4f, .4f), true);
            foreach (float center in new[] { -9.5f, 0, 9.5f })
            {
                float length = center == 0 ? 8 : 5;
                Box(map.transform, "Room partition X", new Vector3(0, 2, center), new Vector3(.4f, 4, length), true);
                Box(map.transform, "Room partition Z", new Vector3(center, 2, 0), new Vector3(length, 4, .4f), true);
            }
            foreach (float x in new[] { -8f, 8f }) foreach (float z in new[] { -8f, 8f })
                IndustrialStack(map.transform, new Vector3(x, 2, z));
            foreach (float x in new[] { -10.6f, 10.6f })
                Cylinder(map.transform, "Ceiling steam main", new Vector3(x, 3.5f, 0), .22f, 22, Quaternion.Euler(90, 0, 0));
            foreach (float z in new[] { -10.6f, 10.6f })
                Cylinder(map.transform, "Ceiling return pipe", new Vector3(0, 3.6f, z), .16f, 22, Quaternion.Euler(0, 0, 90));
            // Dim door frames remain readable between scans without revealing the opponent.
            foreach (float opening in new[] { -5.5f, 5.5f })
            {
                DoorLine(map.transform, new Vector3(-.24f, 0, opening - 1.5f), new Vector3(-.24f, 3.3f, opening - 1.5f), new Vector3(-.24f, 3.3f, opening + 1.5f), new Vector3(-.24f, 0, opening + 1.5f));
                DoorLine(map.transform, new Vector3(opening - 1.5f, 0, -.24f), new Vector3(opening - 1.5f, 3.3f, -.24f), new Vector3(opening + 1.5f, 3.3f, -.24f), new Vector3(opening + 1.5f, 0, -.24f));
            }
            game.walls = walls.ToArray(); game.building = building.ToArray(); game.buildingContours = buildingLines.ToArray();
            }
            var hero = Child(root.transform, "Player — Black Swordsman"); hero.transform.position = new Vector3(-5.5f, .05f, -7);
            game.player = hero.AddComponent<CharacterController>(); game.player.height = 1.8f; game.player.radius = .35f; game.player.center = Vector3.up * .9f; game.player.stepOffset = .25f;
            var camera = Child(hero.transform, "Main Camera"); camera.tag = "MainCamera"; camera.transform.localPosition = Vector3.up * 1.65f;
            game.view = camera.AddComponent<Camera>(); game.view.fieldOfView = 78; game.view.nearClipPlane = .05f;
            game.view.clearFlags = CameraClearFlags.SolidColor; game.view.backgroundColor = Color.black;
            camera.AddComponent<AudioListener>();
            game.playerAudio = camera.AddComponent<AudioSource>(); game.playerAudio.playOnAwake = false; game.playerAudio.spatialBlend = 0;
            var sword = Child(camera.transform, "First Person Sword"); game.sword = sword.transform;
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube); blade.name = "Blade"; blade.transform.SetParent(sword.transform, false);
            blade.transform.localPosition = new Vector3(0, .28f, 0); blade.transform.localScale = new Vector3(.025f, .65f, .025f);
            UnityEngine.Object.DestroyImmediate(blade.GetComponent<Collider>()); blade.GetComponent<Renderer>().sharedMaterial = bossMaterial;
            var swordLines = new List<LineRenderer>(); swordLines.Add(BoxOutline(blade));
            var guard = GameObject.CreatePrimitive(PrimitiveType.Cube); guard.name = "Guard"; guard.transform.SetParent(sword.transform, false);
            guard.transform.localScale = new Vector3(.18f, .035f, .05f); UnityEngine.Object.DestroyImmediate(guard.GetComponent<Collider>()); guard.GetComponent<Renderer>().sharedMaterial = bossMaterial;
            swordLines.Add(BoxOutline(guard)); game.swordContours = swordLines.ToArray();
            foreach (var line in swordLines) line.widthMultiplier = .0015f;
            game.slashContours = MakeSlash(camera.transform);
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Whistle/SceneAssets/Prefabs/Armored Enemy.prefab"), root.transform);
            boss.name = "Hunter Boss"; boss.transform.position = new Vector3(-5.5f, 0, 6); boss.transform.localScale = Vector3.one * 1.15f;
            game.boss = boss.transform; var visual = boss.GetComponent<PooledEnemyVisual>(); game.bossWeapon = visual.weapon;
            game.bossBody = visual.body; game.bossContours = visual.contours;
            foreach (var line in game.bossContours) line.sharedMaterial = ink;
            foreach (var r in game.bossBody) r.sharedMaterial = bossMaterial;
            foreach (var line in visual.footsteps.Concat(visual.falseFootsteps)) line.gameObject.SetActive(false);
            foreach (var collider in boss.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            game.bossAudio = boss.AddComponent<AudioSource>(); game.bossAudio.playOnAwake = false;
            game.bossAudio.spatialBlend = 1; game.bossAudio.rolloffMode = AudioRolloffMode.Logarithmic; game.bossAudio.minDistance = 2; game.bossAudio.maxDistance = 28; game.bossAudio.dopplerLevel = 0;
            game.occlusion = boss.AddComponent<AudioLowPassFilter>();
            var traces = Child(root.transform, "Footprint pool"); game.footprints = new LineRenderer[40];
            for (int i = 0; i < game.footprints.Length; i++)
            {
                var trace = Child(traces.transform, "Footprint " + i).AddComponent<LineRenderer>(); trace.sharedMaterial = ink; trace.useWorldSpace = false;
                trace.loop = true; trace.widthMultiplier = .035f; trace.positionCount = 6;
                trace.SetPositions(new[] { new Vector3(-.08f, 0, -.16f), new Vector3(-.09f, 0, .1f), new Vector3(-.04f, 0, .2f), new Vector3(.07f, 0, .16f), new Vector3(.09f, 0, -.1f), new Vector3(.03f, 0, -.18f) });
                trace.enabled = false; game.footprints[i] = trace;
            }
            game.footstep = Clip("Footstep"); game.scanSound = Clip("Whistle"); game.slashSound = Clip("Sword"); game.impactSound = Clip("Impact");
            game.counterSound = Tone("Counter", 950, 1, .23f, 1);
            game.cues = new[] { Tone("Sweep — metal", 620, 1, .55f, .55f), Tone("Overhead — two low beats", 145, 2, .8f, .8f), Tone("Rush — three rising beats", 340, 3, .95f, 1.8f) };
            SoundEchoAuthoring.Configure(game);
            DuelPresentationAuthoring.Configure(game);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets(); Selection.activeGameObject = root;
            Debug.Log("BOSS DUEL: UI-free monochrome scene saved; input events connected.");
        }
        static GameObject Child(Transform parent, string name)
        { var obj = new GameObject(name); obj.transform.SetParent(parent, false); return obj; }
        static InputActionAsset MakeInputActions()
        {
            string path = AssetRoot + "/DuelControls.inputactions";
            var existing = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path); if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<InputActionAsset>(); asset.name = "DuelControls";
            var map = asset.AddActionMap("Duel");
            var move = map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            map.AddAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            map.AddAction("Attack", InputActionType.Button, "<Mouse>/leftButton"); map.AddAction("Parry", InputActionType.Button, "<Mouse>/rightButton");
            map.AddAction("Scan", InputActionType.Button, "<Keyboard>/space"); map.AddAction("Dodge", InputActionType.Button, "<Keyboard>/leftShift");
            map.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape"); map.AddAction("Restart", InputActionType.Button, "<Keyboard>/r"); map.AddAction("Confirm", InputActionType.Button, "<Keyboard>/enter");
            File.WriteAllText(path, asset.ToJson()); UnityEngine.Object.DestroyImmediate(asset); AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        static Material MakeMaterial(string name, Color color)
        {
            string path = AssetRoot + "/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); EditorUtility.SetDirty(material); return material;
        }
        static Material MakeLineMaterial()
        {
            string path = AssetRoot + "/Echo Lines.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000; EditorUtility.SetDirty(mat); return mat;
        }
        static void Box(Transform parent, string name, Vector3 position, Vector3 size, bool wall)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = name; box.transform.SetParent(parent, false); box.transform.position = position; box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = architecture; building.Add(box.GetComponent<Renderer>());
            if (wall) walls.Add(box.GetComponent<BoxCollider>());
            buildingLines.Add(BoxOutline(box));
        }
        static LineRenderer BoxOutline(GameObject box)
        {
            var outline = box.AddComponent<LineRenderer>(); outline.sharedMaterial = ink; outline.useWorldSpace = false; outline.widthMultiplier = .009f;
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            int[] order = { 0,1,2,3,0,4,5,1,5,6,2,6,7,3,7,4 };
            outline.positionCount = order.Length; outline.SetPositions(order.Select(i => corners[i] * 1.002f).ToArray()); return outline;
        }
        static void DoorLine(Transform parent, params Vector3[] points)
        {
            var line = Child(parent, "Doorway guide").AddComponent<LineRenderer>(); line.useWorldSpace = true; line.sharedMaterial = ink;
            line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = .008f; line.startColor = line.endColor = new Color(1, 1, 1, .16f); buildingLines.Add(line);
        }
        static LineRenderer Contour(Transform parent, string name, Vector3[] points, bool loop = false, float width = .009f)
        {
            var line = Child(parent, name).AddComponent<LineRenderer>(); line.sharedMaterial = ink; line.useWorldSpace = false; line.loop = loop;
            line.positionCount = points.Length; line.SetPositions(points); line.widthMultiplier = width; return line;
        }
        static Vector3[] Circle(float radius, float height)
        {
            return Enumerable.Range(0, 48).Select(i => { float angle = i * Mathf.PI * 2 / 48; return new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius); }).ToArray();
        }
        static GameObject Cylinder(Transform parent, string name, Vector3 center, float radius, float height, Quaternion rotation)
        {
            var root = Child(parent, name); root.transform.position = center; root.transform.rotation = rotation;
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder); mesh.name = "Black surface"; mesh.transform.SetParent(root.transform, false);
            mesh.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            mesh.GetComponent<Renderer>().sharedMaterial = architecture; building.Add(mesh.GetComponent<Renderer>()); UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());
            buildingLines.Add(Contour(root.transform, "Rim top", Circle(radius, height / 2), true));
            buildingLines.Add(Contour(root.transform, "Rim bottom", Circle(radius, -height / 2), true));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4; Vector3 side = new Vector3(Mathf.Cos(angle) * radius * 1.001f, 0, Mathf.Sin(angle) * radius * 1.001f);
                buildingLines.Add(Contour(root.transform, "Pipe seam", new[] { side - Vector3.up * height / 2, side + Vector3.up * height / 2 }, false, .006f));
            }
            return root;
        }
        static void IndustrialStack(Transform parent, Vector3 center)
        {
            var stack = Cylinder(parent, "Industrial riser", center, .55f, 4, Quaternion.identity);
            var collider = stack.AddComponent<BoxCollider>(); collider.size = new Vector3(1.1f, 4, 1.1f); walls.Add(collider);
            foreach (float y in new[] { -1.6f, 1.5f })
            {
                var flange = Cylinder(stack.transform, "Bolted flange", center + Vector3.up * y, .68f, .13f, Quaternion.identity);
                for (int bolt = 0; bolt < 10; bolt++)
                {
                    float angle = bolt * Mathf.PI / 5;
                    Box(flange.transform, "Flange bolt", center + Vector3.up * (y + .09f) + new Vector3(Mathf.Cos(angle) * .6f, 0, Mathf.Sin(angle) * .6f), new Vector3(.06f, .08f, .06f), false);
                    var last = building[building.Count - 1]; UnityEngine.Object.DestroyImmediate(last.GetComponent<Collider>());
                }
            }
        }
        static LineRenderer[] MakeSlash(Transform camera)
        {
            var traces = new List<LineRenderer>();
            for (int j = 0; j < 3; j++)
            {
                Vector3[] points = Enumerable.Range(0, 20).Select(i =>
                {
                    float t = i / 19f; return new Vector3(Mathf.Lerp(.7f, -.65f, t), Mathf.Lerp(-.18f, .35f, t) + Mathf.Sin(t * Mathf.PI) * (.13f + j * .018f), 1.05f + j * .012f);
                }).ToArray();
                var line = Contour(camera, "White sword arc " + j, points, false, j == 0 ? .007f : .0025f); line.enabled = false; traces.Add(line);
            }
            return traces.ToArray();
        }
        static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Whistle/SceneAssets/Audio/" + name + ".wav");
        static AudioClip Tone(string name, float frequency, int beats, float duration, float rise)
        {
            string path = AssetRoot + "/" + name + ".wav"; const int rate = 44100; int samples = (int)(rate * duration);
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2); writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / rate, p = t / duration, beat = (p * beats) % 1;
                    float envelope = Mathf.Min(1, beat * 35) * Mathf.Exp(-beat * 7) * (1 - p * .25f);
                    float phase = 2 * Mathf.PI * frequency * (t + (rise - 1) * t * t / (duration * 2));
                    float sample = (Mathf.Sin(phase) + .3f * Mathf.Sin(phase * 2.7f)) * envelope * .55f;
                    writer.Write((short)(Mathf.Clamp(sample, -1, 1) * 32767));
                }
            }
            AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
