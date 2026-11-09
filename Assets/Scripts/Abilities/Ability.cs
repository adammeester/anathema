using Anathema.Combat;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Abilities
{
    // Base for character-specific abilities. Each ability is its own component, added to the player only for the
    // character that owns it, and lives in Scripts/Abilities/<Character>/.
    // Pressing C (or dying) calls Cancel() on every ability, so Cancel must be safe to call when nothing is active.
    [RequireComponent(typeof(MouseAim), typeof(Health))]
    public abstract class Ability : MonoBehaviour
    {
        public const KeyCode CancelKey = KeyCode.C;

        [SerializeField] protected float cooldown = 1f;

        protected MouseAim Aim { get; private set; }
        protected Health Health { get; private set; }

        // Set by an ability that has borrowed the left mouse button (e.g. click-to-teleport) so the basic attack stays quiet.
        public static bool PrimaryFireBlocked { get; set; }

        float nextUseTime;

        // Shown on the HUD ability bar.
        public virtual string DisplayName => GetType().Name;
        public virtual string KeyLabel => "";

        // EX version info for the HUD's EX menu (shown while Shift is held). ExCost <= 0 means no EX version.
        public virtual float ExCost => 0f;
        public virtual string ExDescription => "";
        public virtual string ExName => DisplayName;

        // False while something other than the cooldown stops this ability being used (e.g. an ultimate without a
        // full energy bar). Only affects how the HUD draws the slot.
        public virtual bool Available => true;

        // True while this ability holds the character in place (PlayerMovement stops walking; gravity still applies).
        public virtual bool RootsMovement => false;

        // Upgrades shorten (or lengthen) this ability's cooldown by setting this; 1 = as authored.
        public float CooldownScale { get; set; } = 1f;
        float EffectiveCooldown => cooldown * CooldownScale;

        public float CooldownRemaining => IsCasting ? 0f : Mathf.Max(0f, nextUseTime - Time.time);
        // 1 = just used, 0 = ready.
        public float CooldownNormalized => EffectiveCooldown > 0f ? Mathf.Clamp01(CooldownRemaining / EffectiveCooldown) : 0f;

        protected virtual void Awake()
        {
            Aim = GetComponent<MouseAim>();
            Health = GetComponent<Health>();
        }

        bool awaitRelease;

        // Online: another player's character. Abilities there only replay visuals when the owner's machine says so
        // (ReplayActivate / ReplayCancel); they never read this machine's input or deal damage.
        protected bool IsReplica => Health.IsReplica;

        int abilityIndex = -1;

        // Cue indices at or above this mean "ability (index - offset), EX version" (see NetPlayer.SendCue).
        public const int ExCueOffset = 100;

        // Set by an ability's Activate() when this use is the EX (energy-spending) version. On a replica it is
        // pre-set from the owner's cue before Activate() runs, so visuals can match.
        protected bool ExActivation { get; set; }

        // Called on a replica when the owner activated this ability.
        public void ReplayActivate(bool ex = false)
        {
            ExActivation = ex;
            if (!Health.IsDead) BeginCast();
        }

        // ---- Wind-up -------------------------------------------------------------------------------------------------
        // An ability with WindupDuration > 0 doesn't fire when the key is pressed: it winds up first (a charging orb and
        // a collapsing ring show it), then Activate() runs as the strike. C, death, a stun, silence, panic or knockback
        // during the wind-up cancels it: OnWindupCancel() is called, the cooldown is refunded and nothing fires.
        // The cooldown still starts at the press, so the fire rate of a wound-up ability is unchanged.
        // Energy for an EX version is spent in OnWindupStart() (owner only) and given back in OnWindupCancel().

        protected virtual float WindupDuration => 0f;
        protected virtual Color WindupColor => new Color(0.6f, 1f, 0.6f, 1f);

        // Called when the wind-up begins, on the owner and on replicas. The owner decides ExActivation here.
        protected virtual void OnWindupStart() { }

        // Called when a wind-up is cancelled before it strikes. Undo anything OnWindupStart() spent or posed.
        protected virtual void OnWindupCancel() { }

        public bool IsWindingUp => windingUp;

        bool windingUp;
        float windupEnd;
        WindupFx windupFx;

        void BeginCast()
        {
            float wind = WindupDuration;
            if (wind <= 0f)
            {
                Activate();
                return;
            }

            windingUp = true;
            windupEnd = Time.time + wind;
            OnWindupStart();
            if (windupFx != null) Destroy(windupFx.gameObject);
            windupFx = WindupFx.Spawn(transform, WindupColor, wind);
        }

        void EndWindup()
        {
            windingUp = false;
            if (windupFx != null) Destroy(windupFx.gameObject);
            windupFx = null;
        }

        // Stops a wind-up in progress (refunding it), then whatever the ability itself has running.
        void CancelCast()
        {
            if (windingUp)
            {
                EndWindup();
                OnWindupCancel();
                if (!IsReplica) RefundCooldown();
            }
            Cancel();
        }

        // True while something stops this character acting: a stun, silence, panic or knockback.
        protected bool IsDisabled => Stunned.IsStunned(gameObject) || Silenced.IsSilenced(gameObject)
                                     || Panicked.IsPanicked(gameObject) || GetComponent<Knockback>() != null
                                     || IsGuardingOtherAbility;

        // True while the character's Counter guard is up: no other ability may start or continue.
        bool IsGuardingOtherAbility
        {
            get
            {
                if (this is Counter) return false;
                var counter = GetComponent<Counter>();
                return counter != null && counter.IsGuarding;
            }
        }

        // Owner only: cancel this ability (refunding a wind-up) and tell the other machine to stop replaying it.
        protected void InterruptSelf()
        {
            CancelCast();
            if (Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
        }

        // Used by the Counter: stop this ability's wind-up or channel the moment the guard goes up.
        public void InterruptForGuard()
        {
            if (!IsReplica) CancelCast();
        }

        void TickWindup()
        {
            if (!windingUp) return;

            if (!IsReplica && IsDisabled)
            {
                InterruptSelf();
                return;
            }

            if (Time.time < windupEnd) return;
            EndWindup();
            Activate();
        }

        Energy exEnergy;

        // True when the EX menu (Shift) is held and this ability has an EX version the character can't afford:
        // pressing the key then does nothing at all (no normal-version fallback).
        public bool ExBlocked
        {
            get
            {
                if (ExCost <= 0f || !Input.GetKey(KeyCode.LeftShift)) return false;
                if (exEnergy == null) exEnergy = GetComponent<Energy>();
                return exEnergy == null || exEnergy.Current < ExCost;
            }
        }

        // True when the EX modifier is held and enough energy was spent. Owner only; replicas read ExActivation.
        protected bool TrySpendEx(Energy energy, float cost)
        {
            if (!Input.GetKey(KeyCode.LeftShift) || energy == null) return false;
            return energy.TrySpend(cost);
        }

        public void ReplayCancel()
        {
            CancelCast();
        }

        protected virtual void Update()
        {
            if (IsReplica)
            {
                if (Health.IsDead) CancelCast();
                else TickWindup();
                return;
            }

            bool cancelPressed = Input.GetKeyDown(CancelKey);
            if (Health.IsDead || cancelPressed) CancelCast();
            if (cancelPressed && Health.Net != null && Health.Net.IsSpawned) Health.Net.SendCue(-1, Aim.AimPoint);
            if (Health.IsDead || Anathema.Net.NetGame.Frozen || Anathema.Net.NetGame.InPrep) return;

            TickWindup();

            bool triggered = IsTriggered();
            // After a refunded cancel, a held key must be released before it can fire again, otherwise
            // cancelling a hold-to-use ability would instantly restart it.
            if (awaitRelease)
            {
                if (triggered) return;
                awaitRelease = false;
            }

            if (windingUp || Time.time < nextUseTime || !triggered || Silenced.IsSilenced(gameObject) || Stunned.IsStunned(gameObject)
                || Panicked.IsPanicked(gameObject) || IsGuardingOtherAbility) return;

            if (ExBlocked) return;

            // Wind-up abilities hold the cooldown back until their effect lands (they call StartCooldown()).
            float cooldownScale = ScalesWithAttackSpeed ? Enraged.AttackSpeedMultiplier(gameObject) : 1f;
            nextUseTime = CooldownStartsOnStrike ? float.PositiveInfinity : Time.time + EffectiveCooldown / cooldownScale;
            ExActivation = false;
            BeginCast();

            // Tell the other machines to replay this ability's visuals.
            if (Health.Net != null && Health.Net.IsSpawned)
            {
                if (abilityIndex < 0) abilityIndex = System.Array.IndexOf(GetComponents<Ability>(), this);
                Health.Net.SendCue(abilityIndex + (ExActivation ? ExCueOffset : 0), Aim.AimPoint);
            }
        }

        // True for basic attacks: their cooldown (and swing timings) speed up with attack speed bonuses such as Enraged.
        protected virtual bool ScalesWithAttackSpeed => false;

        // True for abilities with a wind-up: the cooldown only begins when the ability calls StartCooldown().
        protected virtual bool CooldownStartsOnStrike => false;

        // True from pressing the key until the strike lands, for abilities with CooldownStartsOnStrike.
        public bool IsCasting => float.IsPositiveInfinity(nextUseTime);

        // Makes the ability ready right now (e.g. another ability's payoff resets it). Does not interrupt a cast in progress.
        public void ResetCooldown()
        {
            if (!IsCasting) nextUseTime = 0f;
        }

        protected void StartCooldown()
        {
            nextUseTime = Time.time + EffectiveCooldown;
        }

        // Call from Cancel() when the ability is stopped before its effect lands (e.g. during a wind-up):
        // the cooldown is cleared as if it was never used.
        protected void RefundCooldown()
        {
            nextUseTime = 0f;
            awaitRelease = true;
        }

        protected abstract bool IsTriggered();
        protected abstract void Activate();

        // Stop any animation, effect or window this ability has running. The cooldown is not refunded by default;
        // abilities with a wind-up call RefundCooldown() when cancelled before the effect lands.
        protected abstract void Cancel();

        // Cuts off every ability this character is casting or channelling (a silence with the interrupt flag does this),
        // and tells the other machine to stop replaying them. Runs on the victim's own machine.
        public static void InterruptAll(GameObject target)
        {
            Ability[] abilities = target.GetComponents<Ability>();
            foreach (Ability a in abilities) a.CancelCast();
            if (abilities.Length == 0) return;
            Health h = abilities[0].Health;
            if (h.Net != null && h.Net.IsSpawned) h.Net.SendCue(-1, abilities[0].Aim.AimPoint);
        }

        // The charge-up telegraph: an orb gathering at the chest and a ring collapsing onto the caster's feet.
        // Destroyed by the ability when the wind-up strikes or is cancelled.
        sealed class WindupFx : MonoBehaviour
        {
            const int RingPoints = 36;

            Transform target;
            Transform orb;
            LineRenderer ring;
            Material orbMaterial;
            Material ringMaterial;
            Color color;
            float duration;
            float start;

            public static WindupFx Spawn(Transform caster, Color color, float duration)
            {
                var go = new GameObject("Windup Fx");
                go.transform.SetParent(caster, false);
                var fx = go.AddComponent<WindupFx>();
                fx.target = caster;
                fx.color = color;
                fx.duration = Mathf.Max(0.05f, duration);
                fx.start = Time.time;
                fx.Build();
                return fx;
            }

            void Build()
            {
                orbMaterial = VisualUtil.UnlitMaterial(new Color(color.r, color.g, color.b, 0.55f), 3001);
                ringMaterial = VisualUtil.UnlitMaterial(color, 3000);

                GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.name = "Windup Orb";
                Destroy(s.GetComponent<Collider>());
                s.GetComponent<Renderer>().sharedMaterial = orbMaterial;
                s.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.transform.SetParent(transform, false);
                s.transform.localPosition = Vector3.up * 1.3f;
                orb = s.transform;

                var ringObject = new GameObject("Windup Ring");
                ringObject.transform.SetParent(transform, false);
                ring = ringObject.AddComponent<LineRenderer>();
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = RingPoints;
                ring.numCapVertices = 2;
                ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ring.sharedMaterial = ringMaterial;
                Update();
            }

            void Update()
            {
                float t = Mathf.Clamp01((Time.time - start) / duration);

                // The orb swells and flickers faster as the strike nears.
                float size = (0.15f + 0.55f * t) * (1f + 0.12f * Mathf.Sin(Time.time * (20f + 30f * t)));
                orb.localScale = Vector3.one * size;

                float radius = Mathf.Lerp(1.5f, 0.35f, t * t);
                Vector3 centre = target.position + Vector3.up * 0.08f;
                for (int i = 0; i < RingPoints; i++)
                {
                    float a = i / (float)RingPoints * Mathf.PI * 2f;
                    ring.SetPosition(i, centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
                }
                ring.startWidth = ring.endWidth = 0.08f + 0.1f * t;

                Color c = color;
                c.a = Mathf.Lerp(0.3f, 0.95f, t);
                ringMaterial.color = c;
            }

            void OnDestroy()
            {
                if (orbMaterial != null) Destroy(orbMaterial);
                if (ringMaterial != null) Destroy(ringMaterial);
            }
        }
    }
}
