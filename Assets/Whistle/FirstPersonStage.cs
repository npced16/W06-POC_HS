using System;
using System.Collections.Generic;
using UnityEngine;

namespace WhistlePOC
{
    public sealed class FirstPersonStage : MonoBehaviour
    {
        public WhistleGame Game;
        [field: SerializeField] public Camera View { get; private set; }
        [Header("Enemy prefabs")]
        public GameObject meleePrefab, armoredPrefab, rangedPrefab, shurikenPrefab;
        public GameObject rusherPrefab, deceiverPrefab, armoredDeceiverPrefab;
        [Header("Pooled effects")]
        public GameObject scanWavePrefab, slashEffectPrefab, dragTrailPrefab, perfectEffectPrefab;
        public ObjectPoolManager pool;
        public Transform previewRoot;
        [SerializeField] List<Transform> posts = new List<Transform>();
        [SerializeField] List<LineRenderer> roadContours = new List<LineRenderer>();
        // Kept for the one-time editor migration of the previous authored scene.
        [SerializeField, HideInInspector] List<LineRenderer> scanRings = new List<LineRenderer>();
        [SerializeField, HideInInspector] List<Material> environmentEcho = new List<Material>();
        [SerializeField] Transform sword;
        public int VisibleEnemyCount { get; private set; }
        public int AttackableEnemyCount { get; private set; }
        readonly Dictionary<WhistleGame.Enemy, PooledEnemyVisual> enemies = new Dictionary<WhistleGame.Enemy, PooledEnemyVisual>();
        readonly Dictionary<WhistleGame.Projectile, PooledProjectileVisual> projectiles = new Dictionary<WhistleGame.Projectile, PooledProjectileVisual>();
        readonly Dictionary<float, LineRenderer> scans = new Dictionary<float, LineRenderer>();
        readonly Dictionary<WhistleGame.Cut, PooledSwordGraphic> cuts = new Dictionary<WhistleGame.Cut, PooledSwordGraphic>();
        readonly List<WhistleGame.Enemy> removedEnemies = new List<WhistleGame.Enemy>();
        readonly List<WhistleGame.Projectile> removedShots = new List<WhistleGame.Projectile>();
        readonly List<float> removedScans = new List<float>();
        readonly List<WhistleGame.Cut> removedCuts = new List<WhistleGame.Cut>();
        readonly List<LineRenderer[]> postContours = new List<LineRenderer[]>();
        readonly List<Vector3> postHomes = new List<Vector3>();
        MaterialPropertyBlock tint;
        PooledSwordGraphic trail, perfect;
        Vector3 cameraHome;
        public void Initialize(WhistleGame game)
        {
            tint = new MaterialPropertyBlock();
            Game = game; View = Camera.main;
            if (pool == null || View == null || sword == null || meleePrefab == null || scanWavePrefab == null)
                throw new InvalidOperationException("Assign scene, prefabs and ObjectPoolManager before playing.");
            pool.Initialize(); cameraHome = View.transform.position;
            if (previewRoot != null) previewRoot.gameObject.SetActive(false);
            postContours.Clear(); postHomes.Clear();
            foreach (var post in posts) { postContours.Add(post.GetComponentsInChildren<LineRenderer>()); postHomes.Add(post.localPosition); }
            foreach (var mesh in GetComponentsInChildren<MeshRenderer>()) mesh.enabled = false;
            RenderNow();
        }
        void ColorLine(LineRenderer line, Color color)
        {
            tint.SetColor("_BaseColor", color); tint.SetColor("_Color", color); line.SetPropertyBlock(tint);
        }
        void CircleWave(LineRenderer wave, Vector3 localCenter, float radius, float width, Color color)
        {
            wave.transform.localPosition = localCenter; wave.transform.rotation = View.transform.rotation;
            wave.transform.localScale = new Vector3(radius, radius, 1); wave.widthMultiplier = width; ColorLine(wave, color);
        }
        public Vector3 EnemyWorld(WhistleGame.Enemy enemy) { return new Vector3((enemy.position.y - 390) / 70, 0, Mathf.Max(2.8f, 2.8f + (enemy.position.x - 325) / 48)); }
        public Vector3 EnemyScreen(WhistleGame.Enemy enemy) { return View.WorldToScreenPoint(EnemyWorld(enemy) + Vector3.up * .9f); }
        public Vector3 FakeFootstepWorld(WhistleGame.Enemy enemy) { return EnemyWorld(enemy) + new Vector3(enemy.decoyOffset, 0, 1.2f); }
        public float EnemyDistance(WhistleGame.Enemy enemy) { var p = EnemyWorld(enemy); return new Vector2(p.x, p.z).magnitude; }
        public bool InSwordRange(WhistleGame.Enemy enemy) { return EnemyDistance(enemy) <= Game.swordReach; }
        GameObject EnemyPrefab(WhistleGame.Enemy e)
        {
            if (e.hp > 1) return e.kind == EnemyKind.Deceiver ? armoredDeceiverPrefab : armoredPrefab;
            if (e.ranged) return rangedPrefab;
            return e.kind == EnemyKind.Rusher ? rusherPrefab : e.kind == EnemyKind.Deceiver ? deceiverPrefab : meleePrefab;
        }
        public void RenderNow()
        {
            if (Game == null || View == null || pool == null || !Application.isPlaying) return;
            bool visible = Game.Revealed; View.backgroundColor = new Color(.006f, .006f, .006f);
            VisibleEnemyCount = AttackableEnemyCount = 0;
            if (Game.State != JourneyState.Walking && Game.State != JourneyState.Paused) { if (pool.ActiveCount > 0) ReleaseAllVisuals(); return; }
            foreach (var e in Game.Enemies)
            {
                if (!enemies.TryGetValue(e, out var view)) { view = pool.Spawn(EnemyPrefab(e), transform).GetComponent<PooledEnemyVisual>(); enemies.Add(e, view); }
                view.transform.position = EnemyWorld(e); view.HideBody();
                bool inRange = InSwordRange(e); float echo = Game.EchoStrength(EnemyWorld(e));
                foreach (var contour in view.contours) { contour.enabled = echo > .03f; ColorLine(contour, new Color(1, 1, 1, echo * (inRange ? .95f : .6f))); }
                if (echo > .03f) VisibleEnemyCount++;
                if (visible && echo > .03f && inRange && Game.State == JourneyState.Walking) AttackableEnemyCount++;
                float proximity = Mathf.InverseLerp(28, 2.8f, EnemyDistance(e));
                bool warning = e.windingUp || e.throwWindup > 0 || e.rushStage == 1;
                float growth = e.rushStage == 1 ? 1 - e.rushTimer / Mathf.Max(.1f, Game.rushWindup) : warning ? e.windingUp ? Mathf.Clamp01(e.attack / Game.meleeWindup) : 1 - e.throwWindup / .55f : Mathf.Clamp01(1 - e.pulse);
                float envelope = warning ? .9f : Mathf.SmoothStep(0, 1, growth / .12f) * Mathf.SmoothStep(0, 1, e.pulse);
                if (view.weapon != null) view.weapon.localRotation = Quaternion.Euler(0, 0, warning ? -65 : 0);
                for (int ring = 0; ring < view.footsteps.Length; ring++)
                {
                    var wave = view.footsteps[ring]; wave.enabled = !visible && ring == Mathf.Max(0, e.waveIndex) && (warning || (e.waveIndex >= 0 && e.pulse > .015f));
                    CircleWave(wave, Vector3.up * .9f, Mathf.Lerp(.08f, Mathf.Lerp(.8f, 1.15f, proximity), growth), Mathf.Lerp(.027f, .038f, proximity) * (1 - growth * .45f), warning ? new Color(1, 1, 1, envelope) : new Color(.65f, .65f, .65f, envelope * Mathf.Lerp(.48f, .95f, proximity)));
                }
                for (int ring = 0; ring < view.falseFootsteps.Length; ring++)
                {
                    var wave = view.falseFootsteps[ring]; wave.enabled = !visible && ring == Mathf.Max(0, e.waveIndex) && e.fakePulse > .015f;
                    CircleWave(wave, FakeFootstepWorld(e) - EnemyWorld(e) + Vector3.up * .9f, Mathf.Lerp(.08f, .85f, 1 - e.fakePulse), .027f, new Color(.65f, .65f, .65f, Mathf.SmoothStep(0, 1, (1 - e.fakePulse) / .12f) * e.fakePulse * .7f));
                }
            }
            removedEnemies.Clear(); foreach (var e in enemies.Keys) if (!Game.Enemies.Contains(e)) removedEnemies.Add(e);
            foreach (var e in removedEnemies) { pool.Return(enemies[e].gameObject); enemies.Remove(e); }
            foreach (var shot in Game.Projectiles)
            {
                if (!projectiles.TryGetValue(shot, out var view)) { view = pool.Spawn(shurikenPrefab, transform).GetComponent<PooledProjectileVisual>(); projectiles.Add(shot, view); }
                view.transform.position = shot.position;
                foreach (var body in view.body) { body.enabled = false; body.transform.localRotation = Quaternion.Euler(0, 0, shot.age * 600 + (body.name == "Star" ? 45 : 0)); }
                float echo = Game.EchoStrength(shot.position);
                foreach (var contour in view.contours) { contour.enabled = visible || echo > .03f; ColorLine(contour, new Color(1, 1, 1, visible ? .55f + echo * .4f : echo * .7f)); }
                bool inRange = new Vector2(shot.position.x, shot.position.z).magnitude <= Game.swordReach;
                var wave = view.soundWave; wave.enabled = !visible; wave.transform.rotation = View.transform.rotation;
                float phase = Mathf.Repeat(shot.age * 2.5f, 1); float radius = Mathf.Lerp(.16f, .55f, phase);
                wave.transform.localScale = new Vector3(radius, radius, 1); wave.widthMultiplier = .035f;
                ColorLine(wave, new Color(1, 1, 1, inRange ? Mathf.Lerp(.95f, .45f, phase) : Mathf.Lerp(.8f, .3f, phase)));
            }
            removedShots.Clear(); foreach (var shot in projectiles.Keys) if (!Game.Projectiles.Contains(shot)) removedShots.Add(shot);
            foreach (var shot in removedShots) { pool.Return(projectiles[shot].gameObject); projectiles.Remove(shot); }
            for (int i = 0; i < posts.Count; i++)
            {
                var p = postHomes[i]; p.z = 3 + Mathf.Repeat(p.z - 3 - Game.Progress * 70, 48); posts[i].localPosition = p;
                float echo = Game.EchoStrength(p);
                foreach (var contour in postContours[i]) { contour.enabled = echo > .03f; ColorLine(contour, new Color(.5f, .5f, .5f, echo * .25f)); }
                var guide = roadContours[i]; guide.transform.localPosition = new Vector3(p.x * .75f, .02f, p.z); echo = Game.EchoStrength(guide.transform.position);
                guide.enabled = echo > .03f; ColorLine(guide, new Color(.6f, .6f, .6f, echo * .25f));
            }
            foreach (float emission in Game.EchoPulses)
            {
                float radius = (Game.Clock - emission) * Game.echoSpeed; float range = Game.EchoRange(emission);
                if (radius <= 0 || radius > range) continue;
                if (!scans.TryGetValue(emission, out var ring)) { ring = pool.Spawn(scanWavePrefab, transform).GetComponent<LineRenderer>(); scans.Add(emission, ring); }
                ring.enabled = true; ring.transform.localPosition = new Vector3(0, .03f, 0); ring.transform.localScale = new Vector3(radius, 1, radius);
                ColorLine(ring, new Color(.55f, .55f, .55f, Mathf.Clamp01(1 - radius / Mathf.Max(1, range)) * .32f));
            }
            removedScans.Clear(); foreach (float emission in scans.Keys) if (!Game.EchoPulses.Contains(emission) || (Game.Clock - emission) * Game.echoSpeed > Game.EchoRange(emission)) removedScans.Add(emission);
            foreach (float emission in removedScans) { pool.Return(scans[emission].gameObject); scans.Remove(emission); }
            SyncSwordEffects();
            float swing = Game.Cuts.Count > 0 ? Mathf.Sin(Mathf.Clamp01(Game.Cuts[Game.Cuts.Count - 1].age / .25f) * Mathf.PI) : 0;
            float angle = Game.SwordDragging ? Mathf.Atan2(Game.SwordDirection.y, Game.SwordDirection.x) * Mathf.Rad2Deg - 90 : -25;
            if (Game.swordMode == SwordMode.ClickAll) angle -= swing * 100;
            var rotation = Quaternion.Euler(0, 0, angle); var position = new Vector3(.40f, -.42f, .65f);
            if (Game.swordMode == SwordMode.ClickAll) position += new Vector3(-swing * .45f, swing * .3f, 0);
            if (Game.SwordDragging) { var screen = Game.ScreenPoint(Game.Cursor); var tip = View.transform.InverseTransformPoint(View.ScreenToWorldPoint(new Vector3(screen.x, screen.y, .65f))); position = tip - rotation * new Vector3(0, .64f, 0); }
            float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 32);
            sword.localRotation = Quaternion.Slerp(sword.localRotation, rotation, follow); sword.localPosition = Vector3.Lerp(sword.localPosition, position, follow);
            View.transform.position = cameraHome + View.transform.forward * swing * .16f;
        }
        void SyncSwordEffects()
        {
            foreach (var cut in Game.Cuts)
            {
                if (cuts.ContainsKey(cut)) continue;
                var effect = pool.Spawn(slashEffectPrefab, Game.EffectParent).GetComponent<PooledSwordGraphic>(); effect.Game = Game; effect.Cut = cut; cut.lifetime = effect.lifetime; cuts.Add(cut, effect);
            }
            removedCuts.Clear(); foreach (var cut in cuts.Keys) if (!Game.Cuts.Contains(cut)) removedCuts.Add(cut);
            foreach (var cut in removedCuts) { pool.Return(cuts[cut].gameObject); cuts.Remove(cut); }
            if (Game.SwordTrail.Count > 1) { if (trail == null) { trail = pool.Spawn(dragTrailPrefab, Game.EffectParent).GetComponent<PooledSwordGraphic>(); trail.Game = Game; } }
            else if (trail != null) { pool.Return(trail.gameObject); trail = null; }
            if (Game.PerfectFlash > 0) { if (perfect == null) { perfect = pool.Spawn(perfectEffectPrefab, Game.EffectParent).GetComponent<PooledSwordGraphic>(); perfect.Game = Game; } }
            else if (perfect != null) { pool.Return(perfect.gameObject); perfect = null; }
        }
        public void ReleaseAllVisuals()
        {
            if (pool == null) return;
            pool.ReturnAll(); enemies.Clear(); projectiles.Clear(); scans.Clear(); cuts.Clear(); trail = perfect = null;
            VisibleEnemyCount = AttackableEnemyCount = 0;
        }
        void LateUpdate() { RenderNow(); }
    }
}
