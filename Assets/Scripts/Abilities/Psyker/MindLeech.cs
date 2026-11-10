using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // The Force Stave's EX (Shift+RMB, in place of Kine Slam): a psychic tether to the enemy nearest the cursor. It interrupts and silences the
    // victim at once, then drains their health, healing the Psyker and feeding her energy, until it runs its course.
    // It breaks if the target dies or gets too far away, or if the Psyker is stunned, panicked, silenced, knocked back,
    // dies or presses C. Every machine draws the tether; only the owner's copy drains, heals and applies the silence.
    public class MindLeech : MonoBehaviour
    {
        const float TickInterval = 0.25f;

        GameObject owner;
        Health ownerHealth;
        Energy energy;
        Health target;
        bool dealsEffects;
        float endTime;
        float breakRange;
        float damagePerTick;
        float healPerTick;
        float energyPerTick;
        float nextTick;
        LineRenderer glow;
        LineRenderer core;
        Material glowMaterial;
        Material coreMaterial;

        // Starts a leech from `caster`. Returns false if there was nobody to leech (the caller can refund the cost).
        public static bool Begin(GameObject caster, Health casterHealth, Energy energy, Vector3 aimPoint, bool owner,
            float range, float duration, float silenceDuration, float damagePerSecond, float healPerSecond, float energyPerSecond)
        {
            Health best = null;
            float bestDistance = float.MaxValue;
            foreach (Health h in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (h.IsDead || h.Team == casterHealth.Team) continue;
                if (Vector3.Distance(h.transform.position, caster.transform.position) > range) continue;
                float d = Vector3.Distance(h.transform.position, aimPoint);
                if (d >= bestDistance) continue;
                best = h;
                bestDistance = d;
            }

            if (best == null) return false;

            var existing = caster.GetComponent<MindLeech>();
            if (existing != null) DestroyImmediate(existing);

            var leech = caster.AddComponent<MindLeech>();
            leech.owner = caster;
            leech.ownerHealth = casterHealth;
            leech.energy = energy;
            leech.target = best;
            leech.dealsEffects = owner;
            leech.endTime = Time.time + duration;
            leech.breakRange = range + 3f;
            leech.damagePerTick = damagePerSecond * TickInterval;
            leech.healPerTick = healPerSecond * TickInterval;
            leech.energyPerTick = energyPerSecond * TickInterval;
            leech.nextTick = Time.time + TickInterval;
            leech.BuildVisuals();

            if (owner) Silenced.Apply(best.gameObject, silenceDuration, true);
            return true;
        }

        public void Stop()
        {
            Destroy(this);
        }

        void BuildVisuals()
        {
            glowMaterial = VisualUtil.UnlitMaterial(new Color(0.55f, 0.2f, 0.9f, 0.4f), 3001);
            coreMaterial = VisualUtil.UnlitMaterial(new Color(0.85f, 1f, 0.7f, 0.95f), 3002);
            glow = MakeLine("Leech Glow", glowMaterial, 0.35f);
            core = MakeLine("Leech Core", coreMaterial, 0.1f);
        }

        LineRenderer MakeLine(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 12;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 3;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.sharedMaterial = material;
            return line;
        }

        bool CasterInterrupted()
        {
            return Stunned.IsStunned(owner) || Panicked.IsPanicked(owner) || Silenced.IsSilenced(owner)
                   || owner.GetComponent<Knockback>() != null;
        }

        void Update()
        {
            if (target == null || target.IsDead || ownerHealth.IsDead || Time.time >= endTime)
            {
                Stop();
                return;
            }

            Vector3 from = owner.transform.position + Vector3.up * 1.25f;
            Vector3 to = target.transform.position + Vector3.up * 1.1f;

            if (dealsEffects)
            {
                if (CasterInterrupted() || Vector3.Distance(owner.transform.position, target.transform.position) > breakRange)
                {
                    EndAndTellOthers();
                    return;
                }

                if (Time.time >= nextTick)
                {
                    nextTick += TickInterval;
                    if (target.TakeDamage(damagePerTick, owner, true))
                    {
                        HitMarkerSound.Play();
                        ownerHealth.Heal(healPerTick);
                        if (energy != null) energy.Gain(energyPerTick);
                    }
                }
            }
            else if (CasterInterrupted())
            {
                Stop();
                return;
            }

            // A writhing, pulsing tether.
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 18f);
            Vector3 side = Vector3.Cross(Vector3.up, (to - from).normalized);
            for (int k = 0; k < glow.positionCount; k++)
            {
                float f = k / (float)(glow.positionCount - 1);
                Vector3 p = Vector3.Lerp(from, to, f);
                float amp = 0.12f * Mathf.Sin(f * Mathf.PI);
                p += side * (Mathf.Sin(Time.time * 9f + f * 11f) * amp) + Vector3.up * (Mathf.Cos(Time.time * 7f + f * 9f) * amp);
                glow.SetPosition(k, p);
                core.SetPosition(k, p);
            }
            glow.startWidth = glow.endWidth = 0.35f * pulse;
        }

        // Owner only: the tether snapped, so make the other machine drop its drawing too (-1 cancels replayed abilities).
        void EndAndTellOthers()
        {
            if (ownerHealth.Net != null && ownerHealth.Net.IsSpawned)
                ownerHealth.Net.SendCue(-1, target != null ? target.transform.position : owner.transform.position);
            Stop();
        }

        void OnDestroy()
        {
            if (glow != null) Destroy(glow.gameObject);
            if (core != null) Destroy(core.gameObject);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (coreMaterial != null) Destroy(coreMaterial);
        }
    }
}
