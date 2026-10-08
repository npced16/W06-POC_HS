using UnityEngine;

namespace WhistlePOC
{
    // World-space armor marks and physical death poses; no Canvas or HUD.
    public sealed class DuelPresentation : MonoBehaviour
    {
        public BossDuel game;
        public LineRenderer[] healthMarks, deathBurst;
        float hitTime, deathTime;
        bool initialized, dying, bossDying;
        Vector3 cameraHome, bossDeathPosition;
        Quaternion bossDeathRotation;
        public float HitVisibility => Mathf.Clamp01(hitTime / .35f);

        public void ResetVisuals()
        {
            if (!initialized) { cameraHome = game.view.transform.localPosition; initialized = true; }
            game.view.transform.localPosition = cameraHome;
            dying = false; hitTime = deathTime = 0;
            game.bossAudio.pitch = game.playerAudio.pitch = 1;
            foreach (var line in deathBurst) line.enabled = false;
        }
        public void Hit() { hitTime = 1.6f; }
        public void Die(bool bossDeath)
        {
            dying = true; bossDying = bossDeath; deathTime = 0;
            bossDeathPosition = game.boss.position; bossDeathRotation = game.boss.rotation;
            if (bossDeath)
            {
                game.bossAudio.pitch = .6f;
                SoundEvents.Play(game.bossAudio, game.impactSound, game.boss.position, .8f, 3);
            }
        }
        public void Tick(float dt)
        {
            hitTime = Mathf.Max(0, hitTime - dt);
            bool visible = game.BossHealth > 0 && game.State != DuelState.Defeat && game.Clear(game.player.transform.position, game.boss.position, .05f)
                && (game.Revealed || hitTime > 0 || Vector3.Distance(game.player.transform.position, game.boss.position) < 4.5f);
            for (int i = 0; i < healthMarks.Length; i++)
            {
                float remaining = Mathf.Clamp01((float)game.BossHealth / game.bossMaxHealth * healthMarks.Length - i);
                healthMarks[i].enabled = visible;
                healthMarks[i].startColor = healthMarks[i].endColor = new Color(1, 1, 1, remaining > 0 ? Mathf.Lerp(.4f, 1, remaining) : .07f);
            }
            if (!dying) return;
            deathTime += dt; float fall = Mathf.SmoothStep(0, 1, deathTime / .8f);
            if (bossDying)
            {
                game.boss.rotation = bossDeathRotation * Quaternion.Euler(0, 0, -90 * fall);
                game.boss.position = bossDeathPosition + Vector3.up * (.35f * fall);
                for (int i = 0; i < deathBurst.Length; i++)
                {
                    float angle = i * Mathf.PI * 2 / deathBurst.Length;
                    Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                    Vector3 center = bossDeathPosition + Vector3.up;
                    deathBurst[i].enabled = deathTime < .55f;
                    deathBurst[i].SetPosition(0, center + direction * (.15f + deathTime));
                    deathBurst[i].SetPosition(1, center + direction * (.4f + deathTime * 2.5f));
                    deathBurst[i].startColor = deathBurst[i].endColor = new Color(1, 1, 1, Mathf.Clamp01(1 - deathTime / .55f));
                }
            }
            else
            {
                game.view.transform.localPosition = Vector3.Lerp(cameraHome, new Vector3(0, .25f, 0), fall);
                game.view.transform.localRotation = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(48, 0, 22), fall);
                game.sword.localPosition = new Vector3(.6f, -.8f - fall * .5f, .8f);
                game.sword.localRotation = Quaternion.Euler(0, 0, 90 * fall);
            }
        }
    }
}
