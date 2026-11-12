using System.Collections.Generic;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // R, ultimate (costs a full energy bar): Shadow Cross. A ghostly X is marked on the ground at the cursor during a short
    // wind-up. Then the Mandrake blinks to one end of the first diagonal and tears along it in a single huge slash, blinks
    // to the start of the other diagonal and tears across that one, carving an X. Everyone along either line is cut and
    // is poisoned; anyone standing where the lines cross is hit twice, and if they already carried poison it detonates.
    // He is invulnerable from the first dash to the end of the second. The diagonals are set at +-45 degrees to the line
    // from him to the cursor. Stuns, silence, panic and knockback during the wind-up interrupt it (refunded); C during the
    // wind-up cancels and refunds.
    public class ShadowCross : Ability
    {
        [SerializeField] float energyCost = 100f;
        [SerializeField] float castRange = 9f;          // how far away the centre of the X may be
        [SerializeField] float slashLength = 9f;        // length of each diagonal
        [SerializeField] float slashWidth = 1.4f;       // everything within this distance of a line is hit
        [SerializeField] float damage = 25f;            // per slash
        [SerializeField] int poisonStacks = 2;          // per slash
        [SerializeField] float poisonDamagePerSecond = 2f;
        [SerializeField] float poisonDuration = 4f;
        [SerializeField] float windup = 0.35f;
        [SerializeField] float dashDuration = 0.4f;
        [SerializeField] float blinkPause = 0.25f;      // between the two slashes

        enum Phase { Idle, Dash1, Blink, Dash2 }

        Energy energy;
        Phase phase;
        float phaseEnd;
        Vector3 centre;
        Vector3 a1, b1, a2, b2;
        bool firstStrike;               // the first slash still carries the strike bonuses (stealth, Shadow Jump)
        float strikeBonus;
        Telegraph telegraph;

        public override string DisplayName => "Shadow Cross";
        public override string KeyLabel => "R";
        public override float MovementRange => castRange + slashLength * 2.5f;
        public override bool RootsMovement => phase != Phase.Idle;
        public override bool Available => energy != null && energy.Current >= energyCost && !Silenced.IsSilenced(gameObject) && !Panicked.IsPanicked(gameObject);

        protected override float WindupDuration => windup;
        protected override Color WindupColor => MandrakeKit.Baleflame;

        void Reset()
        {
            cooldown = 1f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.R) && energy != null && energy.Current >= energyCost;

        protected override void OnWindupStart()
        {
            // The X is fixed where the cursor is now; a replica uses the aim point from the owner's cue.
            PlanCross();
            if (!IsReplica && energy != null) energy.TrySpend(energyCost);
            if (telegraph != null) Destroy(telegraph.gameObject);
            if (!Stealthed.HiddenHere(gameObject)) telegraph = Telegraph.Spawn(a1, b1, a2, b2, slashWidth);
        }

        protected override void OnWindupCancel()
        {
            if (!IsReplica && energy != null) energy.Gain(energyCost);
            ClearTelegraph();
        }

        void PlanCross()
        {
            Vector3 from = transform.position;
            Vector3 to = Aim.AimPoint - from;
            to.y = 0f;
            Vector3 dir = to.sqrMagnitude < 0.01f ? transform.forward : to.normalized;
            float reach = LandingSpot.Clear(transform, from, dir, Mathf.Min(to.magnitude, castRange));
            centre = from + dir * reach;

            Vector3 d1 = Quaternion.Euler(0f, 45f, 0f) * dir;
            Vector3 d2 = Quaternion.Euler(0f, -45f, 0f) * dir;
            float half = slashLength * 0.5f;
            // Each slash runs from its near end (the side he's on) across the centre; neither end lands inside a wall.
            a1 = centre - d1 * LandingSpot.Clear(transform, centre, -d1, half);
            b1 = centre + d1 * LandingSpot.Clear(transform, centre, d1, half);
            a2 = centre - d2 * LandingSpot.Clear(transform, centre, -d2, half);
            b2 = centre + d2 * LandingSpot.Clear(transform, centre, d2, half);
            a1.y = b1.y = a2.y = b2.y = from.y;
        }

        protected override void Activate()
        {
            ClearTelegraph();
            firstStrike = true;
            strikeBonus = 0f;
            if (!IsReplica)
            {
                strikeBonus = MandrakeKit.BeginStrike(gameObject, out _);
                // Damage is resolved where Health is the authority; that copy is the one made untouchable.
            }
            if (Health.IsAuthority) Health.Invulnerable = true;

            Vector3 from = transform.position;
            MoveTo(a1);
            if (!Stealthed.HiddenHere(gameObject)) AbilityFx.Teleport(from, a1, MandrakeKit.Baleflame);
            BeginPhase(Phase.Dash1, dashDuration);
            MeleeSound.Slash(0.7f, 0.7f, 0.8f);
        }

        void BeginPhase(Phase next, float seconds)
        {
            phase = next;
            phaseEnd = Time.time + seconds;
        }

        protected override void Update()
        {
            base.Update();
            if (phase == Phase.Idle) return;
            if (Health.IsDead)
            {
                Finish();
                return;
            }

            float t = 1f - Mathf.Clamp01((phaseEnd - Time.time) / Mathf.Max(0.01f, phase == Phase.Blink ? blinkPause : dashDuration));
            switch (phase)
            {
                case Phase.Dash1:
                    MoveTo(Vector3.Lerp(a1, b1, t));
                    if (t >= 1f)
                    {
                        SlashLine(a1, b1);
                        BeginPhase(Phase.Blink, blinkPause);
                    }
                    break;
                case Phase.Blink:
                    if (t >= 1f)
                    {
                        MoveTo(a2);
                        AbilityFx.Teleport(b1, a2, MandrakeKit.Baleflame);
                        MeleeSound.Slash(0.7f, 0.75f, 0.85f);
                        BeginPhase(Phase.Dash2, dashDuration);
                    }
                    break;
                case Phase.Dash2:
                    MoveTo(Vector3.Lerp(a2, b2, t));
                    if (t >= 1f)
                    {
                        SlashLine(a2, b2);
                        Finish();
                    }
                    break;
            }
        }

        // The owner moves; a replica follows the network position. Moved directly (not through the controller) so walls
        // and characters don't stop the dash.
        void MoveTo(Vector3 position)
        {
            if (IsReplica) return;
            transform.position = position;
            Physics.SyncTransforms();
        }

        static readonly Collider[] hitBuffer = new Collider[64];

        void SlashLine(Vector3 a, Vector3 b)
        {
            SlashTrail.Spawn(a, b, slashWidth);
            if (IsReplica) return;

            var struck = new List<Health>();
            int count = Physics.OverlapCapsuleNonAlloc(a + Vector3.up, b + Vector3.up, slashWidth, hitBuffer, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Health target = hitBuffer[i].GetComponentInParent<Health>();
                if (target == null || target == Health || target.IsDead || target.Team == Health.Team || struck.Contains(target)) continue;
                struck.Add(target);
            }

            float bonus = firstStrike ? strikeBonus : 0f;
            firstStrike = false;
            foreach (Health target in struck)
            {
                float dealt = (damage + bonus) * MandrakeKit.VenomScale(target);
                Hit hit = Hit.Direct(dealt).WithPoison(poisonStacks, poisonDamagePerSecond, poisonDuration);
                target.Strike(hit, gameObject, landed =>
                {
                    if (landed) MeleeSound.Impact(1);
                });
            }
        }

        void Finish()
        {
            phase = Phase.Idle;
            Health.Invulnerable = false;
            if (!IsReplica) StartCooldown();
        }

        protected override void Cancel()
        {
            ClearTelegraph();
            if (phase != Phase.Idle) Finish();
        }

        void ClearTelegraph()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
            telegraph = null;
        }

        void OnDestroy()
        {
            ClearTelegraph();
        }

        // The X marked on the ground during the wind-up: two faint bands that brighten as the strike nears.
        sealed class Telegraph : MonoBehaviour
        {
            Material material;
            float start;

            public static Telegraph Spawn(Vector3 a1, Vector3 b1, Vector3 a2, Vector3 b2, float width)
            {
                var go = new GameObject("Shadow Cross Telegraph");
                var t = go.AddComponent<Telegraph>();
                t.start = Time.time;
                t.material = VisualUtil.UnlitMaterial(new Color(0.45f, 1f, 0.3f, 0.15f), 3000);
                Band(go.transform, a1, b1, width * 2f, 0.17f, t.material);
                Band(go.transform, a2, b2, width * 2f, 0.17f, t.material);
                return t;
            }

            void Update()
            {
                Color c = material.color;
                c.a = Mathf.Min(0.45f, 0.15f + (Time.time - start) * 0.9f);
                material.color = c;
            }

            void OnDestroy()
            {
                if (material != null) Destroy(material);
            }
        }

        // A flat band on the ground from a to b.
        static Transform Band(Transform parent, Vector3 a, Vector3 b, float width, float height, Material material)
        {
            GameObject band = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(band.GetComponent<Collider>());
            var r = band.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Vector3 d = b - a;
            d.y = 0f;
            band.transform.SetParent(parent, false);
            band.transform.position = new Vector3((a.x + b.x) * 0.5f, height, (a.z + b.z) * 0.5f);
            band.transform.rotation = Quaternion.LookRotation(d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward, Vector3.up);
            band.transform.localScale = new Vector3(width, 0.01f, d.magnitude);
            return band.transform;
        }

        // The cut left behind: a bright baleflame gash on the ground and a tall blade-flash along the line, fading out.
        sealed class SlashTrail : MonoBehaviour
        {
            const float Life = 0.9f;
            Material gash;
            Material flash;
            Transform flashBand;
            float start;

            public static void Spawn(Vector3 a, Vector3 b, float width)
            {
                var go = new GameObject("Shadow Cross Slash");
                var s = go.AddComponent<SlashTrail>();
                s.start = Time.time;
                s.gash = VisualUtil.UnlitMaterial(MandrakeKit.BaleflameBright, 3001);
                s.flash = VisualUtil.UnlitMaterial(new Color(0.85f, 1f, 0.8f, 0.8f), 3002);
                Band(go.transform, a, b, width * 0.6f, 0.18f, s.gash);
                s.flashBand = Band(go.transform, a, b, 0.12f, 1.1f, s.flash);
                s.flashBand.localScale = new Vector3(0.08f, 1.6f, s.flashBand.localScale.z);
            }

            void Update()
            {
                float t = (Time.time - start) / Life;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                Color g = gash.color;
                g.a = MandrakeKit.BaleflameBright.a * (1f - t);
                gash.color = g;
                Color f = flash.color;
                f.a = 0.8f * Mathf.Clamp01(1f - t * 3f);
                flash.color = f;
            }

            void OnDestroy()
            {
                if (gash != null) Destroy(gash);
                if (flash != null) Destroy(flash);
            }
        }
    }
}
