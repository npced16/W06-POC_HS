using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhistlePOC.Editor
{
    public static class SoundEchoAuthoring
    {
        const string WavePath = "Assets/Prepab/Sound Echo Wave.prefab";
        const string MaterialPath = "Assets/Whistle/BossAssets/Sound Map Echo.mat";

        [MenuItem("Whistle POC/Add sound reveal waves")]
        public static void AddToCurrentScene()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before authoring sound echoes.");
            var game = Object.FindFirstObjectByType<BossDuel>();
            if (game == null) throw new InvalidOperationException("Open boss duel first.");
            Configure(game);
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void Configure(BossDuel game)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Whistle/Sound Map Echo"));
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WavePath);
            if (prefab == null)
            {
                var obj = new GameObject("Sound Echo Wave", typeof(LineRenderer));
                var line = obj.GetComponent<LineRenderer>();
                line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Whistle/BossAssets/Echo Lines.mat");
                line.useWorldSpace = false; line.loop = true; line.widthMultiplier = .016f; line.positionCount = 96;
                for (int i = 0; i < line.positionCount; i++)
                { float angle = i * Mathf.PI * 2 / line.positionCount; line.SetPosition(i, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle))); }
                line.enabled = false; prefab = PrefabUtility.SaveAsPrefabAsset(obj, WavePath); Object.DestroyImmediate(obj);
            }
            var echo = game.soundEcho;
            if (echo == null)
            {
                var root = new GameObject("Sound Echo"); root.transform.SetParent(game.transform, false);
                echo = root.AddComponent<SoundEcho>();
                echo.waves = new LineRenderer[SoundEcho.Capacity];
                for (int i = 0; i < echo.waves.Length; i++)
                {
                    var wave = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform); wave.name = "Sound Wave " + i;
                    echo.waves[i] = wave.GetComponent<LineRenderer>();
                }
            }
            echo.mapMaterial = material; game.soundEcho = echo;
            EditorUtility.SetDirty(echo); EditorUtility.SetDirty(game);
        }
    }
}
