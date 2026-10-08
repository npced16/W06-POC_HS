using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace WhistlePOC.Editor
{
    public static class DuelPresentationAuthoring
    {
        [MenuItem("Whistle POC/Apply readable combat feedback")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
            var game = Object.FindFirstObjectByType<BossDuel>();
            if (game == null) throw new InvalidOperationException("Open boss duel first.");
            Configure(game);
            if (game.soundEcho != null)
            {
                game.soundEcho.minimumRadius = .5f; game.soundEcho.maximumRadius = 4.5f;
                game.soundEcho.holdDuration = .04f; game.soundEcho.fadeDuration = .2f; game.soundEcho.idleVisibility = 0;
                EditorUtility.SetDirty(game.soundEcho);
            }
            EditorSceneManager.MarkSceneDirty(game.gameObject.scene); EditorSceneManager.SaveScene(game.gameObject.scene); AssetDatabase.SaveAssets();
        }
        public static void Configure(BossDuel game)
        {
            var feedback = game.presentation;
            if (feedback == null)
            {
                var root = new GameObject("Combat presentation"); root.transform.SetParent(game.transform, false);
                feedback = root.AddComponent<DuelPresentation>();
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Whistle/BossAssets/Echo Lines.mat");
                string path = "Assets/Prepab/Boss Armor Health Marks.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    var marks = new GameObject("Boss Armor Health Marks");
                    for (int i = 0; i < 6; i++)
                    {
                        var obj = new GameObject("Armor mark " + i, typeof(LineRenderer)); obj.transform.SetParent(marks.transform, false);
                        obj.transform.localPosition = new Vector3((i - 2.5f) * .09f, 1.2f, .48f);
                        var line = obj.GetComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = false; line.loop = true; line.widthMultiplier = .006f; line.positionCount = 4;
                        line.SetPositions(new[] { new Vector3(-.026f, 0, 0), new Vector3(0, .045f, 0), new Vector3(.026f, 0, 0), new Vector3(0, -.045f, 0) });
                    }
                    prefab = PrefabUtility.SaveAsPrefabAsset(marks, path); Object.DestroyImmediate(marks);
                }
                var health = (GameObject)PrefabUtility.InstantiatePrefab(prefab, game.boss); health.transform.localPosition = Vector3.zero; health.transform.localRotation = Quaternion.identity;
                feedback.healthMarks = health.GetComponentsInChildren<LineRenderer>();
                feedback.deathBurst = new LineRenderer[10];
                for (int i = 0; i < feedback.deathBurst.Length; i++)
                {
                    var obj = new GameObject("Death impact " + i, typeof(LineRenderer)); obj.transform.SetParent(root.transform, false);
                    var line = obj.GetComponent<LineRenderer>(); line.sharedMaterial = material; line.useWorldSpace = true; line.positionCount = 2; line.widthMultiplier = .012f; line.enabled = false;
                    feedback.deathBurst[i] = line;
                }
            }
            feedback.game = game; game.presentation = feedback; EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(game);
        }
    }
}
