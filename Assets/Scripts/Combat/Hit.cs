using Anathema.Net;
using Unity.Netcode;
using UnityEngine;

namespace Anathema.Combat
{
    // A status effect that rides on a hit (stun, slow, knockback...). It only takes hold if the hit lands: a hit that
    // is negated (a counter, i-frames) carries none of its riders with it.
    public struct HitRider : INetworkSerializable
    {
        public const int None = -1;

        public int Kind;           // a NetPlayer.StatusKind, or None
        public float A, B;         // meaning depends on the kind (see Hit.ApplyRiders)
        public Vector3 Direction;

        public bool IsNone => Kind == None;

        public static HitRider Empty => new HitRider { Kind = None };
        public static HitRider Stun(float seconds) => new HitRider { Kind = (int)NetPlayer.StatusKind.Stun, A = seconds };
        public static HitRider Slow(float seconds, float fraction) => new HitRider { Kind = (int)NetPlayer.StatusKind.Slow, A = seconds, B = fraction };
        public static HitRider Knockback(Vector3 direction, float distance, float seconds) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Knockback, A = distance, B = seconds, Direction = direction };
        public static HitRider Root(float seconds) => new HitRider { Kind = (int)NetPlayer.StatusKind.Root, A = seconds };
        public static HitRider Silence(float seconds, bool interrupt = false) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Silence, A = seconds, B = interrupt ? 1f : 0f };
        // Panic runs from the attacker, so it needs the hit's source.
        public static HitRider Panic(float seconds) => new HitRider { Kind = (int)NetPlayer.StatusKind.Panic, A = seconds };
        // Burning damage is credited to the hit's source.
        public static HitRider Burn(float damagePerSecond, float seconds) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Burn, A = damagePerSecond, B = seconds };
        // Bleeding damage is credited to the hit's source.
        public static HitRider Bleed(float damagePerSecond, float seconds) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Bleed, A = damagePerSecond, B = seconds };
        // Poison damage is credited to the hit's source. Adds `stacks` stacks (see Poisoned).
        public static HitRider Poison(int stacks, float damagePerSecondPerStack, float seconds) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Poison, A = damagePerSecondPerStack, B = seconds, Direction = new Vector3(stacks, 0f, 0f) };
        // Balefire damage is credited to the hit's source, and only that source's next hit makes it burst.
        public static HitRider Balefire(float damagePerSecond, float seconds) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Balefire, A = damagePerSecond, B = seconds };
        public static HitRider Maim(float seconds, float slowFraction) =>
            new HitRider { Kind = (int)NetPlayer.StatusKind.Maim, A = seconds, B = slowFraction };

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref A);
            serializer.SerializeValue(ref B);
            serializer.SerializeValue(ref Direction);
        }
    }

    // One attack landing on one target: the damage and every status effect that comes with it, sent as a single unit
    // so the effects can never outlive the damage. Build with Hit.Direct(...).With...(); resolve with Health.Strike.
    public struct Hit : INetworkSerializable
    {
        public const int MaxRiders = 3;

        public float Damage;
        public bool Aoe;
        public bool Dot;
        public float Pierce;
        public float ShieldBreak;
        public HitRider Rider0, Rider1, Rider2;

        public static Hit Direct(float damage)
        {
            return new Hit { Damage = damage, ShieldBreak = 1f, Rider0 = HitRider.Empty, Rider1 = HitRider.Empty, Rider2 = HitRider.Empty };
        }

        public Hit AsDot() { Dot = true; return this; }

        public Hit AsAoe() { Aoe = true; return this; }
        public Hit WithPierce(float pierce) { Pierce = pierce; return this; }
        public Hit WithShieldBreak(float factor) { ShieldBreak = factor; return this; }

        public Hit With(HitRider rider)
        {
            if (Rider0.IsNone) Rider0 = rider;
            else if (Rider1.IsNone) Rider1 = rider;
            else if (Rider2.IsNone) Rider2 = rider;
            else Debug.LogWarning("Hit carries at most " + MaxRiders + " riders.");
            return this;
        }

        public Hit WithStun(float seconds) => With(HitRider.Stun(seconds));
        public Hit WithSlow(float seconds, float fraction) => With(HitRider.Slow(seconds, fraction));
        public Hit WithKnockback(Vector3 direction, float distance, float seconds) => With(HitRider.Knockback(direction, distance, seconds));
        public Hit WithRoot(float seconds) => With(HitRider.Root(seconds));
        public Hit WithSilence(float seconds, bool interrupt = false) => With(HitRider.Silence(seconds, interrupt));
        public Hit WithPanic(float seconds) => With(HitRider.Panic(seconds));
        public Hit WithBurn(float damagePerSecond, float seconds) => With(HitRider.Burn(damagePerSecond, seconds));
        public Hit WithBleed(float damagePerSecond, float seconds) => With(HitRider.Bleed(damagePerSecond, seconds));
        public Hit WithBalefire(float damagePerSecond, float seconds) => With(HitRider.Balefire(damagePerSecond, seconds));
        public Hit WithMaim(float seconds, float slowFraction) => With(HitRider.Maim(seconds, slowFraction));
        public Hit WithPoison(int stacks, float damagePerSecondPerStack, float seconds) => With(HitRider.Poison(stacks, damagePerSecondPerStack, seconds));

        // Runs on the machine that owns `victim`, after the damage landed.
        public void ApplyRiders(GameObject victim, GameObject attacker)
        {
            Apply(Rider0, victim, attacker);
            Apply(Rider1, victim, attacker);
            Apply(Rider2, victim, attacker);
        }

        static void Apply(HitRider r, GameObject victim, GameObject attacker)
        {
            if (r.IsNone) return;
            switch ((NetPlayer.StatusKind)r.Kind)
            {
                case NetPlayer.StatusKind.Stun: Stunned.Apply(victim, r.A); break;
                case NetPlayer.StatusKind.Slow: Slowed.Apply(victim, r.A, r.B); break;
                case NetPlayer.StatusKind.Knockback: Knockback.Apply(victim, r.Direction, r.A, r.B); break;
                case NetPlayer.StatusKind.Root: Rooted.Apply(victim, r.A); break;
                case NetPlayer.StatusKind.Silence: Silenced.Apply(victim, r.A, r.B > 0f); break;
                case NetPlayer.StatusKind.Panic: Panicked.Apply(victim, attacker, r.A); break;
                case NetPlayer.StatusKind.Burn: Burning.Apply(victim, attacker, r.A, r.B); break;
                case NetPlayer.StatusKind.Bleed: Bleeding.Apply(victim, attacker, r.A, r.B); break;
                case NetPlayer.StatusKind.Balefire: Balefire.Apply(victim, attacker, r.A, r.B); break;
                case NetPlayer.StatusKind.Maim: Maimed.Apply(victim, r.A, r.B); break;
                case NetPlayer.StatusKind.Poison: Poisoned.Apply(victim, attacker, Mathf.RoundToInt(r.Direction.x), r.A, r.B); break;
                default: Debug.LogWarning("Unsupported hit rider " + (NetPlayer.StatusKind)r.Kind); break;
            }
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Damage);
            serializer.SerializeValue(ref Aoe);
            serializer.SerializeValue(ref Dot);
            serializer.SerializeValue(ref Pierce);
            serializer.SerializeValue(ref ShieldBreak);
            serializer.SerializeNetworkSerializable(ref Rider0);
            serializer.SerializeNetworkSerializable(ref Rider1);
            serializer.SerializeNetworkSerializable(ref Rider2);
        }
    }
}
