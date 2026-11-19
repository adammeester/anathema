using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Psyker
{
    // Q: Discharge. The Psyker snaps her fingers and every Charged enemy within range is struck by an arc of lightning
    // from her hand: each takes damage and loses its Charge. It pays off Chain Lightning (and Kine Slam, which Charges
    // too): the more enemies she has Charged, the more it hits. With nothing Charged in range the key does nothing, so
    // it is never wasted.
    //
    // Online: every machine replays it from the cue against its own copies of the Charged marks (they are applied on
    // every machine as Chain Lightning is replayed), so the arcs and the spent runes match; only the owner's copy deals
    // the damage.
    public class Discharge : Ability
    {
        [SerializeField] float range = 12f;
        [SerializeField] float damage = 16f;
        [SerializeField] float energyPerHit = 8f;
        [SerializeField] float windup = 0.5f;

        static readonly List<Health> targets = new List<Health>();

        Energy energy;

        public override string DisplayName => "Discharge";
        public override string KeyLabel => "Q";

        // A gather of crackling power before the snap, telegraphed on every screen so the people about to be struck see
        // it coming: a flickering warning line from her hand to each Charged target and a ring closing on their feet.
        // Breakable by a stun, silence, panic or knockback (refunded, like any wind-up). Who it actually strikes is
        // decided as it lands, so stepping out of range or losing the Charge in time dodges it.
        protected override float WindupDuration => windup;
        protected override Color WindupColor => PsykerFx.Cyan;

        static readonly Color WarningColor = new Color(0.55f, 0.95f, 1f, 0.8f);

        readonly List<Health> warned = new List<Health>();
        readonly List<LineRenderer> warningLines = new List<LineRenderer>();
        Material warningMaterial;
        float windupStart;

        protected override void OnWindupStart()
        {
            windupStart = Time.time;
            Sfx.Play(Sfx.ElectricBeam, 0.25f, 1.4f, 1.6f, windup);
            ClearWarnings();
            if (warningMaterial == null) warningMaterial = VisualUtil.UnlitMaterial(WarningColor, 3004);
            foreach (Health target in FindTargets())
            {
                warned.Add(target);
                PsykerFx.Ring(target.transform.position + Vector3.up * 0.1f, 1.2f, 0.3f, windup, PsykerFx.Cyan, 0.08f);

                var line = new GameObject("Discharge Warning").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.useWorldSpace = true;
                line.positionCount = 8;
                line.sharedMaterial = warningMaterial;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                warningLines.Add(line);
            }
            DrawWarnings();
        }

        protected override void OnWindupCancel() => ClearWarnings();

        protected override void Update()
        {
            base.Update();
            if (IsWindingUp) DrawWarnings();
            else if (warningLines.Count > 0) ClearWarnings();
        }

        // Jagged, jittering lines that thicken as the snap nears.
        void DrawWarnings()
        {
            Vector3 hand = transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
            float progress = windup > 0f ? Mathf.Clamp01((Time.time - windupStart) / windup) : 1f;
            float width = Mathf.Lerp(0.03f, 0.09f, progress);
            for (int i = 0; i < warningLines.Count; i++)
            {
                LineRenderer line = warningLines[i];
                Health target = warned[i];
                if (line == null) continue;
                bool live = target != null && !target.IsDead && Charged.IsCharged(target.gameObject);
                if (line.enabled != live) line.enabled = live;
                if (!live) continue;

                Vector3 chest = target.transform.position + Vector3.up * 1.1f;
                int n = line.positionCount;
                for (int k = 0; k < n; k++)
                {
                    float f = k / (float)(n - 1);
                    Vector3 p = Vector3.Lerp(hand, chest, f);
                    if (k > 0 && k < n - 1) p += Random.insideUnitSphere * 0.15f;
                    line.SetPosition(k, p);
                }
                line.startWidth = line.endWidth = width;
            }
        }

        void ClearWarnings()
        {
            foreach (LineRenderer line in warningLines) if (line != null) Destroy(line.gameObject);
            warningLines.Clear();
            warned.Clear();
        }

        void OnDestroy()
        {
            ClearWarnings();
            if (warningMaterial != null) Destroy(warningMaterial);
        }

        // Greyed out on the HUD while nothing Charged is in reach.
        public override bool Available => FindTargets().Count > 0;

        void Reset()
        {
            cooldown = 7f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q) && FindTargets().Count > 0;

        // Living, visible enemies carrying a Charge within range. The list is reused.
        List<Health> FindTargets()
        {
            targets.Clear();
            if (Health == null) return targets;
            Vector3 here = transform.position;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (h.IsDead || h.Team == Health.Team || !Charged.IsCharged(h.gameObject)) continue;
                if (Stealthed.HiddenFrom(h.gameObject, Health.Team)) continue;
                Vector3 to = h.transform.position - here;
                to.y = 0f;
                if (to.magnitude <= range) targets.Add(h);
            }
            return targets;
        }

        protected override void Activate()
        {
            ClearWarnings();
            Vector3 hand = transform.position + Vector3.up * 1.4f + transform.forward * 0.4f;
            var struck = new List<Health>(FindTargets());
            if (struck.Count == 0)
            {
                // Everything Charged left range (or lost its Charge) during the wind-up: nothing is spent.
                if (!IsReplica) RefundCooldown();
                return;
            }

            Sfx.Play(Sfx.Any(Sfx.MagicAttack1, Sfx.MagicAttack2), 0.4f, 1.25f, 1.4f);
            PsykerFx.Ring(transform.position + Vector3.up * 0.1f, 0.2f, 1.2f, 0.25f, PsykerFx.Cyan, 0.12f);

            foreach (Health target in struck)
            {
                Vector3 chest = target.transform.position + Vector3.up * 1.1f;
                PsykerFx.Bolt(new List<Vector3> { hand, chest }, 0.3f);
                Charged.Consume(target.gameObject);
                if (IsReplica) continue;

                target.Strike(Hit.Direct(damage), gameObject, landed =>
                {
                    if (!landed) return;
                    HitMarkerSound.Play();
                    if (energy != null) energy.Gain(energyPerHit);
                });
            }
        }

        protected override void Cancel()
        {
        }
    }
}
