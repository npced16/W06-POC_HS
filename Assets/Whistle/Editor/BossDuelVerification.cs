using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WhistlePOC.Editor
{
    public static class BossDuelVerification
    {
        static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly List<string> results = new List<string>();
        static void Field(BossDuel game, string name, object value) => typeof(BossDuel).GetField(name, Private).SetValue(game, value);
        static void Property(BossDuel game, string name, object value) => typeof(BossDuel).GetProperty(name).SetValue(game, value);
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception("FAIL: " + message); results.Add("PASS: " + message); }
        static void Place(BossDuel game, Vector3 player, Vector3 boss)
        {
            game.player.enabled = false; game.player.transform.position = player; game.player.enabled = true;
            game.boss.position = boss; Vector3 facing = boss - player; facing.y = 0;
            game.player.transform.rotation = Quaternion.LookRotation(facing); game.boss.rotation = Quaternion.LookRotation(-facing); Physics.SyncTransforms();
        }
        static void Strike(BossDuel game, BossPattern pattern)
        {
            Property(game, "Pattern", pattern); Property(game, "Action", BossAction.Strike);
            Field(game, "timer", .16f); Field(game, "strikeResolved", false); game.TickBoss(.01f);
        }
        [MenuItem("Whistle POC/Run boss duel smoke test")]
        public static void Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode in the boss duel scene first.");
            var game = UnityEngine.Object.FindFirstObjectByType<BossDuel>();
            if (game == null) throw new InvalidOperationException("Open boss duel first.");
            results.Clear();
            try
            {
                game.Begin();
                Check(game.Health == 3 && game.BossHealth == 24 && game.State == DuelState.Fighting, "new duel resets health, boss and state");
                VerifyInputEvents(game);
                game.Begin();
                Check(game.cues.Length == 3 && game.cues[0] != game.cues[1] && game.cues[1] != game.cues[2], "three distinct authored attack cues");
                Check(game.bossAudio.spatialBlend == 1 && game.bossAudio.minDistance == 2 && game.occlusion != null, "3D boss audio and wall occlusion configured");
                Check(game.Scan() && game.Revealed && game.Breath == 65, "scan spends 35 breath and reveals");
                Check(!game.Scan() && game.Breath == 65, "scan cannot stack or spend again during reveal");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, 6));
                game.FindRoute(game.boss.position, game.player.transform.position);
                Check(game.RouteCount > 0, "boss routes from neighboring room through doorway");
                Place(game, new Vector3(6, .05f, -6), new Vector3(-6, 0, 6));
                bool crossedWall = false;
                for (int i = 0; i < 160; i++) { game.TickBoss(.05f); crossedWall |= game.Blocked(game.boss.position, .45f); }
                Check(!crossedWall && Vector3.Distance(game.boss.position, game.player.transform.position) < 11, "boss physically pursues across rooms without crossing walls");
                game.Begin();
                Check(!game.Clear(new Vector3(-9, 0, -3), new Vector3(-9, 0, 3), .1f), "wall blocks attack and sound line");
                Check(game.Clear(new Vector3(-5.5f, 0, -3), new Vector3(-5.5f, 0, 3), .1f), "doorway allows attack and sound line");
                game.player.enabled = false; game.player.transform.position = new Vector3(-1.5f, .05f, -2); game.player.enabled = true;
                for (int i = 0; i < 20; i++) game.player.Move(Vector3.right * .2f);
                Check(game.player.transform.position.x < -.5f, "character controller cannot walk through partition");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, 6));
                Check(!game.Attack() && game.Stamina == 76, "whiff consumes stamina and returns no hit");
                Check(!game.Parry(), "whiff recovery prevents immediate parry");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                Check(game.Attack() && game.BossHealth == 23, "armored boss takes only one damage outside opening");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                Property(game, "Action", BossAction.Recover); Field(game, "timer", 1f);
                Check(game.Attack() && game.BossHealth == 21, "recovery opening rewards three damage");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                Check(game.Scan(), "pre-counter scan spends breath"); Field(game, "scanRemaining", 0f);
                Check(game.Parry(), "parry can be committed with stamina"); Strike(game, BossPattern.Sweep);
                Check(game.Health == 3 && game.Counters == 1 && game.Action == BossAction.Stunned && game.Breath == 95, "perfect parry avoids damage, stuns boss and refunds breath");
                Check(game.Attack() && game.BossHealth == 19, "counter opening rewards five damage");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                game.Parry(); Field(game, "parryRemaining", 0f); Strike(game, BossPattern.Sweep);
                Check(game.Health == 2 && game.Counters == 0, "expired parry fails and costs health");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                Field(game, "dodgeRemaining", .2f); Strike(game, BossPattern.Overhead);
                Check(game.Health == 3, "active dodge avoids boss damage");
                game.Begin(); Place(game, new Vector3(-9, .05f, -1.4f), new Vector3(-9, 0, 1.4f));
                Strike(game, BossPattern.Overhead); Check(game.Health == 3, "boss cannot strike through wall");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                Property(game, "BossHealth", 12); game.TickBoss(.01f); Check(game.Enraged, "half health enters second phase");
                Property(game, "BossHealth", 1); game.Attack(); Check(game.State == DuelState.Victory && game.LastFeedback.Contains("결투 승리"), "boss death reports victory without UI");
                game.Begin(); Place(game, new Vector3(-5.5f, .05f, -6), new Vector3(-5.5f, 0, -3.8f));
                for (int i = 0; i < 3; i++) Strike(game, BossPattern.Sweep);
                Check(game.State == DuelState.Defeat && game.Health == 0, "three hits end duel in defeat");
                Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 0 && UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 0, "active duel scene has no Canvas or UI EventSystem");
                game.Begin(); Check(game.Breath == 100 && game.Stamina == 100 && game.Counters == 0, "restart clears resources and counters");
                Debug.Log("BOSS DUEL SMOKE: " + results.Count + " checks passed.");
            }
            catch (Exception ex) { results.Add(ex.ToString()); throw; }
            finally
            {
                Directory.CreateDirectory("POCVerification"); File.WriteAllLines("POCVerification/boss-duel-smoke.txt", results);
                game.Begin(); Property(game, "State", DuelState.Paused);
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
        }
        static void VerifyInputEvents(BossDuel game)
        {
            var previous = InputSystem.settings.backgroundBehavior;
            var keyboard = InputSystem.AddDevice<Keyboard>(); int moveEvents = 0, scanEvents = 0; Vector2 lastMove = Vector2.zero;
            Action<Vector2> move = value => { moveEvents++; lastMove = value; };
            Action scan = () => scanEvents++;
            game.input.Move += move; game.input.Scan += scan;
            try
            {
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update();
                Check(moveEvents > 0 && lastMove.y == 1, "WASD action publishes movement event");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                Check(lastMove == Vector2.zero, "key release publishes zero movement");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
                Check(scanEvents == 1 && game.Revealed && game.Breath == 65, "scan action event reaches subscribed game manager once");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                game.input.enabled = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
                Check(scanEvents == 1, "disabled input component stops publishing gameplay actions");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); game.input.enabled = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update();
                Check(scanEvents == 2, "reenabling input does not duplicate subscriptions");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
            }
            finally
            {
                game.input.Move -= move; game.input.Scan -= scan; game.input.enabled = true;
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previous;
            }
        }
    }
}
