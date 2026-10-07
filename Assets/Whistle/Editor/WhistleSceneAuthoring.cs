using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhistlePOC.Editor
{
    public static class WhistleSceneAuthoring
    {
        const string Root = "Assets/Whistle/SceneAssets/Prefabs/";
        [MenuItem("Whistle POC/Convert to prefabs and pooling")]
        public static void ConvertToPooling()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before migrating.");
            var game = Object.FindFirstObjectByType<WhistleGame>(); if (game == null) throw new InvalidOperationException("Open WhistlePOC first.");
            var stage = game.Stage; Directory.CreateDirectory(Root);
            var data = new SerializedObject(stage);
            var previousScans = data.FindProperty("scanRings");
            var material = stage.meleePrefab.GetComponentsInChildren<LineRenderer>(true).First().sharedMaterial;
            var circle = WavePrefab("Footstep Wave", material, false, false);
            var square = WavePrefab("Square Sound Wave", material, true, false);
            var scan = WavePrefab("Whistle Scan Front", material, false, true);
            string[] enemyFiles = { "Melee Enemy", "Armored Enemy", "Ranged Enemy", "Rusher Enemy", "Deceiver Enemy" };
            foreach (string name in enemyFiles) BakeEnemy(Root + name + ".prefab", circle);
            string armoredPath = Root + "Armored Deceiver.prefab";
            if (!File.Exists(armoredPath))
            {
                var decoy = PrefabUtility.LoadPrefabContents(Root + "Deceiver Enemy.prefab");
                var armored = PrefabUtility.LoadPrefabContents(Root + "Armored Enemy.prefab");
                var armor = Object.Instantiate(armored.transform.Find("Armor").gameObject, decoy.transform); armor.name = "Armor";
                PrefabUtility.SaveAsPrefabAsset(decoy, armoredPath); PrefabUtility.UnloadPrefabContents(armored); PrefabUtility.UnloadPrefabContents(decoy);
            }
            BakeEnemy(armoredPath, circle);
            BakeProjectile(Root + "Shuriken.prefab", square);
            stage.armoredDeceiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(armoredPath);
            stage.scanWavePrefab = scan;
            stage.slashEffectPrefab = SwordEffect("Sword Slash Effect", PooledSwordGraphic.EffectKind.Slash);
            stage.dragTrailPrefab = SwordEffect("Sword Drag Trail", PooledSwordGraphic.EffectKind.DragTrail);
            stage.perfectEffectPrefab = SwordEffect("Perfect Counter Effect", PooledSwordGraphic.EffectKind.PerfectRing);
            var oldScans = new List<GameObject>();
            for (int i = 0; i < previousScans.arraySize; i++) if (previousScans.GetArrayElementAtIndex(i).objectReferenceValue is LineRenderer line) oldScans.Add(line.gameObject);
            previousScans.ClearArray(); data.FindProperty("environmentEcho").ClearArray(); data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var old in oldScans) Object.DestroyImmediate(old);
            stage.armoredDeceiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(armoredPath);
            stage.scanWavePrefab = scan;
            stage.slashEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Sword Slash Effect.prefab");
            stage.dragTrailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Sword Drag Trail.prefab");
            stage.perfectEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Perfect Counter Effect.prefab");
            var manager = game.GetComponentInChildren<ObjectPoolManager>();
            if (manager == null) { var obj = new GameObject("Object Pool Manager"); obj.transform.SetParent(game.transform, false); manager = obj.AddComponent<ObjectPoolManager>(); }
            stage.pool = manager;
            manager.catalog.Clear();
            AddPool(manager, stage.meleePrefab, 8); AddPool(manager, stage.armoredPrefab, 4); AddPool(manager, stage.rangedPrefab, 6);
            AddPool(manager, stage.rusherPrefab, 6); AddPool(manager, stage.deceiverPrefab, 6); AddPool(manager, stage.armoredDeceiverPrefab, 4);
            AddPool(manager, stage.shurikenPrefab, 16); AddPool(manager, scan, 6); AddPool(manager, stage.slashEffectPrefab, 64);
            AddPool(manager, stage.dragTrailPrefab, 1); AddPool(manager, stage.perfectEffectPrefab, 1); AddPool(manager, circle, 2); AddPool(manager, square, 2);
            Connect(game.transform.Find("Environment").gameObject, "Environment");
            var canvas = game.GetComponentInChildren<Canvas>(true).gameObject; Connect(canvas, "Ink Canvas");
            var spawns = game.spawnPoints[0].parent.gameObject; Connect(spawns, "Spawn Points");
            var player = Camera.main.transform.parent.gameObject;
            var sword = (Transform)new SerializedObject(stage).FindProperty("sword").objectReferenceValue;
            Connect(sword.gameObject, "First Person Sword"); Connect(player, "Player");
            if (stage.previewRoot != null) Connect(stage.previewRoot.gameObject, "Editor Placement Preview");
            Connect(manager.gameObject, "Object Pool Manager");
            EditorUtility.SetDirty(stage); EditorUtility.SetDirty(manager); EditorUtility.SetDirty(game);
            Connect(game.gameObject, "Whistle Game");
            BindPlayer(game);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene);
            Debug.Log("WHISTLE POOLING: authored prefabs connected and pool catalog saved.");
        }
        static void AddPool(ObjectPoolManager manager, GameObject prefab, int count) { manager.catalog.Add(new ObjectPoolManager.Entry { prefab = prefab, prewarm = count }); }
        static void Connect(GameObject obj, string name)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(obj)) return;
            PrefabUtility.SaveAsPrefabAssetAndConnect(obj, Root + name + ".prefab", InteractionMode.AutomatedAction);
        }
        static GameObject WavePrefab(string name, Material material, bool square, bool horizontal)
        {
            string path = Root + name + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var obj = new GameObject(name, typeof(LineRenderer), typeof(PoolMember)); var line = obj.GetComponent<LineRenderer>();
            line.useWorldSpace = false; line.loop = true; line.startColor = line.endColor = Color.white; line.sharedMaterial = material;
            line.widthMultiplier = horizontal ? .018f : .025f;
            if (square) line.positionCount = 4;
            else
            {
                line.positionCount = horizontal ? 72 : 48;
                for (int i = 0; i < line.positionCount; i++) { float a = i * Mathf.PI * 2 / line.positionCount; line.SetPosition(i, horizontal ? new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) : new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0)); }
            }
            // SetPositions does not expand the existing position count.
            if (square) line.SetPositions(new[] { new Vector3(-1,-1,0), new Vector3(-1,1,0), new Vector3(1,1,0), new Vector3(1,-1,0) });
            line.enabled = false; var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path); Object.DestroyImmediate(obj); return prefab;
        }
        static void ReplaceWave(LineRenderer old, GameObject prefab)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
            obj.name = old.name; obj.transform.localPosition = old.transform.localPosition; obj.transform.localRotation = old.transform.localRotation;
            Object.DestroyImmediate(old.gameObject);
        }
        static void BakeEnemy(string path, GameObject wavePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            foreach (var line in root.GetComponentsInChildren<LineRenderer>(true).Where(l => l.name.StartsWith("Footstep Wave") || l.name.StartsWith("False Footstep")).ToArray())
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(line.gameObject)) ReplaceWave(line, wavePrefab);
            if (root.GetComponent<PoolMember>() == null) root.AddComponent<PoolMember>();
            var visual = root.GetComponent<PooledEnemyVisual>() ?? root.AddComponent<PooledEnemyVisual>();
            visual.body = root.GetComponentsInChildren<MeshRenderer>(true);
            var lines = root.GetComponentsInChildren<LineRenderer>(true);
            visual.footsteps = lines.Where(l => l.name.StartsWith("Footstep Wave")).ToArray(); visual.falseFootsteps = lines.Where(l => l.name.StartsWith("False Footstep")).ToArray();
            visual.contours = lines.Where(l => !visual.footsteps.Contains(l) && !visual.falseFootsteps.Contains(l)).ToArray(); visual.weapon = root.transform.Find("Enemy Sword");
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
        }
        static void BakeProjectile(string path, GameObject wavePrefab)
        {
            var root = PrefabUtility.LoadPrefabContents(path); var previous = root.GetComponentsInChildren<LineRenderer>(true).First(l => l.name == "Square Sound Wave");
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(previous.gameObject)) ReplaceWave(previous, wavePrefab);
            if (root.GetComponent<PoolMember>() == null) root.AddComponent<PoolMember>();
            var visual = root.GetComponent<PooledProjectileVisual>() ?? root.AddComponent<PooledProjectileVisual>();
            visual.body = root.GetComponentsInChildren<MeshRenderer>(true); visual.soundWave = root.GetComponentsInChildren<LineRenderer>(true).First(l => l.name == "Square Sound Wave");
            visual.contours = root.GetComponentsInChildren<LineRenderer>(true).Where(l => l != visual.soundWave).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
        }
        static GameObject SwordEffect(string name, PooledSwordGraphic.EffectKind kind)
        {
            string path = Root + name + ".prefab"; var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(PooledSwordGraphic));
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = Vector2.one * .5f; rect.sizeDelta = new Vector2(1600, 900);
            var effect = obj.GetComponent<PooledSwordGraphic>(); effect.kind = kind; effect.raycastTarget = false;
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path); Object.DestroyImmediate(obj); return prefab;
        }
        static void BindPlayer(WhistleGame game)
        {
            var stage = new SerializedObject(game.Stage); stage.FindProperty("<View>k__BackingField").objectReferenceValue = Camera.main;
            stage.FindProperty("sword").objectReferenceValue = Camera.main.transform.Find("First Person Sword"); stage.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(game.Stage);
        }
        public static WhistleGame Author(WhistleGame game)
        {
            if (game.Stage != null) { BindPlayer(game); return game; }
            var core = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Whistle Game.prefab"); var player = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Player.prefab");
            if (core == null || player == null) throw new InvalidOperationException("Open the saved authored WhistlePOC scene.");
            if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);
            Object.DestroyImmediate(game.gameObject); PrefabUtility.InstantiatePrefab(player);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(core); game = instance.GetComponent<WhistleGame>(); BindPlayer(game); return game;
        }
        [MenuItem("Whistle POC/Add experimental combat features")]
        public static void AddCombatFeatures()
        {
            var game = Object.FindFirstObjectByType<WhistleGame>(); if (game == null || Application.isPlaying) return;
            game.Stage.rusherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Rusher Enemy.prefab");
            game.Stage.deceiverPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Deceiver Enemy.prefab");
            EditorUtility.SetDirty(game.Stage); EditorSceneManager.SaveScene(game.gameObject.scene);
        }
        [MenuItem("Whistle POC/Edit level layout")]
        public static void EditLevel()
        {
            var game = Object.FindFirstObjectByType<WhistleGame>(); if (game == null || Application.isPlaying) return;
            SceneVisibilityManager.instance.Hide(game.GetComponentInChildren<Canvas>().gameObject, true); EditorApplication.ExecuteMenuItem("Window/General/Scene");
            SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 11), Quaternion.Euler(30, -25, 0), 18); Selection.activeGameObject = game.gameObject;
        }
        [MenuItem("Whistle POC/Edit UI layout")]
        public static void EditUI()
        {
            var game = Object.FindFirstObjectByType<WhistleGame>(); if (game == null || Application.isPlaying) return;
            var canvas = game.GetComponentInChildren<Canvas>().gameObject; SceneVisibilityManager.instance.Show(canvas, true); Selection.activeGameObject = canvas;
            EditorApplication.ExecuteMenuItem("Window/General/Scene"); SceneView.lastActiveSceneView.FrameSelected();
        }
        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawSpawns(WhistleGame game, GizmoType type)
        {
            if (Application.isPlaying || game.spawnPoints == null) return;
            Gizmos.color = Color.white;
            foreach (var point in game.spawnPoints) { if (point == null) continue; Gizmos.DrawWireSphere(point.position + Vector3.up, .4f); Handles.Label(point.position + Vector3.up * 1.6f, point.name); }
        }
    }
}
