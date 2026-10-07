using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace WhistlePOC
{
    public enum JourneyState { Title, Walking, Camp, Victory, Defeat, Paused }
    public enum SwordMode { Drag, ClickAll }
    public enum EnemyKind { Walker, Rusher, Deceiver }

    public sealed class WhistleGame : MonoBehaviour
    {
        [Header("POC tuning")]
        public float whistleDuration = 1.1f;
        public float breathRecovery = 0.6f;
        public float breathRecoveryDelay = 0.2f;
        public float enemySpeedMultiplier = 1.5f;
        public float meleeWindup = 0.45f;
        public float projectileSpeed = 11f;
        public float echoSpeed = 45f;
        public float echoInterval = .6f;
        public float echoLifetime = .48f;
        public float slowMotionScale = 0.12f;
        public float swordReach = 6.5f;
        public float legDuration = 24f;
        public int swordDamage = 1;
        public int escortMaxHealth = 5;
        public SwordMode swordMode = SwordMode.Drag;
        [Header("Sound reading and counterplay")]
        public float shortEchoRange = 8f;
        public float longEchoRange = 30f;
        public float longWhistleThreshold = .3f;
        public float rushTriggerDistance = 12f;
        public float rushWindup = .45f;
        public float rushSpeedMultiplier = 3.2f;
        public float rushDuration = .7f;
        public float perfectWindow = .16f;
        public float perfectProjectileDistance = 1.8f;
        public float perfectBreathReward = .18f;
        [Header("Scene setup")]
        public Transform[] spawnPoints;
        [SerializeField, HideInInspector] bool sceneAuthored;
        public JourneyState State { get; private set; } = JourneyState.Title;
        public bool Revealed { get; private set; }
        public float Breath { get; private set; }
        public int Health { get; private set; }
        public int Kills { get; private set; }
        public int Combo { get; private set; }
        public int Perfects { get; private set; }
        public float PerfectFlash { get; private set; }
        public float WhistleHeld { get; private set; }
        public int Leg { get; private set; } = 1;
        public float Progress { get; private set; }
        public float Clock { get; private set; }
        public Vector2 Hero { get; private set; } = new Vector2(420, 390);
        public readonly List<Enemy> Enemies = new List<Enemy>();
        public readonly List<Projectile> Projectiles = new List<Projectile>();
        public readonly List<float> EchoPulses = new List<float>();
        readonly Dictionary<float, float> echoRanges = new Dictionary<float, float>();
        public readonly List<Cut> Cuts = new List<Cut>();
        public Vector2 Cursor { get; private set; }
        public float HitFlash { get; private set; }
        public string Message { get; private set; } = "";
        public sealed class Enemy
        {
            public Vector2 position;
            public int hp;
            public int waveIndex = -1;
            public bool ranged, windingUp;
            public EnemyKind kind;
            public int rushStage;
            public float rushTimer, fakeStep = .35f, fakeAge, fakePulse, fakeDuration = 1.1f, decoyOffset = 2.2f;
            public float throwTimer = 1.1f, throwWindup;
            public float speed, step, pulse, attack, nextSlash, waveAge, waveDuration = 1.2f;
        }
        public sealed class Projectile { public Vector3 position, velocity; public float age; }
        public sealed class Cut { public Vector2 from, to; public float age, lifetime = .45f; public bool hit, sweep; }
        public struct TrailPoint { public Vector2 position; public float born; public bool start; }
        public readonly List<TrailPoint> SwordTrail = new List<TrailPoint>();
        public Vector2 SwordDirection { get; private set; } = Vector2.up;
        public bool SwordDragging { get; private set; }
        Vector2 dragFrom;
        bool dragStarted, swordButtonHeld, tabHeld;
        [SerializeField, HideInInspector] WhistleInk ink;
        [field: SerializeField] public FirstPersonStage Stage { get; private set; }
        [SerializeField, HideInInspector] TMP_Text status, instructions, popupTitle, popupBody, toast;
        [SerializeField, HideInInspector] GameObject panel;
        [SerializeField, HideInInspector] UnityEngine.UI.Image breathFill, progressFill;
        [SerializeField, HideInInspector] UnityEngine.UI.Button primary, secondary, modeButton;
        [SerializeField, HideInInspector] TMP_Text primaryLabel, secondaryLabel, modeLabel;
        [SerializeField, HideInInspector] AudioSource whistle, effects;
        [SerializeField, HideInInspector] AudioClip stepClip, cutClip, hurtClip, warningClip, throwClip;
        [SerializeField, HideInInspector] TMP_FontAsset font;
        float initialWhistleDuration;
        int initialSwordDamage;
        float waveTimer, slashTimer, comboTimer, messageTimer, dashTimer;
        float recoveryTimer;
        float nextClickSlash;
        bool exhausted;
        bool longEchoSent;
        int waveSerial;
        JourneyState beforePause;
        readonly Vector2 home = new Vector2(420, 390);

        void Awake()
        {
            Application.runInBackground = true;
            Time.timeScale = 1;
            Health = escortMaxHealth;
            Breath = whistleDuration;
            initialWhistleDuration = whistleDuration; initialSwordDamage = swordDamage;
            if (ink == null || Stage == null || whistle == null || effects == null) throw new InvalidOperationException("Use the authored Whistle Game prefab and assign its scene references.");
            ink.Game = this;
            Stage.Initialize(this);
            whistle.volume = 0; whistle.loop = true; whistle.Play();
            modeButton.onClick.RemoveAllListeners(); modeButton.onClick.AddListener(ToggleSwordMode);
            ShowTitle();
        }
        void ShowTitle()
        {
            ShowPanel("흑 · 백", "눈을 감은 검객, 흑. 길을 여는 휘파람, 백.\n\n휘파람의 반향이 어둠 속 윤곽을 짧게 그린다.\n짧게는 가까이, 길게는 멀리. 위기 직전 베면 숨을 돌려받는다.\n\nSPACE 짧게/길게 감지 · TAB 검 모드 전환 · 자동 전진\n드래그로 직접 베기 / 클릭 한 번으로 감지된 적 일괄 베기\n세 구간을 통과해 백을 산문까지 호위하세요.", "호위 시작", BeginJourney);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            Clock += dt;
            bool tab = Keyboard.current != null && Keyboard.current.tabKey.isPressed;
            if (tab && !tabHeld) ToggleSwordMode();
            tabHeld = tab;
            EchoPulses.RemoveAll(p => { if (Clock - p <= 2) return false; echoRanges.Remove(p); return true; });
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) BeginJourney();
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
            if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame && panel.activeSelf)
                primary.onClick.Invoke();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ink.rectTransform,
                Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero, null, out var local);
            Cursor = local + new Vector2(800, 450);
            slashTimer -= dt;
            comboTimer -= dt;
            if (comboTimer <= 0 && !Revealed) Combo = 0;
            HitFlash = Mathf.Max(0, HitFlash - dt * 3);
            PerfectFlash = Mathf.Max(0, PerfectFlash - dt * 2.5f);
            messageTimer -= dt;
            for (int i = Cuts.Count - 1; i >= 0; i--) { Cuts[i].age += dt; if (Cuts[i].age > Cuts[i].lifetime) Cuts.RemoveAt(i); }
            SwordTrail.RemoveAll(p => Clock - p.born > .22f);
            if (State == JourneyState.Walking)
            {
                bool hold = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
                    (Mouse.current != null && Mouse.current.rightButton.isPressed);
                if (!hold) exhausted = false;
                SetWhistle(hold && !exhausted && Breath > 0);
                if (Revealed)
                {
                    WhistleHeld += dt;
                    if (!longEchoSent && WhistleHeld >= longWhistleThreshold) { EmitEcho(longEchoRange); longEchoSent = true; }
                    else if (EchoPulses.Count == 0 || Clock - EchoPulses[EchoPulses.Count - 1] >= echoInterval) EmitEcho(longEchoSent ? longEchoRange : shortEchoRange);
                    recoveryTimer = breathRecoveryDelay;
                    Breath = Mathf.Max(0, Breath - dt);
                    if (Breath <= 0) { exhausted = true; SetWhistle(false); Say("숨이 다했다 — 놓고 다시 숨을 고르세요"); }
                }
                else
                {
                    float recoveryDt = Mathf.Max(0, dt - recoveryTimer);
                    recoveryTimer = Mathf.Max(0, recoveryTimer - dt);
                    Breath = Mathf.Min(whistleDuration, Breath + recoveryDt * breathRecovery);
                }
                float worldDt = dt * (Revealed ? slowMotionScale : 1);
                Progress = Mathf.Min(1, Progress + worldDt / legDuration);
                waveTimer -= worldDt;
                if (waveTimer <= 0 && Progress < 0.88f) { SpawnWave(); waveTimer = 3.2f - Leg * 0.35f; }
                AdvanceEnemies(worldDt);
                if (State == JourneyState.Walking) AdvanceProjectiles(worldDt);
                UpdateSwordInput();
                dashTimer -= dt;
                if (dashTimer <= 0) Hero = Vector2.Lerp(Hero, home, 1 - Mathf.Exp(-dt * 12));
                if (Progress >= 1 && Enemies.Count == 0 && Projectiles.Count == 0 && State == JourneyState.Walking) Arrive();
            }
            else { SetWhistle(false); dragStarted = false; SwordDragging = false; }
            whistle.volume = Mathf.MoveTowards(whistle.volume, Revealed ? 0.16f : 0, dt * 2);
            UpdateHUD();
            ink.SetVerticesDirty();
        }
        void UpdateSwordInput()
        {
            bool overMode = Mouse.current != null && RectTransformUtility.RectangleContainsScreenPoint(modeButton.GetComponent<RectTransform>(), Mouse.current.position.ReadValue(), null);
            if (overMode)
            {
                swordButtonHeld = Mouse.current.leftButton.isPressed; dragStarted = false; SwordDragging = false;
                return;
            }
            SetSwordInput(Cursor, Mouse.current != null && Mouse.current.leftButton.isPressed);
        }
        public void ToggleSwordMode() { SetSwordMode(swordMode == SwordMode.Drag ? SwordMode.ClickAll : SwordMode.Drag); }
        public void SetSwordMode(SwordMode mode)
        {
            swordMode = mode; dragStarted = false; SwordDragging = false; SwordTrail.Clear();
            swordButtonHeld = Mouse.current != null && Mouse.current.leftButton.isPressed;
            Say(mode == SwordMode.Drag ? "검 모드: 드래그 — 궤적으로 직접 베기" : "검 모드: 클릭 — 시야 안 적을 한 번에 베기");
        }
        public void SetSwordInput(Vector2 pointer, bool held)
        {
            bool pressed = held && !swordButtonHeld; swordButtonHeld = held; Cursor = pointer;
            if (swordMode == SwordMode.Drag) SetSwordDrag(pointer, held);
            else
            {
                dragStarted = false; SwordDragging = false;
                if (pressed) TryClickSlash();
            }
        }
        public void SetSwordDrag(Vector2 pointer, bool held)
        {
            Cursor = pointer;
            SwordDragging = swordMode == SwordMode.Drag && State == JourneyState.Walking && Revealed && held;
            if (!SwordDragging) { dragStarted = false; return; }
            if (!dragStarted)
            {
                dragFrom = pointer; dragStarted = true;
                SwordTrail.Add(new TrailPoint { position = pointer, born = Clock, start = true }); return;
            }
            var delta = pointer - dragFrom;
            if (delta.sqrMagnitude < 4) return;
            SwordDirection = Vector2.Lerp(SwordDirection, delta.normalized, .65f).normalized;
            int samples = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / 6), 1, 60);
            for (int i = 1; i <= samples; i++) SwordTrail.Add(new TrailPoint { position = Vector2.Lerp(dragFrom, pointer, i / (float)samples), born = Clock });
            TrySlashSegment(dragFrom, pointer); dragFrom = pointer;
        }

        public void BeginJourney()
        {
            Leg = 1; swordDamage = initialSwordDamage; whistleDuration = initialWhistleDuration;
            Health = escortMaxHealth; Kills = Combo = Perfects = 0; PerfectFlash = 0; waveSerial = 0; Enemies.Clear(); Projectiles.Clear(); Cuts.Clear(); SwordTrail.Clear(); Hero = home;
            StartLeg();
        }
        void StartLeg()
        {
            Stage.ReleaseAllVisuals();
            SetWhistle(false);
            EchoPulses.Clear(); echoRanges.Clear(); WhistleHeld = 0; longEchoSent = false;
            State = JourneyState.Walking; Progress = 0; Breath = whistleDuration;
            waveTimer = .7f; recoveryTimer = 0; nextClickSlash = 0; exhausted = false; slashTimer = 0; dragStarted = false; swordButtonHeld = Mouse.current != null && Mouse.current.leftButton.isPressed; SwordDragging = false; Enemies.Clear(); Projectiles.Clear(); panel.SetActive(false);
            Say("멎은 발소리는 돌진 예고 — 가짜 발자국은 반향으로 구분하세요");
        }
        public void SetWhistle(bool active)
        {
            if (!Revealed && active && State == JourneyState.Walking) { WhistleHeld = 0; longEchoSent = false; EmitEcho(shortEchoRange); }
            if (Revealed && !active) recoveryTimer = breathRecoveryDelay;
            Revealed = active && State == JourneyState.Walking;
            Time.timeScale = State == JourneyState.Paused ? 0 : Revealed ? slowMotionScale : 1;
        }
        void EmitEcho(float range) { EchoPulses.Add(Clock); echoRanges[Clock] = range; }
        public float EchoRange(float emission) { return echoRanges.TryGetValue(emission, out float range) ? range : longEchoRange; }
        public float EchoStrength(Vector3 position)
        {
            float distance = new Vector2(position.x, position.z).magnitude;
            float strength = 0;
            foreach (float emission in EchoPulses)
            {
                if (distance > EchoRange(emission)) continue;
                float age = Clock - emission - distance / Mathf.Max(1, echoSpeed);
                if (age < 0 || age >= echoLifetime) continue;
                strength = Mathf.Max(strength, Mathf.SmoothStep(0, 1, age / .025f) * Mathf.Pow(1 - age / echoLifetime, .65f));
            }
            return strength;
        }
        public void SpawnWave()
        {
            int count = 3 + (Leg >= 2 ? 1 : 0);
            for (int i = 0; i < count; i++)
            {
                Vector2 position = new Vector2(1350 + i * 125, 265 + i * 87 + UnityEngine.Random.Range(-20f, 20f));
                if (spawnPoints != null && spawnPoints.Length > 0 && spawnPoints[i % spawnPoints.Length] != null)
                {
                    var point = spawnPoints[i % spawnPoints.Length].position;
                    position = new Vector2(325 + (point.z - 2.8f) * 48, 390 + point.x * 70);
                }
                Enemies.Add(new Enemy { position = position,
                    hp = Leg >= 2 && i == 1 ? 2 : 1, ranged = i == count - 1,
                    kind = i == count - 1 ? EnemyKind.Walker : i == 0 && waveSerial % 2 == 0 ? EnemyKind.Rusher : i == 1 ? EnemyKind.Deceiver : EnemyKind.Walker,
                    decoyOffset = (waveSerial % 2 == 0 ? 1 : -1) * 2.2f,
                    speed = (150 + Leg * 12 + UnityEngine.Random.Range(-28f, 35f)) * enemySpeedMultiplier, step = i * .21f, waveAge = 0, pulse = 0 });
            }
            waveSerial++;
        }
        void AdvanceEnemies(float dt)
        {
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (e.kind == EnemyKind.Rusher && e.position.x > 325)
                {
                    if (e.rushStage == 0 && Stage.EnemyDistance(e) <= rushTriggerDistance)
                    {
                        e.rushStage = 1; e.rushTimer = rushWindup; Play(warningClip, .2f);
                        Say("발걸음이 멎었다 — 곧 돌진한다!");
                    }
                    else if (e.rushStage == 1)
                    {
                        e.rushTimer -= dt;
                        if (e.rushTimer <= 0) { e.rushStage = 2; e.rushTimer = rushDuration; e.step = 0; }
                    }
                    else if (e.rushStage == 2)
                    {
                        e.rushTimer -= dt;
                        if (e.rushTimer <= 0) e.rushStage = 3;
                    }
                }
                if (e.kind == EnemyKind.Deceiver)
                {
                    e.fakeAge += dt; e.fakeStep -= dt;
                    e.fakePulse = Mathf.Clamp01(1 - e.fakeAge / Mathf.Max(.1f, e.fakeDuration));
                    if (e.fakeStep <= 0)
                    {
                        e.fakeStep = Mathf.Lerp(1.3f, .65f, Mathf.InverseLerp(25, 3, Stage.EnemyDistance(e)));
                        e.fakeDuration = e.fakeStep * .8f; e.fakeAge = 0; e.fakePulse = 1;
                        Play(stepClip, .07f, Mathf.Clamp(Stage.FakeFootstepWorld(e).x / 5, -1, 1));
                    }
                }
                e.waveAge += dt;
                e.pulse = Mathf.Clamp01(1 - e.waveAge / e.waveDuration);
                e.step -= dt;
                if (e.step <= 0)
                {
                    float nearness = Mathf.InverseLerp(25, 3, Stage.EnemyDistance(e));
                    e.step = Mathf.Lerp(1.5f, .48f, nearness); e.waveDuration = e.step * .85f; e.waveAge = 0; e.pulse = 1;
                    if (e.rushStage == 2) { e.step *= .45f; e.waveDuration = e.step * .85f; }
                    e.waveIndex = (e.waveIndex + 1) % 3;
                    float proximity = Mathf.InverseLerp(1530, 325, e.position.x);
                    Play(stepClip, Mathf.Lerp(0.025f, 0.20f, proximity), Mathf.Clamp((e.position.y - 390) / 220, -1, 1));
                }
                if (e.ranged && Stage.EnemyDistance(e) > swordReach && Stage.EnemyDistance(e) < 15)
                {
                    if (e.throwWindup > 0)
                    {
                        e.throwWindup -= dt;
                        if (e.throwWindup <= 0)
                        {
                            var origin = Stage.EnemyWorld(e) + Vector3.up * 1.2f;
                            Projectiles.Add(new Projectile { position = origin, velocity = (new Vector3(0, 1.1f, 0) - origin).normalized * projectileSpeed });
                            e.throwTimer = 2.2f; Play(throwClip, .18f);
                            Say("표창! 네모 파동을 보고 가까워지면 쳐내세요");
                        }
                    }
                    else
                    {
                        e.throwTimer -= dt;
                        if (e.throwTimer <= 0) { e.throwWindup = .55f; Play(warningClip, .12f); }
                    }
                }
                else e.throwWindup = 0;
                if (e.position.x > 325)
                {
                    // A thrower plants its feet only during the short throwing windup.
                    if (e.throwWindup <= 0 && e.rushStage != 1) e.position.x = Mathf.Max(325, e.position.x - e.speed * (e.rushStage == 2 ? rushSpeedMultiplier : 1) * dt);
                }
                else
                {
                    if (e.attack <= 0) { e.windingUp = true; Play(warningClip, .2f); Say("가까운 적이 검을 들었다! 밝은 파동이 끝나기 전에 베세요"); }
                    e.attack += dt;
                    if (e.windingUp && e.attack >= meleeWindup)
                    {
                        e.windingUp = false; DamageEscort("백이 공격받았다! 가까운 발자국부터 베어내세요");
                        if (State != JourneyState.Walking) return;
                    }
                    if (e.attack >= .95f) e.attack = 0;
                }
            }
        }
        void AdvanceProjectiles(float dt)
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var shot = Projectiles[i]; shot.age += dt; shot.position += shot.velocity * dt;
                if (shot.position.z > .65f) continue;
                Projectiles.RemoveAt(i); DamageEscort("표창이 백에게 닿았다! 네모 파동을 가까이서 쳐내세요");
                if (State != JourneyState.Walking) return;
            }
        }
        void DamageEscort(string message)
        {
            Health = Mathf.Max(0, Health - 1); HitFlash = 1; Combo = 0;
            Play(hurtClip, .25f); Say(message);
            if (Health <= 0) Finish(false);
        }
        public Rect ProjectileHitRect(Projectile shot)
        {
            var bottom = CanvasPoint(Stage.View.WorldToScreenPoint(shot.position - new Vector3(.22f, .22f, 0)));
            var top = CanvasPoint(Stage.View.WorldToScreenPoint(shot.position + new Vector3(.22f, .22f, 0)));
            return Rect.MinMaxRect(bottom.x - 18, bottom.y - 18, top.x + 18, top.y + 18);
        }
        public bool TrySlashSegment(Vector2 from, Vector2 to)
        {
            if (State != JourneyState.Walking || !Revealed) return false;
            if (Vector2.Distance(from, to) < 2) return false;
            bool hit = false;
            bool deflected = false;
            bool perfect = false;

            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var shot = Projectiles[i];
                if (new Vector2(shot.position.x, shot.position.z).magnitude > swordReach || !SegmentHitsBody(from, to, ProjectileHitRect(shot))) continue;
                perfect |= shot.position.z <= perfectProjectileDistance;
                Projectiles.RemoveAt(i); hit = deflected = true; Combo++; comboTimer = 2.5f; HitFlash = .2f;
            }

            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (!Stage.InSwordRange(e) || Clock < e.nextSlash || EchoStrength(Stage.EnemyWorld(e)) <= .03f) continue;
                if (!SegmentHitsBody(from, to, EnemyHitRect(e))) continue;
                hit = true; e.nextSlash = Clock + .18f; e.hp -= swordDamage;
                comboTimer = 2.5f; HitFlash = .2f;
                if (e.hp <= 0) { perfect |= IsPerfectCounter(e); Enemies.RemoveAt(i); Kills++; Combo++; }
            }
            if (!hit)
            {
                if (slashTimer <= 0) { slashTimer = .12f; Play(cutClip, .08f); }
                return false;
            }
            Cuts.Add(new Cut { from = from, to = to, hit = true });
            Play(cutClip, .25f);
            if (perfect) RewardPerfect(); else Say(deflected ? "표창을 쳐냈다!" : Combo > 1 ? Combo + " 연참" : "검의 궤적이 닿았다");
            return true;
        }
        bool IsVisibleTarget(Vector3 position)
        {
            var p = Stage.View.WorldToViewportPoint(position);
            return p.z > Stage.View.nearClipPlane && p.x >= 0 && p.x <= 1 && p.y >= 174f / 900 && p.y <= 724f / 900;
        }
        void AddClickCut(Vector2 center, float span, int order)
        {
            var direction = new Vector2(1, order % 2 == 0 ? .4f : -.4f).normalized;
            Cuts.Add(new Cut { from = center - direction * span, to = center + direction * span, age = -Mathf.Min(.18f, order * .035f), hit = true, sweep = true });
        }
        public bool TryClickSlash()
        {
            if (swordMode != SwordMode.ClickAll || State != JourneyState.Walking || !Revealed || Clock < nextClickSlash) return false;
            nextClickSlash = Clock + .28f;
            int targets = 0;
            bool perfect = false;
            for (int i = Enemies.Count - 1; i >= 0; i--)
            {
                var e = Enemies[i];
                if (Clock < e.nextSlash || EchoStrength(Stage.EnemyWorld(e)) <= .03f || !IsVisibleTarget(Stage.EnemyWorld(e) + Vector3.up * .9f)) continue;
                var rect = EnemyHitRect(e);
                AddClickCut(rect.center, Mathf.Clamp(rect.height * .65f, 38, 150), targets++);
                e.nextSlash = Clock + .18f; e.hp -= swordDamage;
                if (e.hp <= 0) { perfect |= IsPerfectCounter(e); Enemies.RemoveAt(i); Kills++; Combo++; }
            }
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var shot = Projectiles[i];
                if (new Vector2(shot.position.x, shot.position.z).magnitude > swordReach || !IsVisibleTarget(shot.position)) continue;
                AddClickCut(ProjectileHitRect(shot).center, 55, targets++);
                perfect |= shot.position.z <= perfectProjectileDistance;
                Projectiles.RemoveAt(i); Combo++;
            }
            if (targets == 0)
                Cuts.Add(new Cut { from = new Vector2(560, 350), to = new Vector2(1040, 550), sweep = true });
            else { HitFlash = .2f; comboTimer = 2.5f; }
            Play(cutClip, targets > 0 ? .3f : .1f);
            if (perfect) RewardPerfect(); else Say(targets > 0 ? "일괄 베기 · " + targets + " 대상" : "시야 안에 벨 대상이 없다");
            return targets > 0;
        }
        bool IsPerfectCounter(Enemy e)
        {
            return (e.windingUp && e.attack >= meleeWindup - perfectWindow && e.attack < meleeWindup) || (e.rushStage == 2 && Stage.EnemyDistance(e) <= 3.6f);
        }
        void RewardPerfect()
        {
            Perfects++; Breath = Mathf.Min(whistleDuration, Breath + perfectBreathReward); PerfectFlash = 1;
            Say("아슬아슬!  숨 +" + perfectBreathReward.ToString("0.00") + "초");
        }
        public void Arrive()
        {
            if (Enemies.Count > 0 || Projectiles.Count > 0) { Say("남은 적과 날아오는 표창을 먼저 막으세요"); return; }
            SetWhistle(false); Enemies.Clear(); Progress = 1;
            if (Leg == 3) { Finish(true); return; }
            State = JourneyState.Camp;
            ShowPanel("숨을 고르는 곳", "구간 " + Leg + " / 3 통과  ·  " + Kills + " 처치\n\n백의 체력을 1 회복하고 한 가지를 정비합니다.\n긴 숨으로 더 오래 보고, 날 선 검으로 갑옷을 베세요.",
                "긴 숨  +0.6초", () => Upgrade(true), "검 연마  +1 공격력", () => Upgrade(false));
        }
        public Vector2 EnemyAim(Enemy enemy)
        {
            return CanvasPoint(Stage.EnemyScreen(enemy));
        }
        public Transform EffectParent => ink.transform.parent;
        public Vector2 CanvasPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(ink.rectTransform, screen, null, out var local);
            return local + new Vector2(800, 450);
        }
        public Vector2 ScreenPoint(Vector2 canvas) { return RectTransformUtility.WorldToScreenPoint(null, ink.rectTransform.TransformPoint(canvas - new Vector2(800, 450))); }
        public Rect EnemyHitRect(Enemy enemy)
        {
            var p = Stage.EnemyWorld(enemy);
            var bottom = CanvasPoint(Stage.View.WorldToScreenPoint(p + new Vector3(-.38f, .02f, 0)));
            var top = CanvasPoint(Stage.View.WorldToScreenPoint(p + new Vector3(.38f, 1.88f, 0)));
            return Rect.MinMaxRect(bottom.x - 12, bottom.y - 12, top.x + 12, top.y + 12);
        }
        static bool SegmentHitsBody(Vector2 from, Vector2 to, Rect rect)
        {
            float enter = 0, leave = 1; var d = to - from;
            return ClipAxis(from.x, d.x, rect.xMin, rect.xMax, ref enter, ref leave) && ClipAxis(from.y, d.y, rect.yMin, rect.yMax, ref enter, ref leave);
        }
        static bool ClipAxis(float p, float d, float min, float max, ref float enter, ref float leave)
        {
            if (Mathf.Abs(d) < .0001f) return p >= min && p <= max;
            float a = (min - p) / d, b = (max - p) / d;
            enter = Mathf.Max(enter, Mathf.Min(a, b)); leave = Mathf.Min(leave, Mathf.Max(a, b)); return enter <= leave;
        }
        public void Upgrade(bool breath)
        {
            if (State != JourneyState.Camp) return;
            if (breath) whistleDuration += 0.6f; else swordDamage++;
            Health = Mathf.Min(escortMaxHealth, Health + 1); Leg++; StartLeg();
        }
        void Finish(bool win)
        {
            State = win ? JourneyState.Victory : JourneyState.Defeat; SetWhistle(false);
            ShowPanel(win ? "산문에 닿다" : "휘파람이 끊겼다", win ?
                "흑의 검과 백의 숨이 길을 열었다.\n\n호위 성공  ·  " + Kills + " 처치  ·  남은 체력 " + Health + "\n\n다른 정비 조합으로 다시 도전해 보세요." :
                "백을 지켜내지 못했다.\n\n휘파람을 짧게 쓰고 놓으면 숨이 회복됩니다.\n가까운 발자국을 먼저 베고, 드래그로 연참하세요.", "다시 시작", BeginJourney);
        }
        void TogglePause()
        {
            if (State == JourneyState.Walking)
            {
                beforePause = State; State = JourneyState.Paused; SetWhistle(false);
                ShowPanel("잠시 멈춤", "SPACE / 우클릭 유지 : 휘파람\nTAB : 드래그 / 클릭 모드 전환\n좌클릭 : 선택한 모드로 검 사용\nR : 처음부터 다시 시작", "계속 호위", Resume);
            }
            else if (State == JourneyState.Paused) Resume();
        }
        void Resume() { State = beforePause; Time.timeScale = 1; panel.SetActive(false); }
        void Say(string text) { Message = text; messageTimer = 2.2f; }
        void UpdateHUD()
        {
            status.text = "호위 " + Leg + " / 3     " + new string('●', Mathf.Max(0, Health)) + new string('○', escortMaxHealth - Mathf.Max(0, Health)) + "     " + Kills + " 처치";
            instructions.text = Revealed ? (longEchoSent ? "넓은 반향 · " : "근거리 반향 · ") + (swordMode == SwordMode.Drag ? "좌클릭 드래그로 베기" : "좌클릭으로 감지된 적 일괄 베기") : "SPACE 짧게: 가까이 · 길게: 멀리     /     ○ 발걸음 · □ 표창";
            modeLabel.text = swordMode == SwordMode.Drag ? "검: 드래그   [TAB 전환]" : "검: 클릭 일괄 베기   [TAB 전환]";
            breathFill.fillAmount = Breath / whistleDuration; progressFill.fillAmount = Mathf.Clamp01(Progress);
            toast.text = messageTimer > 0 ? Message : Combo > 1 ? Combo + " 연참" : "";
        }
        void ShowPanel(string title, string body, string action, Action callback, string alternative = null, Action other = null)
        {
            panel.SetActive(true); popupTitle.text = title; popupBody.text = body; primaryLabel.text = action;
            primary.onClick.RemoveAllListeners(); primary.onClick.AddListener(() => callback());
            primary.GetComponent<RectTransform>().anchoredPosition = new Vector2(alternative == null ? 0 : -230, -200);
            secondary.gameObject.SetActive(alternative != null);
            secondaryLabel.text = alternative; secondary.onClick.RemoveAllListeners();
            if (other != null) secondary.onClick.AddListener(() => other());
        }
        void Play(AudioClip clip, float volume, float pan = 0) { effects.panStereo = pan; effects.PlayOneShot(clip, volume); }
        void OnDestroy() { Time.timeScale = 1; }
    }
}


