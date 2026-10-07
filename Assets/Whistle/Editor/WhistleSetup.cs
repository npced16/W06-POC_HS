using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace WhistlePOC.Editor
{
    [InitializeOnLoad]
    public static class WhistleSetup
    {
        const string ScenePath = "Assets/Scenes/WhistlePOC.unity";
        const string TestKey = "WhistlePOC.Smoke";
        static double phaseTime;
        static int phase;
        static WhistleGame game;
        static Keyboard testKeyboard;
        static bool testHolding;
        static bool checkedRange;
        static Mouse testMouse;
        static WhistleInputProbe inputProbe;
        static Vector2 mousePosition, mouseEnd;
        static bool mouseHeld;
        static bool testToggleMode;
        static WhistleGame.Enemy clickArmor, offscreenEnemy;
        static WhistleGame.Enemy echoNear, echoFar;
        static WhistleGame.Enemy rushEnemy, deceiver;
        static float rushStart;
        static float releasedBreath;
        static WhistleGame.Enemy thrower;
        static int projectileHealth;
        static readonly System.Collections.Generic.List<WhistleGame.Enemy> contactEnemies = new System.Collections.Generic.List<WhistleGame.Enemy>();
        static InputSettings.BackgroundBehavior previousBackground;
        static InputSettings.UpdateMode previousInputUpdate;
        static WhistleSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) && !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty)
                    CreateScene();
            };
            EditorApplication.update += Tick;
        }
        [MenuItem("Whistle POC/Open prototype")]
        public static void CreateScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = File.Exists(ScenePath) ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (Camera.main == null)
            {
                var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 1.65f, 0); camera.GetComponent<Camera>().fieldOfView = 65;
            }
            var controller = UnityEngine.Object.FindFirstObjectByType<WhistleGame>();
            if (controller == null)
            {
                Directory.CreateDirectory("POCVerification");
                if (File.Exists(ScenePath) && !File.Exists("POCVerification/WhistlePOC-before-authoring.unity")) File.Copy(ScenePath, "POCVerification/WhistlePOC-before-authoring.unity");
                controller = new GameObject("Whistle Game").AddComponent<WhistleGame>();
            }
            controller = WhistleSceneAuthoring.Author(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = controller.gameObject;
            Debug.Log("WHISTLE POC: scene-authored environment, UI, spawn points and prefab references are ready.");
        }
        [MenuItem("Whistle POC/Run gameplay smoke test")]
        public static void BuildAndVerify()
        {
            CreateScene();
            SessionState.SetBool(TestKey, true);
            EditorApplication.isPaused = false;
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            Debug.Log("WHISTLE CHECK: " + message);
        }
        // Combat checks use settled returns; the final phases test real propagation timing.
        static void RevealForCombat()
        {
            game.SetWhistle(true);
            foreach (var enemy in game.Enemies) game.EchoPulses.Add(game.Clock - game.Stage.EnemyDistance(enemy) / game.echoSpeed - .08f);
            game.EchoPulses.Sort();
        }
        static void CheckPooling()
        {
            var pool = game.Stage.pool;
            Check(pool != null && pool.CreatedCount > 0, "pool manager prewarms its prefab catalog");
            int created = pool.CreatedCount;
            var first = pool.Spawn(game.Stage.meleePrefab, game.transform);
            var firstVisual = first.GetComponent<PooledEnemyVisual>();
            firstVisual.weapon.localRotation = Quaternion.Euler(0, 0, -65); firstVisual.footsteps[0].enabled = true;
            var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", new Color(1, 1, 1, .2f)); firstVisual.footsteps[0].SetPropertyBlock(block);
            Check(pool.Return(first) && !first.activeSelf && !pool.Return(first), "return deactivates an object and rejects double return");
            var reused = pool.Spawn(game.Stage.meleePrefab, game.transform);
            var reset = reused.GetComponent<PooledEnemyVisual>(); reset.footsteps[0].GetPropertyBlock(block);
            Check(reused == first && reset.weapon.localRotation == game.Stage.meleePrefab.GetComponent<PooledEnemyVisual>().weapon.localRotation && !reset.footsteps[0].enabled && block.isEmpty, "rent reuses the same instance and clears pose, wave and material state");
            pool.Return(reused);
            game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(430, 390), hp = 1 }); game.Stage.RenderNow();
            var enemy = UnityEngine.Object.FindFirstObjectByType<PooledEnemyVisual>(); int enemyId = enemy.GetInstanceID();
            game.Enemies.Clear(); game.Stage.RenderNow();
            game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(460, 390), hp = 1 }); game.Stage.RenderNow();
            Check(UnityEngine.Object.FindFirstObjectByType<PooledEnemyVisual>().GetInstanceID() == enemyId, "gameplay removal and respawn reuse the same enemy visual");
            game.Projectiles.Add(new WhistleGame.Projectile { position = new Vector3(0, 1.1f, 4), age = .3f }); game.Stage.RenderNow();
            int projectileId = UnityEngine.Object.FindFirstObjectByType<PooledProjectileVisual>().GetInstanceID();
            game.Projectiles.Clear(); game.Stage.RenderNow(); game.Projectiles.Add(new WhistleGame.Projectile { position = new Vector3(0, 1.1f, 8) }); game.Stage.RenderNow();
            var projectile = UnityEngine.Object.FindFirstObjectByType<PooledProjectileVisual>();
            Check(projectile.GetInstanceID() == projectileId && Mathf.Abs(projectile.soundWave.transform.localScale.x - .16f) < .001f, "projectile reuse resets its sound-wave animation");
            var cut = new WhistleGame.Cut { from = new Vector2(500, 350), to = new Vector2(900, 500), sweep = true }; game.Cuts.Add(cut); game.Stage.RenderNow();
            var slash = UnityEngine.Object.FindObjectsByType<PooledSwordGraphic>(FindObjectsSortMode.None).First(g => g.kind == PooledSwordGraphic.EffectKind.Slash); int slashId = slash.GetInstanceID();
            game.Cuts.Clear(); game.Stage.RenderNow(); Check(slash.Cut == null && slash.Game == null, "effect return clears old bindings");
            var nextCut = new WhistleGame.Cut { from = new Vector2(600, 350), to = new Vector2(800, 500), sweep = true }; game.Cuts.Add(nextCut); game.Stage.RenderNow();
            var nextSlash = UnityEngine.Object.FindObjectsByType<PooledSwordGraphic>(FindObjectsSortMode.None).First(g => g.kind == PooledSwordGraphic.EffectKind.Slash);
            Check(nextSlash.GetInstanceID() == slashId && nextSlash.Cut == nextCut && pool.CreatedCount == created, "slash effect reuse binds the new attack without new instantiation");
            game.BeginJourney(); Check(pool.ActiveCount == 0, "restart returns every enemy, projectile, scan and sword effect to the pool");
        }
        static void Tick()
        {
            if (File.Exists("POCVerification/restart-test.flag"))
            {
                File.Delete("POCVerification/restart-test.flag");
                SessionState.SetBool(TestKey, false);
                EditorApplication.isPlaying = false;
                File.WriteAllText("POCVerification/request-test.flag", "Retry after resource setup");
                return;
            }
            if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists("POCVerification/request-test.flag"))
            {
                try { File.Delete("POCVerification/request-test.flag"); }
                catch (IOException) { return; }
                BuildAndVerify();
                return;
            }
            if (!SessionState.GetBool(TestKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (inputProbe != null)
            {
                inputProbe.keyboard = testKeyboard; inputProbe.mouse = testMouse;
                inputProbe.holding = testHolding; inputProbe.mouseHeld = mouseHeld; inputProbe.position = mousePosition;
                inputProbe.toggleMode = testToggleMode;
            }
            try
            {
                if (game == null)
                {
                    game = UnityEngine.Object.FindFirstObjectByType<WhistleGame>();
                    if (game == null) return;
                    phase = 0; phaseTime = game.Clock; checkedRange = false;
                }
                double elapsed = game.Clock - phaseTime;
                if (elapsed < (phase == 6 || phase == 15 ? .08 : phase == 7 ? .1 : .3)) return;
                Directory.CreateDirectory("POCVerification");
                switch (phase)
                {
                    case 0:
                        Check(game.State == JourneyState.Title, "title and runtime bootstrap");
                        Check(game.Stage != null && game.Stage.meleePrefab != null && game.Stage.shurikenPrefab != null && game.spawnPoints.Length == 4, "scene retains prefab and spawn-point references after reload");
                        Check(game.Stage.rusherPrefab != null && game.Stage.deceiverPrefab != null, "experimental enemy prefabs persist in the scene");
                        Check(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length == 1 && UnityEngine.Object.FindObjectsByType<WhistleGame>(FindObjectsSortMode.None).Length == 1, "authored scene reuses UI and controller without duplicates");
                        Check(!game.Stage.previewRoot.gameObject.activeSelf, "editor placement previews are hidden during gameplay");
                        var stage = UnityEngine.Object.FindFirstObjectByType<WhistleInk>();
                        Check(stage != null && stage.GetComponent<CanvasRenderer>() != null, "ink stage has a canvas renderer");
                        Check(game.Stage != null && !game.Stage.View.orthographic && game.Stage.View.transform.position.y > 1.5f, "first person perspective at eye height");
                        foreach (var text in UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None))
                            Check(text.font != null, "UI font loaded");
                        ScreenCapture.CaptureScreenshot("POCVerification/title.png");
                        break;
                    case 1:
                        game.BeginJourney(); game.SpawnWave();
                        for (int i = 0; i < game.Enemies.Count; i++) game.Enemies[i].position.x = 460 + i * 190;
                        previousBackground = InputSystem.settings.backgroundBehavior;
                        previousInputUpdate = InputSystem.settings.updateMode;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                        testKeyboard = InputSystem.AddDevice<Keyboard>("WhistleSmokeKeyboard");
                        testMouse = InputSystem.AddDevice<Mouse>("WhistleSmokeMouse");
                        testHolding = mouseHeld = testToggleMode = false;
                        inputProbe = new GameObject("Whistle Test Input").AddComponent<WhistleInputProbe>();
                        Check(game.Health == 5 && game.Leg == 1 && game.Enemies.Count == 3, "restart resets health, route and enemies");
                        Check(game.Enemies[2].ranged && game.Enemies[0].speed > 190, "waves include a thrower and faster approaching enemies");
                        game.Stage.RenderNow();
                        Check(game.Stage.VisibleEnemyCount == 0 && game.Stage.AttackableEnemyCount == 0, "enemy bodies stay hidden before whistle");
                        Check(!game.TrySlashSegment(game.EnemyAim(game.Enemies[0]) - Vector2.right * 65, game.EnemyAim(game.Enemies[0]) + Vector2.right * 65), "attacks are disabled in darkness");
                        ScreenCapture.CaptureScreenshot("POCVerification/dark.png");
                        break;
                    case 2:
                        game.SetWhistle(false); game.Stage.RenderNow();
                        int visibleWaves = 0;
                        var waves = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Where(w => w.name.StartsWith("Footstep Wave")).ToArray();
                        Check(waves.Length == game.Enemies.Count * 3, "each enemy owns exactly three wave circles");
                        foreach (var wave in waves)
                        {
                            Check(wave.GetComponentInParent<PooledEnemyVisual>() != null && wave.transform.localPosition == Vector3.up * .9f, "waves follow the unit in world space");
                            if (wave.enabled) visibleWaves++;
                        }
                        Check(visibleWaves >= 1 && visibleWaves <= game.Enemies.Count, "footsteps display one expanding circle per enemy");
                        foreach (var wave in waves)
                        {
                            int active = 0;
                            foreach (var sibling in wave.transform.parent.GetComponentsInChildren<LineRenderer>()) if (sibling.name.StartsWith("Footstep Wave") && sibling.enabled) active++;
                            Check(active <= 1, "circles never expand simultaneously on the same unit");
                        }
                        testHolding = true;
                        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(Key.Space));
                        InputSystem.Update();
                        RevealForCombat();
                        Check(game.Revealed && Time.timeScale < .2f, "whistle reveals enemies and slows the world");
                        game.Stage.RenderNow();
                        Check(game.Stage.VisibleEnemyCount == 3 && game.Stage.AttackableEnemyCount == 1, "far enemies remain gray and nearby attackable enemies turn bright");
                        var stationaryAim = game.EnemyAim(game.Enemies[0]);
                        Check(!game.TrySlashSegment(stationaryAim, stationaryAim), "holding a stationary cursor cannot attack");
                        Check(!game.TrySlashSegment(game.EnemyAim(game.Enemies[2]) - Vector2.right * 65, game.EnemyAim(game.Enemies[2]) + Vector2.right * 65), "far enemies cannot be cut");
                        var headRect = game.EnemyHitRect(game.Enemies[0]);
                        var dragStart = new Vector2(headRect.xMin - 25, headRect.yMax - 20);
                        var dragEnd = new Vector2(headRect.xMax + 25, headRect.yMax - 20);
                        game.SetSwordDrag(dragStart, true);
                        game.SetSwordDrag(dragStart, true);
                        Check(game.Kills == 0, "press without moving never attacks");
                        game.SetSwordDrag(dragEnd, true);
                        Check(game.Kills == 1, "head drag hits immediately even after an empty swing");
                        Check(game.SwordTrail.Count > 2, "drag creates a continuous sampled sword trail");
                        game.SetSwordDrag(dragEnd, false);
                        Check(game.Kills == 1 && game.Combo == 1, "kill and chain feedback");
                        Canvas.ForceUpdateCanvases();
                        ScreenCapture.CaptureScreenshot("POCVerification/revealed.png");
                        break;
                    case 3:
                        if (elapsed < 2.1) return;
                        Check(game.Breath < game.whistleDuration, "held input consumes breath in real time");
                        Check(!game.Revealed, "exhausted breath closes sight even while whistle key stays held; breath=" + game.Breath);
                        testHolding = false;
                        InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
                        InputSystem.Update();
                        game.SetWhistle(false);
                        Check(Time.timeScale == 1 && !game.Revealed, "release restores darkness and normal time");
                        int remaining = game.Enemies.Count;
                        game.Arrive(); Check(game.State == JourneyState.Walking && game.Enemies.Count == remaining, "arrival cannot erase surviving enemies");
                        game.Enemies.Clear(); game.Projectiles.Clear(); game.Arrive(); Check(game.State == JourneyState.Camp, "cleared arrival opens camp");
                        game.Upgrade(true); Check(game.Leg == 2 && game.whistleDuration > 1.6f, "breath upgrade carries into next leg");
                        game.Arrive(); game.Upgrade(false);
                        Check(game.Leg == 3 && game.swordDamage == 2, "sword upgrade carries into next leg");
                        game.SpawnWave();
                        var armored = game.Enemies[1]; armored.position.x = 430;
                        RevealForCombat(); Check(game.TrySlashSegment(game.EnemyAim(armored) - Vector2.right * 65, game.EnemyAim(armored) + Vector2.right * 65) && !game.Enemies.Contains(armored), "upgraded sword cuts armor in one hit");
                        game.Enemies.Clear(); game.Projectiles.Clear(); game.Arrive(); Check(game.State == JourneyState.Victory, "third cleared arrival finishes escort");
                        game.BeginJourney(); game.SpawnWave();
                        game.Enemies[0].position = new Vector2(430, 340);
                        game.Enemies[1].position = new Vector2(430, 450);
                        RevealForCombat();
                        Check(game.TrySlashSegment(game.EnemyAim(game.Enemies[0]) - Vector2.right * 65, game.EnemyAim(game.Enemies[1]) + Vector2.right * 65) && game.Kills == 2, "one drag trajectory cuts two nearby enemies");
                        game.BeginJourney(); game.Enemies.Clear();
                        contactEnemies.Clear();
                        for (int i = 0; i < 5; i++)
                        {
                            var contact = new WhistleGame.Enemy { position = new Vector2(325, 370), hp = 1 };
                            contactEnemies.Add(contact); game.Enemies.Add(contact);
                        }
                        break;
                    case 4:
                        if (elapsed < 1.2) return;
                        Check(game.State == JourneyState.Defeat && game.Health == 0, "unhandled attackers cause escort defeat; state=" + game.State + " health=" + game.Health + " revealed=" + game.Revealed + " attack=" + contactEnemies[0].attack + " windup=" + contactEnemies[0].windingUp);
                        foreach (var contact in contactEnemies) Check(game.Enemies.Contains(contact) && contact.position.x == 325, "attacker stays in front instead of disappearing on contact");
                        game.BeginJourney(); Check(game.State == JourneyState.Walking && game.Health == 5 && game.swordDamage == 1, "restart after defeat clears upgrades");
                        game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(430, 390), hp = 1 });
                        var mouseRect = game.EnemyHitRect(game.Enemies[0]);
                        mousePosition = game.ScreenPoint(new Vector2(mouseRect.xMin - 25, mouseRect.yMax - 20));
                        mouseEnd = game.ScreenPoint(new Vector2(mouseRect.xMax + 25, mouseRect.yMax - 20));
                        mouseHeld = true; testHolding = true;
                        break;
                    case 5:
                        Check(game.Kills == 0, "actual held mouse button without movement cannot attack");
                        mousePosition = mouseEnd;
                        break;
                    case 6:
                        Check(game.Kills == 1 && game.SwordTrail.Count > 1, "actual Input System mouse drag hits head and creates a trail");
                        ScreenCapture.CaptureScreenshot("POCVerification/actual-drag.png");
                        mouseHeld = false; testHolding = false;
                        releasedBreath = game.Breath;
                        break;
                    case 7:
                        Check(game.Breath <= releasedBreath + .01f, "breath does not recover during the initial release delay");
                        break;
                    case 8:
                        if (elapsed < .65) return;
                        Check(game.Breath > releasedBreath && game.breathRecovery == .6f && game.breathRecoveryDelay == .2f, "breath recovers quickly after a short release delay");
                        game.BeginJourney(); game.Enemies.Clear();
                        thrower = new WhistleGame.Enemy { position = new Vector2(850, 390), hp = 1, ranged = true, throwTimer = 0 };
                        game.Enemies.Add(thrower);
                        break;
                    case 9:
                        Check(thrower.throwWindup > 0 && game.Projectiles.Count == 0, "ranged attack gives a warning before releasing its shuriken");
                        break;
                    case 10:
                        if (elapsed < .4) return;
                        Check(game.Projectiles.Count == 1, "thrower launches a moving projectile");
                        game.Stage.RenderNow();
                        var square = GameObject.Find("Square Sound Wave").GetComponent<LineRenderer>();
                        Check(square.enabled && square.positionCount == 4 && square.transform.parent.name == "Shuriken", "unrevealed shuriken displays a square wave at its actual position");
                        foreach (var body in square.transform.parent.GetComponentsInChildren<MeshRenderer>()) Check(!body.enabled, "projectile body stays hidden before whistle");
                        ScreenCapture.CaptureScreenshot("POCVerification/shuriken.png");
                        break;
                    case 11:
                        var shot = game.Projectiles[0];
                        Check(shot.position.z < game.Stage.EnemyWorld(thrower).z, "shuriken moves towards escort");
                        shot.position = new Vector3(0, 1.1f, 10);
                        var farRect = game.ProjectileHitRect(shot);
                        game.SetWhistle(true);
                        Check(!game.TrySlashSegment(new Vector2(farRect.xMin - 20, farRect.center.y), new Vector2(farRect.xMax + 20, farRect.center.y)), "distant shuriken cannot be deflected");
                        shot.position = new Vector3(0, 1.1f, 4); game.Stage.RenderNow();
                        Check(!GameObject.Find("Square Sound Wave").GetComponent<LineRenderer>().enabled, "whistle replaces square wave with visible shuriken");
                        var shotRect = game.ProjectileHitRect(shot);
                        Check(game.TrySlashSegment(new Vector2(shotRect.xMin - 20, shotRect.center.y), new Vector2(shotRect.xMax + 20, shotRect.center.y)) && game.Projectiles.Count == 0 && game.Health == 5, "drag deflects nearby shuriken without hurting escort");
                        game.SetWhistle(false); game.Enemies.Clear();
                        projectileHealth = game.Health;
                        game.Projectiles.Add(new WhistleGame.Projectile { position = new Vector3(0, 1.1f, .5f) });
                        break;
                    case 12:
                        Check(game.Health == projectileHealth - 1 && game.Projectiles.Count == 0, "unblocked shuriken damages escort once and disappears");
                        game.BeginJourney(); Check(game.Projectiles.Count == 0, "restart clears projectile hazards");
                        game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(430, 340), hp = 1 });
                        game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(1000, 390), hp = 1 });
                        clickArmor = new WhistleGame.Enemy { position = new Vector2(430, 450), hp = 2 };
                        offscreenEnemy = new WhistleGame.Enemy { position = new Vector2(650, 2000), hp = 1 };
                        game.Enemies.Add(clickArmor); game.Enemies.Add(offscreenEnemy);
                        game.Projectiles.Add(new WhistleGame.Projectile { position = new Vector3(-.5f, 1.1f, 4) });
                        game.Projectiles.Add(new WhistleGame.Projectile { position = new Vector3(0, 1.1f, 12) });
                        testToggleMode = true;
                        break;
                    case 13:
                        Check(game.swordMode == SwordMode.ClickAll, "Tab input switches from drag to click mode");
                        testToggleMode = false;
                        Check(!game.TryClickSlash() && game.Kills == 0, "click mode cannot attack before whistle");
                        mousePosition = game.ScreenPoint(new Vector2(800, 450));
                        testHolding = true; mouseHeld = false;
                        break;
                    case 14:
                        if (elapsed < .72) return;
                        Check(game.EchoStrength(game.Stage.EnemyWorld(clickArmor)) > .03f, "near enemy has been detected before click attack");
                        mouseHeld = true;
                        break;
                    case 15:
                        Check(game.Kills == 2 && clickArmor.hp == 1 && game.Enemies.Contains(offscreenEnemy), "one actual mouse click hits all visible enemies including distant targets, while excluding offscreen enemies");
                        Check(game.Projectiles.Count == 1 && game.Projectiles[0].position.z == 12, "click slash deflects nearby projectiles only");
                        Check(game.Cuts.Count >= 4 && game.Cuts[0].sweep, "click slash draws separate animated sword effects for its targets");
                        ScreenCapture.CaptureScreenshot("POCVerification/click-mode.png");
                        break;
                    case 16:
                        Check(game.Kills == 2 && clickArmor.hp == 1, "holding the mouse button never repeats click attacks");
                        testToggleMode = true;
                        break;
                    case 17:
                        Check(game.swordMode == SwordMode.Drag && clickArmor.hp == 1, "mode toggle during a held button does not trigger an extra attack");
                        testToggleMode = false; mouseHeld = false; testHolding = false;
                        RevealForCombat();
                        var armorRect = game.EnemyHitRect(clickArmor);
                        var armorStart = new Vector2(armorRect.xMin - 25, armorRect.center.y);
                        var armorEnd = new Vector2(armorRect.xMax + 25, armorRect.center.y);
                        game.SetSwordInput(armorStart, false); game.SetSwordInput(armorStart, true); game.SetSwordInput(armorEnd, true);
                        Check(!game.Enemies.Contains(clickArmor) && game.Kills == 3, "drag mode still cuts by mouse trajectory after switching back");
                        GameObject.Find("Sword Mode Toggle").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                        Check(game.swordMode == SwordMode.ClickAll && game.Kills == 3, "screen mode button toggles without attacking");
                        game.BeginJourney(); Check(game.swordMode == SwordMode.ClickAll, "selected sword mode persists across restart");
                        game.SetSwordMode(SwordMode.Drag);
                        echoNear = new WhistleGame.Enemy { position = new Vector2(430, 340), hp = 1 };
                        echoFar = new WhistleGame.Enemy { position = new Vector2(1000, 450), hp = 1 };
                        game.Enemies.Add(echoNear); game.Enemies.Add(echoFar);
                        testHolding = true; game.SetWhistle(true); game.Stage.RenderNow();
                        Check(game.Stage.VisibleEnemyCount == 0, "whistle does not instantly reveal enemy outlines");
                        break;
                    case 18:
                        Check(game.EchoStrength(game.Stage.EnemyWorld(echoNear)) > .03f && game.EchoStrength(game.Stage.EnemyWorld(echoFar)) == 0, "sound front reaches nearby enemy before distant enemy");
                        game.Stage.RenderNow();
                        Check(game.Stage.View.backgroundColor.maxColorComponent < .02f, "whistle leaves the background dark");
                        foreach (var root in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "Enemy Capsule" || t.name == "Road" || t.name == "Roadside Cube"))
                            foreach (var body in root.GetComponentsInChildren<MeshRenderer>()) Check(!body.enabled, "sonar never exposes filled enemy or environment meshes");
                        break;
                    case 19:
                        if (elapsed < .42) return;
                        Check(game.EchoStrength(game.Stage.EnemyWorld(echoFar)) > .03f, "distant enemy outline appears when sound front arrives");
                        ScreenCapture.CaptureScreenshot("POCVerification/sonar.png");
                        testHolding = false;
                        break;
                    case 20:
                        Check(!game.Revealed && game.EchoStrength(game.Stage.EnemyWorld(echoFar)) > .03f, "echo afterimage fades after releasing whistle without keeping attack enabled");
                        Check(!game.TrySlashSegment(new Vector2(100, 100), new Vector2(1500, 800)), "echo afterimage cannot be attacked after whistle release");
                        break;
                    case 21:
                        if (elapsed < .4) return;
                        game.Stage.RenderNow();
                        Check(game.EchoStrength(game.Stage.EnemyWorld(echoFar)) == 0 && game.Stage.VisibleEnemyCount == 0, "sonar outlines disappear after their brief return lifetime");
                        game.BeginJourney(); game.SpawnWave();
                        Check(game.Enemies.Any(e => e.kind == EnemyKind.Rusher) && game.Enemies.Any(e => e.kind == EnemyKind.Deceiver) && game.Enemies.Any(e => e.ranged), "waves mix rushers, false footsteps and ranged attackers");
                        game.Enemies.Clear();
                        rushEnemy = new WhistleGame.Enemy { position = new Vector2(530, 340), hp = 1, speed = 240, kind = EnemyKind.Rusher };
                        deceiver = new WhistleGame.Enemy { position = new Vector2(430, 450), hp = 1, kind = EnemyKind.Deceiver, fakeStep = 0 };
                        rushStart = rushEnemy.position.x;
                        game.Enemies.Add(rushEnemy); game.Enemies.Add(deceiver);
                        break;
                    case 22:
                        Check(rushEnemy.rushStage == 1 && rushEnemy.position.x == rushStart, "rusher stops to warn before its charge");
                        game.Stage.RenderNow();
                        var falseWaves = UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Where(w => w.name.StartsWith("False Footstep")).ToArray();
                        Check(falseWaves.Length == 3 && falseWaves.Any(w => w.enabled) && game.Enemies.Count == 2, "deceiver adds false sound waves without spawning a fake enemy");
                        ScreenCapture.CaptureScreenshot("POCVerification/false-footsteps.png");
                        break;
                    case 23:
                        Check(rushEnemy.rushStage == 2 && rushEnemy.position.x < rushStart, "rusher accelerates towards escort after its warning");
                        RevealForCombat(); game.Stage.RenderNow();
                        Check(!UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Any(w => w.name.StartsWith("False Footstep") && w.enabled), "sonar removes the deceiver's false cues and shows its real outline");
                        var fakeAim = game.CanvasPoint(game.Stage.View.WorldToScreenPoint(game.Stage.FakeFootstepWorld(deceiver) + Vector3.up * .9f));
                        Check(!game.TrySlashSegment(fakeAim - Vector2.right * 20, fakeAim + Vector2.right * 20), "swinging at a false footstep cannot damage a real enemy");
                        game.BeginJourney();
                        game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(325, 390), hp = 1, windingUp = true, attack = game.meleeWindup - .08f });
                        testHolding = true; RevealForCombat();
                        break;
                    case 24:
                        var perfectEnemy = game.Enemies[0]; float beforeReward = game.Breath;
                        Check(beforeReward < game.whistleDuration, "counterplay check has spent breath to recover");
                        Check(game.TrySlashSegment(game.EnemyAim(perfectEnemy) - Vector2.right * 65, game.EnemyAim(perfectEnemy) + Vector2.right * 65) && game.Perfects == 1 && game.Breath > beforeReward && game.Health == 5, "last-moment melee kill restores breath before damage");
                        var perfectShot = new WhistleGame.Projectile { position = new Vector3(0, 1.1f, 1.5f) }; game.Projectiles.Add(perfectShot);
                        var perfectRect = game.ProjectileHitRect(perfectShot);
                        Check(game.TrySlashSegment(new Vector2(perfectRect.xMin - 25, perfectRect.center.y), new Vector2(perfectRect.xMax + 25, perfectRect.center.y)) && game.Perfects == 2 && game.Projectiles.Count == 0, "last-moment projectile deflection also awards breath");
                        var earlyShot = new WhistleGame.Projectile { position = new Vector3(0, 1.1f, 4) }; game.Projectiles.Add(earlyShot);
                        var earlyRect = game.ProjectileHitRect(earlyShot);
                        Check(game.TrySlashSegment(new Vector2(earlyRect.xMin - 25, earlyRect.center.y), new Vector2(earlyRect.xMax + 25, earlyRect.center.y)) && game.Perfects == 2, "early safe deflection does not award a perfect bonus");
                        ScreenCapture.CaptureScreenshot("POCVerification/perfect.png");
                        break;
                    case 25:
                        game.BeginJourney(); game.SetSwordMode(SwordMode.ClickAll);
                        for (int i = 0; i < 2; i++) game.Enemies.Add(new WhistleGame.Enemy { position = new Vector2(325, 350 + i * 80), hp = 1, windingUp = true, attack = game.meleeWindup - .08f });
                        RevealForCombat();
                        Check(game.TryClickSlash() && game.Kills == 2 && game.Perfects == 1, "click counterattack grants one reward per attack even with multiple perfect targets");
                        Check(!game.TryClickSlash() && game.Perfects == 1, "repeat click cannot farm the same perfect bonus");
                        game.SetSwordMode(SwordMode.Drag); testHolding = false;
                        break;
                    case 26:
                        game.BeginJourney();
                        echoNear = new WhistleGame.Enemy { position = new Vector2(430, 340), hp = 1 };
                        echoFar = new WhistleGame.Enemy { position = new Vector2(1000, 450), hp = 1 };
                        game.Enemies.Add(echoNear); game.Enemies.Add(echoFar);
                        game.SetWhistle(true); game.SetWhistle(false);
                        break;
                    case 27:
                        Check(game.EchoStrength(game.Stage.EnemyWorld(echoNear)) > .03f && game.EchoStrength(game.Stage.EnemyWorld(echoFar)) == 0, "a short whistle detects nearby threats without revealing distant enemies");
                        game.BeginJourney();
                        CheckPooling();
                        inputProbe.enabled = false; UnityEngine.Object.Destroy(inputProbe.gameObject); inputProbe = null;
                        File.WriteAllText("POCVerification/result.txt", "PASS: prefab scene references, gameplay, sonar, both sword modes, counterplay, pool prewarm, instance reuse, transform/material reset, projectile/effect reset, double-return guard, no new instantiation during reuse, restart returns all pooled objects.\n");
                        InputSystem.RemoveDevice(testKeyboard);
                        InputSystem.RemoveDevice(testMouse); testMouse = null;
                        InputSystem.settings.backgroundBehavior = previousBackground;
                        InputSystem.settings.updateMode = previousInputUpdate;
                        SessionState.SetBool(TestKey, false);
                        EditorApplication.isPlaying = false;
                        Debug.Log("WHISTLE SMOKE: PASS");
                        if (Application.isBatchMode) EditorApplication.Exit(0);
                        return;
                }
                phase++; phaseTime = game.Clock;
            }
            catch (Exception e)
            {
                Directory.CreateDirectory("POCVerification"); File.WriteAllText("POCVerification/result.txt", "FAIL: " + e);
                Debug.LogException(e); SessionState.SetBool(TestKey, false); EditorApplication.isPlaying = false;
                if (inputProbe != null) { inputProbe.enabled = false; UnityEngine.Object.Destroy(inputProbe.gameObject); inputProbe = null; }
                if (testKeyboard != null) InputSystem.RemoveDevice(testKeyboard);
                if (testMouse != null) { InputSystem.RemoveDevice(testMouse); testMouse = null; }
                InputSystem.settings.backgroundBehavior = previousBackground;
                InputSystem.settings.updateMode = previousInputUpdate;
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}



