using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhistlePOC.Editor
{
    public static class DuelMapPrefabAuthoring
    {
        const string Folder = "Assets/Prepab/Map Modules";
        public const string MapPath = "Assets/Prepab/Boss Duel Building.prefab";
        const string RiserPath = "Assets/Prepab/Industrial riser.prefab";
        static Material black, ink;

        [MenuItem("Whistle POC/Convert current map to modular prefabs")]
        public static void Convert()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var game = Object.FindFirstObjectByType<BossDuel>();
            if (game == null) throw new InvalidOperationException("Open the boss duel scene first.");
            var map = game.map != null ? game.map.gameObject : game.transform.Find("Building — four rooms and connecting doors").gameObject;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Prepab", "Map Modules");
            black = AssetDatabase.LoadAssetAtPath<Material>("Assets/Whistle/BossAssets/Building.mat");
            ink = AssetDatabase.LoadAssetAtPath<Material>("Assets/Whistle/BossAssets/Echo Lines.mat");
            var door = DoorPrefab(); var railing = RailingPrefab();
            var children = map.transform.Cast<Transform>().ToArray();
            foreach (var child in children)
            {
                var obj = child.gameObject;
                if (child.name.StartsWith("Doorway guide"))
                {
                    var line = child.GetComponent<LineRenderer>(); var points = new Vector3[line.positionCount]; line.GetPositions(points);
                    if (!line.useWorldSpace) for (int i = 0; i < points.Length; i++) points[i] = line.transform.TransformPoint(points[i]);
                    var bounds = new Bounds(points[0], Vector3.zero); foreach (var point in points) bounds.Encapsulate(point);
                    var center = bounds.center; center.y = 0;
                    var frame = (GameObject)PrefabUtility.InstantiatePrefab(door, map.transform);
                    frame.name = "Doorway Frame"; frame.transform.position = center; frame.transform.rotation = Quaternion.Euler(0, bounds.size.z > bounds.size.x ? 90 : 0, 0);
                    Object.DestroyImmediate(obj); continue;
                }
                if (PrefabUtility.IsAnyPrefabInstanceRoot(obj))
                {
                    if (obj.GetComponent<DuelMapModule>() == null) MarkObstacles(obj, child.name.StartsWith("Industrial riser"));
                    continue;
                }
                if (child.name.StartsWith("Industrial riser") && AssetDatabase.LoadAssetAtPath<GameObject>(RiserPath) != null)
                {
                    obj = Replace(obj, AssetDatabase.LoadAssetAtPath<GameObject>(RiserPath));
                    MarkObstacles(obj, true); continue;
                }
                bool solid = child.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0 || child.name.StartsWith("Room partition");
                MarkObstacles(obj, solid);
                string key = Key(child);
                string path = Folder + "/" + key + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    // Normalize the asset pivot while retaining the user's scene transform as an override.
                    Vector3 position = child.localPosition; Quaternion rotation = child.localRotation;
                    child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity;
                    PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.AutomatedAction);
                    child.localPosition = position; child.localRotation = rotation;
                }
                else Replace(obj, prefab);
            }
            if (map.transform.Find("Perimeter maintenance rails") == null)
            {
                var rails = new GameObject("Perimeter maintenance rails"); rails.transform.SetParent(map.transform, false);
                foreach (float coordinate in new[] { -9f, -6f, -3f, 3f, 6f, 9f })
                    foreach (float side in new[] { -11.1f, 11.1f })
                    {
                        Place(railing, rails.transform, new Vector3(coordinate, 0, side), 0);
                        Place(railing, rails.transform, new Vector3(side, 0, coordinate), 90);
                    }
            }
            game.map = map.GetComponent<DuelMap>() ?? map.AddComponent<DuelMap>(); game.map.Bind(game);
            EditorUtility.SetDirty(game); EditorUtility.SetDirty(game.map);
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(map))
                PrefabUtility.SaveAsPrefabAssetAndConnect(map, MapPath, InteractionMode.AutomatedAction);
            else PrefabUtility.ApplyPrefabInstance(map, InteractionMode.AutomatedAction);
            game.map.Bind(game); EditorUtility.SetDirty(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = map;
            Debug.Log("DUEL MAP: nested prefab modules saved; user riser prefab and edited placement preserved.");
        }
        static string Key(Transform obj)
        {
            if (obj.name == "Floor" || obj.name == "Ceiling") return "Floor Slab 24m";
            if (obj.name.Contains("wall")) return obj.localScale.z > obj.localScale.x ? "Outer Wall Z 24m" : "Outer Wall X 24m";
            if (obj.name.StartsWith("Room partition")) return "Partition " + (obj.localScale.z > obj.localScale.x ? "Z " : "X ") + Mathf.Max(obj.localScale.x, obj.localScale.z).ToString("0") + "m";
            if (obj.name.StartsWith("Ceiling steam")) return "Steam Main 22m";
            if (obj.name.StartsWith("Ceiling return")) return "Return Pipe 22m";
            return obj.name;
        }
        static void MarkObstacles(GameObject obj, bool solid)
        {
            var module = obj.GetComponent<DuelMapModule>() ?? obj.AddComponent<DuelMapModule>();
            module.navigationObstacles = solid ? obj.GetComponentsInChildren<BoxCollider>().Where(c => !c.isTrigger).ToArray() : new BoxCollider[0];
            EditorUtility.SetDirty(module);
        }
        static GameObject Replace(GameObject old, GameObject prefab)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
            obj.name = old.name; obj.transform.localPosition = old.transform.localPosition; obj.transform.localRotation = old.transform.localRotation; obj.transform.localScale = old.transform.localScale;
            obj.transform.SetSiblingIndex(old.transform.GetSiblingIndex()); Object.DestroyImmediate(old); return obj;
        }
        static void Place(GameObject prefab, Transform parent, Vector3 position, float yaw)
        {
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent); obj.transform.localPosition = position; obj.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        }
        static GameObject DoorPrefab()
        {
            string path = Folder + "/Doorway Frame 3m.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("Doorway Frame 3m"); var module = root.AddComponent<DuelMapModule>();
            var left = Box(root.transform, "Left jamb", new Vector3(-1.49f, 1.7f, 0), new Vector3(.07f, 3.4f, .09f));
            var right = Box(root.transform, "Right jamb", new Vector3(1.49f, 1.7f, 0), new Vector3(.07f, 3.4f, .09f));
            var lintel = Box(root.transform, "Lintel", new Vector3(0, 3.42f, 0), new Vector3(3.06f, .08f, .09f)); Object.DestroyImmediate(lintel.GetComponent<BoxCollider>());
            module.navigationObstacles = new[] { left.GetComponent<BoxCollider>(), right.GetComponent<BoxCollider>() };
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return prefab;
        }
        static GameObject RailingPrefab()
        {
            string path = Folder + "/Maintenance Railing 3m.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("Maintenance Railing 3m"); var module = root.AddComponent<DuelMapModule>();
            foreach (float x in new[] { -1.45f, 0, 1.45f })
                Box(root.transform, "Post", new Vector3(x, .55f, 0), new Vector3(.04f, 1.1f, .04f));
            foreach (float y in new[] { .15f, .55f, 1.1f })
                Box(root.transform, "Rail", new Vector3(0, y, 0), new Vector3(3, .035f, .035f));
            foreach (var collider in root.GetComponentsInChildren<BoxCollider>()) Object.DestroyImmediate(collider);
            var blocker = root.AddComponent<BoxCollider>(); blocker.center = Vector3.up * .55f; blocker.size = new Vector3(3, 1.1f, .1f); module.navigationObstacles = new[] { blocker };
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path); Object.DestroyImmediate(root); return prefab;
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name = name; obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial = black;
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = ink; line.useWorldSpace = false; line.widthMultiplier = .007f;
            Vector3[] corners = { new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f), new Vector3(.5f,-.5f,.5f), new Vector3(-.5f,-.5f,.5f), new Vector3(-.5f,.5f,-.5f), new Vector3(.5f,.5f,-.5f), new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            int[] order = { 0,1,2,3,0,4,5,1,5,6,2,6,7,3,7,4 };
            line.positionCount = order.Length; line.SetPositions(order.Select(i => corners[i] * 1.002f).ToArray()); return obj;
        }
    }
}
