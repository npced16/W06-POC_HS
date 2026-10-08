using System.Collections.Generic;
using UnityEngine;

namespace WhistlePOC
{
    public enum DuelState { Title, Fighting, Paused, Victory, Defeat }
    public enum BossAction { Hunt, Windup, Strike, Recover, Stunned }
    public enum BossPattern { Sweep, Overhead, Rush }

    // All geometry, audio sources and reusable traces are authored in the scene.
    public sealed class BossDuel : MonoBehaviour
    {
        public CharacterController player;
        public DuelInputSystem input;
        public DuelMap map;
        public SoundEcho soundEcho;
        public DuelPresentation presentation;
        public Camera view;
        public Transform boss, bossWeapon, sword;
        public Renderer[] bossBody, building;
        public LineRenderer[] bossContours, footprints, buildingContours, swordContours, slashContours;
        public BoxCollider[] walls;
        public AudioSource bossAudio, playerAudio;
        public AudioLowPassFilter occlusion;
        public AudioClip footstep, scanSound, slashSound, impactSound, counterSound;
        public AudioClip[] cues;
        [Header("Duel tuning")]
        public int bossMaxHealth = 24, playerMaxHealth = 3;
        public float moveSpeed = 4.2f, mouseSensitivity = .12f, scanDuration = 1.15f;
        public float swordReach = 2.7f, perfectWindow = .17f;
        public DuelState State { get; private set; }
        public BossAction Action { get; private set; }
        public BossPattern Pattern { get; private set; }
        public int BossHealth { get; private set; }
        public int Health { get; private set; }
        public int Counters { get; private set; }
        public float Breath { get; private set; }
        public float Stamina { get; private set; }
        public bool Revealed => scanRemaining > 0;
        public bool Enraged => BossHealth <= bossMaxHealth / 2;
        public string LastFeedback { get; private set; }
        public event System.Action<string> Feedback;
        float yaw, pitch, timer, scanRemaining, attackLock, parryRemaining, dodgeRemaining;
        float gravity, bossStep, playerStep, routeTimer, elapsed, swing;
        Vector3 playerHome, bossHome, target, dodgeDirection;
        int patternIndex, traceIndex;
        bool strikeResolved, phaseEntered;
        bool playerLeftFoot, bossLeftFoot;
        Vector2 moveInput, lookInput;
        readonly List<Vector3> route = new List<Vector3>();
        float[] traceAges;
        readonly Queue<int> frontier = new Queue<int>();
        readonly int[] parents = new int[31 * 31];
        readonly bool[] walkable = new bool[31 * 31];
        static readonly int[] Neighbors = { -1, 1, -31, 31 };

        void OnEnable()
        {
            input.Move += OnMove; input.Look += OnLook; input.Attack += OnAttack; input.Parry += OnParry;
            input.Scan += OnScan; input.Dodge += OnDodge; input.Pause += OnPause; input.Restart += Begin; input.Confirm += OnConfirm;
        }
        void UnsubscribeInput()
        {
            if (input == null) return;
            input.Move -= OnMove; input.Look -= OnLook; input.Attack -= OnAttack; input.Parry -= OnParry;
            input.Scan -= OnScan; input.Dodge -= OnDodge; input.Pause -= OnPause; input.Restart -= Begin; input.Confirm -= OnConfirm;
            moveInput = lookInput = Vector2.zero;
        }
        void OnMove(Vector2 value) => moveInput = value;
        void OnLook(Vector2 value) { if (State == DuelState.Fighting && Cursor.lockState == CursorLockMode.Locked) lookInput = value; }
        void OnAttack() => Attack();
        void OnParry() => Parry();
        void OnScan() => Scan();
        void OnConfirm() { if (State != DuelState.Fighting) ResumeOrBegin(); }
        void OnPause()
        {
            if (State == DuelState.Fighting) { State = DuelState.Paused; lookInput = Vector2.zero; LockCursor(false); Toast("일시정지 — ESC 또는 ENTER로 계속"); }
            else if (State == DuelState.Paused) ResumeOrBegin();
        }
        void OnDodge()
        {
            if (State != DuelState.Fighting || Stamina < 30 || dodgeRemaining > 0 || attackLock > 0) return;
            Vector3 direction = player.transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y).normalized);
            Stamina -= 30; dodgeRemaining = .24f; dodgeDirection = direction.sqrMagnitude > .1f ? direction : -player.transform.forward;
        }

        void Awake()
        {
            if (map != null) map.Bind(this);
            playerHome = player.transform.position; bossHome = boss.position;
            traceAges = new float[footprints.Length];
            Physics.SyncTransforms();
            for (int i = 0; i < walkable.Length; i++) walkable[i] = !Blocked(CellWorld(i), .55f);
            Time.timeScale = 1; Begin();
            Render(0);
        }

        public void Begin()
        {
            if (map != null) map.Bind(this);
            if (soundEcho != null) { soundEcho.Initialize(buildingContours, walls); soundEcho.Paused = false; soundEcho.ResetWaves(); }
            Physics.SyncTransforms();
            for (int i = 0; i < walkable.Length; i++) walkable[i] = !Blocked(CellWorld(i), .55f);
            player.enabled = false; player.transform.position = playerHome; player.transform.rotation = Quaternion.identity; player.enabled = true;
            view.transform.localRotation = Quaternion.identity; yaw = pitch = gravity = 0;
            boss.position = bossHome; boss.rotation = Quaternion.identity;
            if (presentation != null) presentation.ResetVisuals();
            Health = playerMaxHealth; BossHealth = bossMaxHealth; Breath = Stamina = 100; Counters = 0;
            timer = 1.5f; Action = BossAction.Hunt; State = DuelState.Fighting;
            scanRemaining = attackLock = parryRemaining = dodgeRemaining = elapsed = swing = 0;
            patternIndex = traceIndex = 0; phaseEntered = false; route.Clear(); routeTimer = bossStep = playerStep = 0;
            playerLeftFoot = bossLeftFoot = false;
            bossAudio.Stop(); playerAudio.Stop();
            for (int i = 0; i < traceAges.Length; i++) traceAges[i] = 10;
            LockCursor(true); Toast("발소리를 따라가세요. SPACE로 잔향 감지.");
        }

        void Update()
        {
            if (presentation != null) presentation.Tick(State == DuelState.Paused ? 0 : Time.deltaTime);
            if (State != DuelState.Fighting) { lookInput = Vector2.zero; Render(0); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f); elapsed += dt;
            scanRemaining = Mathf.Max(0, scanRemaining - dt); attackLock = Mathf.Max(0, attackLock - dt);
            parryRemaining = Mathf.Max(0, parryRemaining - dt); dodgeRemaining = Mathf.Max(0, dodgeRemaining - dt);
            swing = Mathf.Max(0, swing - dt);
            Breath = Mathf.Min(100, Breath + dt * (Revealed ? 0 : 9)); Stamina = Mathf.Min(100, Stamina + dt * 19);
            for (int i = 0; i < traceAges.Length; i++) traceAges[i] += dt;
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                yaw += lookInput.x * mouseSensitivity; pitch = Mathf.Clamp(pitch - lookInput.y * mouseSensitivity, -75, 75);
                player.transform.rotation = Quaternion.Euler(0, yaw, 0); view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            lookInput = Vector2.zero;
            Vector3 movement = player.transform.TransformDirection(new Vector3(moveInput.x, 0, moveInput.y).normalized);
            gravity = player.isGrounded ? -2 : gravity - dt * 20;
            Vector3 motion = dodgeRemaining > 0 ? dodgeDirection * 13 : movement * moveSpeed * (attackLock > 0 ? .55f : 1);
            Vector3 positionBeforeMove = player.transform.position;
            player.Move((motion + Vector3.up * gravity) * dt);
            Vector3 distanceMoved = player.transform.position - positionBeforeMove; distanceMoved.y = 0;
            if (distanceMoved.sqrMagnitude > .000001f && player.isGrounded && dodgeRemaining <= 0)
            { playerStep -= dt; if (playerStep <= 0) { playerStep = .48f; EmitFootstep(player.transform, true); } }
            TickBoss(dt);
            Render(dt);
        }

        public void ResumeOrBegin()
        {
            if (State == DuelState.Paused) { State = DuelState.Fighting; LockCursor(true); }
            else Begin();
        }
        public bool Scan()
        {
            if (State != DuelState.Fighting || Revealed || Breath < 35) return false;
            Breath -= 35; scanRemaining = scanDuration; SoundEvents.Play(playerAudio, scanSound, player.transform.position, .55f);
            // A loud scan draws the boss into engagement even from the neighboring room.
            if (Action == BossAction.Hunt) timer = Mathf.Min(timer, .2f);
            return true;
        }
        bool FacingBoss(float angle)
        {
            Vector3 delta = boss.position - player.transform.position; delta.y = 0;
            return delta.magnitude <= swordReach && Vector3.Angle(player.transform.forward, delta) < angle && Clear(player.transform.position, boss.position, .05f);
        }
        public bool Attack()
        {
            if (State != DuelState.Fighting || attackLock > 0 || Stamina < 24 || dodgeRemaining > 0) return false;
            Stamina -= 24; attackLock = .72f; swing = .28f; SoundEvents.Play(playerAudio, slashSound, player.transform.position, .6f);
            if (!FacingBoss(48)) { Toast("헛베기 — 빈틈 노출"); return false; }
            int damage = Action == BossAction.Stunned ? 5 : Action == BossAction.Recover ? 3 : 1;
            BossHealth = Mathf.Max(0, BossHealth - damage); SoundEvents.Play(bossAudio, impactSound, boss.position, .6f);
            if (presentation != null) presentation.Hit();
            Toast(damage >= 3 ? "빈틈 베기  +" + damage : "갑옷 타격  +1");
            if (BossHealth == 0) Finish(true);
            return true;
        }
        public bool Parry()
        {
            if (State != DuelState.Fighting || attackLock > 0 || Stamina < 18 || dodgeRemaining > 0) return false;
            Stamina -= 18; parryRemaining = perfectWindow; attackLock = .8f;
            Toast("받아치기 — 마지막 순간에 맞추세요"); return true;
        }

        public void TickBoss(float dt)
        {
            if (State != DuelState.Fighting) return;
            timer -= dt;
            Vector3 delta = player.transform.position - boss.position; delta.y = 0;
            if (delta.sqrMagnitude > .01f && (Action == BossAction.Hunt || (Action == BossAction.Windup && timer > .35f)))
                boss.rotation = Quaternion.RotateTowards(boss.rotation, Quaternion.LookRotation(delta), dt * 220);
            if (!phaseEntered && Enraged)
            { phaseEntered = true; Toast("2단계 — 사냥꾼의 공격이 빨라집니다"); SoundEvents.Play(bossAudio, cues[2], boss.position, .85f); }
            if (Action == BossAction.Hunt)
            {
                if (delta.magnitude > 2) FollowPlayer(dt);
                float range = patternIndex % 3 == 2 ? 11 : 3.2f;
                if (timer <= 0 && delta.magnitude < range && Clear(boss.position, player.transform.position, .45f)) BeginWindup();
            }
            else if (Action == BossAction.Windup && timer <= 0)
            { Action = BossAction.Strike; timer = Pattern == BossPattern.Rush ? .6f : .16f; strikeResolved = false; }
            else if (Action == BossAction.Strike)
            {
                if (Pattern == BossPattern.Rush)
                {
                    Vector3 advance = boss.forward * dt * (Enraged ? 15 : 12);
                    if (!Blocked(boss.position + advance, .5f)) { boss.position += advance; Step(dt, .1f); }
                    else timer = 0;
                }
                if (!strikeResolved)
                {
                    float range = Pattern == BossPattern.Rush ? 1.8f : Pattern == BossPattern.Overhead ? 3.4f : 2.8f;
                    float angle = Pattern == BossPattern.Sweep ? 95 : Pattern == BossPattern.Overhead ? 24 : 40;
                    delta = player.transform.position - boss.position; delta.y = 0;
                    if (delta.magnitude < range && Vector3.Angle(boss.forward, delta) < angle && Clear(boss.position, player.transform.position, .1f)) ResolveHit();
                    else if (Pattern != BossPattern.Rush) strikeResolved = true;
                }
                if (Action == BossAction.Strike && timer <= 0)
                { Action = BossAction.Recover; timer = Pattern == BossPattern.Overhead ? 1.35f : .95f; SoundEvents.Play(bossAudio, impactSound, boss.position, .25f); }
            }
            else if ((Action == BossAction.Recover || Action == BossAction.Stunned) && timer <= 0)
            { Action = BossAction.Hunt; timer = Enraged ? .3f : .65f; routeTimer = 0; }
        }
        void BeginWindup()
        {
            Pattern = (BossPattern)(patternIndex++ % 3); Action = BossAction.Windup;
            timer = Pattern == BossPattern.Sweep ? .72f : Pattern == BossPattern.Overhead ? 1.1f : 1.25f;
            if (Enraged) timer *= .78f;
            target = player.transform.position; target.y = boss.position.y;
            boss.rotation = Quaternion.LookRotation(target - boss.position);
            SoundEvents.Play(bossAudio, cues[(int)Pattern], boss.position, .85f);
        }
        void ResolveHit()
        {
            strikeResolved = true;
            if (dodgeRemaining > 0) { Toast("회피 성공 — 공격 후 빈틈을 노리세요"); return; }
            if (parryRemaining > 0 && FacingBoss(65))
            {
                Counters++; Action = BossAction.Stunned; timer = 1.6f; parryRemaining = attackLock = 0;
                Breath = Mathf.Min(100, Breath + 30); Stamina = Mathf.Min(100, Stamina + 25);
                scanRemaining = Mathf.Max(scanRemaining, .6f); SoundEvents.Play(playerAudio, counterSound, player.transform.position, .85f);
                Toast("완벽한 받아치기 — 숨 +30 · 보스 무방비"); return;
            }
            Health--; SoundEvents.Play(playerAudio, impactSound, player.transform.position, .9f);
            Toast("피격 — " + Health + "번의 기회가 남았습니다");
            if (Health <= 0) Finish(false);
        }
        void FollowPlayer(float dt)
        {
            routeTimer -= dt;
            if (routeTimer <= 0) { routeTimer = .5f; FindRoute(boss.position, player.transform.position); }
            Vector3 destination = player.transform.position; destination.y = boss.position.y;
            if (!Clear(boss.position, destination, .55f))
            {
                while (route.Count > 0 && Vector3.Distance(boss.position, route[0]) < .3f) route.RemoveAt(0);
                if (route.Count == 0) return;
                destination = route[0];
            }
            Vector3 next = Vector3.MoveTowards(boss.position, destination, dt * (Enraged ? 3.9f : 3.15f));
            if (!Blocked(next, .5f)) { boss.position = next; Step(dt, Enraged ? .32f : .44f); }
        }
        void Step(float dt, float interval)
        {
            bossStep -= dt; if (bossStep > 0) return; bossStep = interval;
            EmitFootstep(boss, false);
        }
        void EmitFootstep(Transform source, bool isPlayer)
        {
            if (isPlayer) playerLeftFoot = !playerLeftFoot;
            else bossLeftFoot = !bossLeftFoot;
            bool left = isPlayer ? playerLeftFoot : bossLeftFoot;
            Vector3 position = source.position + source.right * (left ? -.18f : .18f);
            var trace = footprints[traceIndex]; trace.transform.position = position + Vector3.up * .025f;
            trace.transform.rotation = source.rotation; traceAges[traceIndex] = 0; traceIndex = (traceIndex + 1) % footprints.Length;
            SoundEvents.Play(isPlayer ? playerAudio : bossAudio, footstep, position, isPlayer ? .23f : .7f, 2.5f);
        }
        public bool Blocked(Vector3 p, float radius)
        {
            foreach (var wall in walls)
            {
                Bounds b = wall.bounds;
                if (p.x > b.min.x - radius && p.x < b.max.x + radius && p.z > b.min.z - radius && p.z < b.max.z + radius) return true;
            }
            return false;
        }
        public bool Clear(Vector3 from, Vector3 to, float radius)
        {
            from.y = to.y = 1; Vector3 d = to - from; float length = d.magnitude;
            if (length < .001f) return true;
            foreach (var wall in walls)
            { Bounds b = wall.bounds; b.Expand(new Vector3(radius * 2, 0, radius * 2)); if (b.IntersectRay(new Ray(from, d / length), out float hit) && hit < length) return false; }
            return true;
        }
        static Vector3 CellWorld(int i) => new Vector3(i % 31 * .8f - 12, 0, i / 31 * .8f - 12);
        static int CellIndex(Vector3 p) => Mathf.Clamp(Mathf.RoundToInt((p.x + 12) / .8f), 0, 30) + Mathf.Clamp(Mathf.RoundToInt((p.z + 12) / .8f), 0, 30) * 31;
        public void FindRoute(Vector3 from, Vector3 to)
        {
            route.Clear(); frontier.Clear(); int start = CellIndex(from), end = CellIndex(to);
            // Nearest available cell avoids rounding a valid position into a wall.
            start = NearestCell(start); end = NearestCell(end);
            for (int i = 0; i < parents.Length; i++) parents[i] = -1;
            parents[start] = start; frontier.Enqueue(start);
            while (frontier.Count > 0 && parents[end] < 0)
            {
                int cell = frontier.Dequeue();
                foreach (int offset in Neighbors)
                {
                    int next = cell + offset;
                    if (next < 0 || next >= parents.Length || (Mathf.Abs(offset) == 1 && next / 31 != cell / 31) || parents[next] >= 0 || !walkable[next]) continue;
                    if (!Clear(CellWorld(cell), CellWorld(next), .5f)) continue;
                    parents[next] = cell; frontier.Enqueue(next);
                }
            }
            if (parents[end] < 0) return;
            for (int at = end; at != start; at = parents[at]) route.Add(CellWorld(at));
            route.Reverse();
        }
        int NearestCell(int cell)
        {
            if (walkable[cell]) return cell;
            int nearest = cell; float best = float.MaxValue;
            for (int i = 0; i < walkable.Length; i++) if (walkable[i])
            { float distance = (CellWorld(i) - CellWorld(cell)).sqrMagnitude; if (distance < best) { best = distance; nearest = i; } }
            return nearest;
        }
        public int RouteCount => route.Count;
        void Render(float dt)
        {
            float echo = Revealed ? Mathf.Clamp01(scanRemaining / .22f) : 0;
            // Black surfaces keep correct depth occlusion; only white contours reveal shapes.
            float bossVisibility = echo;
            if (presentation != null) bossVisibility = Mathf.Max(bossVisibility, presentation.HitVisibility * .85f);
            if (Vector3.Distance(player.transform.position, boss.position) < 4 && Clear(player.transform.position, boss.position, .05f)) bossVisibility = Mathf.Max(bossVisibility, .3f);
            if (State == DuelState.Victory) bossVisibility = .7f;
            foreach (var r in bossBody) r.enabled = true;
            foreach (var line in bossContours) { line.enabled = bossVisibility > .01f; line.startColor = line.endColor = new Color(1, 1, 1, bossVisibility); }
            if (soundEcho != null) { soundEcho.Paused = State == DuelState.Paused; soundEcho.SetScanStrength(echo); }
            else foreach (var line in buildingContours) line.startColor = line.endColor = new Color(1, 1, 1, Mathf.Lerp(.09f, .88f, echo));
            foreach (var line in swordContours) line.startColor = line.endColor = Color.white;
            foreach (var line in slashContours)
            {
                line.enabled = swing > 0;
                line.startColor = line.endColor = new Color(1, 1, 1, Mathf.Clamp01(swing / .18f));
            }
            for (int i = 0; i < footprints.Length; i++)
            {
                float alpha = Mathf.Clamp01(1 - traceAges[i] / 2.1f);
                // Traces record past movement and do not update to the boss's current position.
                footprints[i].enabled = (State == DuelState.Fighting || State == DuelState.Paused) && alpha > 0;
                footprints[i].startColor = footprints[i].endColor = new Color(1, 1, 1, alpha * .75f);
            }
            bool obstructed = !Clear(view.transform.position, boss.position, 0);
            occlusion.cutoffFrequency = Mathf.Lerp(occlusion.cutoffFrequency, obstructed ? 900 : 22000, dt * 10);
            bossAudio.volume = obstructed ? .5f : 1;
            if (bossWeapon != null) bossWeapon.localRotation = Quaternion.Euler(Action == BossAction.Windup ? -75 : 0, 0, Action == BossAction.Strike ? -70 : Action == BossAction.Stunned ? 45 : 0);
            if (State == DuelState.Defeat) return;
            float slash = Mathf.Sin((1 - swing / .28f) * Mathf.PI) * (swing > 0 ? 1 : 0);
            sword.localRotation = Quaternion.Euler(0, 0, parryRemaining > 0 ? 65 : -22 - slash * 110);
            sword.localPosition = new Vector3(.36f - slash * .5f, -.4f + slash * .25f, .65f);
        }
        void Finish(bool victory)
        {
            State = victory ? DuelState.Victory : DuelState.Defeat; bossAudio.Stop();
            if (presentation != null) presentation.Die(victory);
            LockCursor(false);
            SoundEvents.Play(playerAudio, victory ? counterSound : impactSound, player.transform.position, .8f);
            Toast((victory ? "결투 승리" : "잔향이 끊겼다") + " — 받아치기 " + Counters + "회 · " + elapsed.ToString("F1") + "초. ENTER 또는 R로 재도전");
        }
        static void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
        void Toast(string text) { LastFeedback = text; Feedback?.Invoke(text); Debug.Log("[Boss Duel] " + text); }
        void OnDisable() { UnsubscribeInput(); LockCursor(false); Time.timeScale = 1; }
        void OnApplicationFocus(bool focused)
        { if (!focused && State == DuelState.Fighting) OnPause(); }
    }
}
