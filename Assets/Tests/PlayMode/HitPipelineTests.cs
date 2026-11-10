using System.Collections.Generic;
using Anathema.Combat;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Anathema.Tests
{
    // The hit pipeline on one machine (offline path): damage and its riders travel as one Hit, and the riders only take
    // hold when the hit lands. The network leg (server validation, result back to the attacker) needs two running
    // instances and is covered by the manual host + client checks.
    public class HitPipelineTests
    {
        readonly List<GameObject> spawned = new List<GameObject>();

        Health attacker;
        Health victim;

        GameObject Spawn(string name, int team, float maxHealth = 100f)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.AddComponent<Health>().Configure(maxHealth, team);
            return go;
        }

        [SetUp]
        public void SetUp()
        {
            attacker = Spawn("Attacker", 1).GetComponent<Health>();
            victim = Spawn("Victim", 2).GetComponent<Health>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in spawned) if (go != null) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        // Runs Strike and returns what the result callback reported (null if it never ran).
        bool? Strike(Hit hit)
        {
            bool? result = null;
            victim.Strike(hit, attacker.gameObject, landed => result = landed);
            return result;
        }

        // --- Landing hits ---------------------------------------------------------------------------------------

        [Test]
        public void LandedHit_DealsDamageAndReportsLanded()
        {
            bool? result = Strike(Hit.Direct(30f));

            Assert.AreEqual(true, result);
            Assert.AreEqual(70f, victim.Current, 0.001f);
        }

        [Test]
        public void LandedHit_AppliesStun()
        {
            Strike(Hit.Direct(10f).WithStun(1.5f));

            Assert.IsTrue(Stunned.IsStunned(victim.gameObject));
        }

        [Test]
        public void LandedHit_AppliesSlow()
        {
            Strike(Hit.Direct(10f).WithSlow(2f, 0.4f));

            Assert.AreEqual(0.6f, Slowed.Multiplier(victim.gameObject), 0.001f);
        }

        [Test]
        public void LandedHit_AppliesKnockback()
        {
            Strike(Hit.Direct(10f).WithKnockback(Vector3.forward, 3f, 0.3f));

            Assert.IsNotNull(victim.GetComponent<Knockback>());
        }

        [Test]
        public void LandedHit_AppliesEveryRider()
        {
            Strike(Hit.Direct(10f).WithSlow(2f, 0.4f).WithKnockback(Vector3.forward, 3f, 0.3f));

            Assert.AreEqual(0.6f, Slowed.Multiplier(victim.gameObject), 0.001f);
            Assert.IsNotNull(victim.GetComponent<Knockback>());
        }

        [Test]
        public void HitWithoutRiders_AppliesNoStatus()
        {
            Strike(Hit.Direct(10f));

            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
            Assert.AreEqual(1f, Slowed.Multiplier(victim.gameObject));
        }

        // --- Negated hits (the bug this exists for: a countered snipe must not stun) -----------------------------

        [Test]
        public void CounteredHit_DealsNoDamage_NoStun_ReportsNotLanded()
        {
            // What Counter does while the guard is up: the hit is zeroed.
            victim.DamageModifier = (amount, source) => 0f;

            bool? result = Strike(Hit.Direct(50f).WithStun(2f));

            Assert.AreEqual(false, result);
            Assert.AreEqual(100f, victim.Current, 0.001f);
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
        }

        [Test]
        public void CounteredHit_AppliesNoSlowOrKnockback()
        {
            victim.DamageModifier = (amount, source) => 0f;

            Strike(Hit.Direct(50f).WithSlow(2f, 0.5f).WithKnockback(Vector3.forward, 3f, 0.3f));

            Assert.AreEqual(1f, Slowed.Multiplier(victim.gameObject));
            Assert.IsNull(victim.GetComponent<Knockback>());
        }

        [Test]
        public void CounterOnlyNegatesTheFirstHit_WhenItDropsItsGuard()
        {
            // Counter cancels itself on the first hit: later hits land normally.
            victim.DamageModifier = (amount, source) =>
            {
                victim.DamageModifier = null;
                return 0f;
            };

            Assert.AreEqual(false, Strike(Hit.Direct(20f).WithStun(1f)));
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));

            Assert.AreEqual(true, Strike(Hit.Direct(20f).WithStun(1f)));
            Assert.IsTrue(Stunned.IsStunned(victim.gameObject));
            Assert.AreEqual(80f, victim.Current, 0.001f);
        }

        [Test]
        public void InvulnerableVictim_TakesNothing_NoStun()
        {
            victim.Invulnerable = true;

            bool? result = Strike(Hit.Direct(50f).WithStun(2f));

            Assert.AreEqual(false, result);
            Assert.AreEqual(100f, victim.Current, 0.001f);
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
        }

        [Test]
        public void DeadVictim_ReportsNotLanded_NoStun()
        {
            victim.Strike(Hit.Direct(500f), attacker.gameObject);
            Assert.IsTrue(victim.IsDead);

            bool? result = Strike(Hit.Direct(10f).WithStun(2f));

            Assert.AreEqual(false, result);
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
        }

        [Test]
        public void LethalHit_AppliesNoRiders()
        {
            bool? result = Strike(Hit.Direct(500f).WithStun(2f).WithSlow(2f, 0.5f));

            Assert.AreEqual(true, result);
            Assert.IsTrue(victim.IsDead);
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
            Assert.AreEqual(1f, Slowed.Multiplier(victim.gameObject));
        }

        [Test]
        public void ZeroDamageHit_ReportsNotLanded()
        {
            Assert.AreEqual(false, Strike(Hit.Direct(0f).WithStun(2f)));
            Assert.IsFalse(Stunned.IsStunned(victim.gameObject));
        }

        // --- Shields and armour ---------------------------------------------------------------------------------

        [Test]
        public void FullyAbsorbedHit_StillLands_AndCarriesItsRiders()
        {
            // A shield soaking the damage is a landed hit (the attacker still earns energy), as before this change.
            victim.Absorber = (amount, source) => 0f;

            bool? result = Strike(Hit.Direct(30f).WithStun(1f));

            Assert.AreEqual(true, result);
            Assert.AreEqual(100f, victim.Current, 0.001f);
            Assert.IsTrue(Stunned.IsStunned(victim.gameObject));
        }

        [Test]
        public void Armour_ReducesDamage_ButHitStillCarriesRiders()
        {
            victim.Armor = 0.5f;

            Strike(Hit.Direct(40f).WithStun(1f));

            Assert.AreEqual(80f, victim.Current, 0.001f);
            Assert.IsTrue(Stunned.IsStunned(victim.gameObject));
        }

        // --- Callback and API behaviour -------------------------------------------------------------------------

        [Test]
        public void Strike_WithoutCallback_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => victim.Strike(Hit.Direct(10f).WithStun(1f), attacker.gameObject));
            Assert.AreEqual(90f, victim.Current, 0.001f);
        }

        [Test]
        public void Strike_ReportsExactlyOnce()
        {
            int calls = 0;
            victim.Strike(Hit.Direct(10f), attacker.gameObject, landed => calls++);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void Aoe_IgnoresDamageModifier_SoACounterDoesNotNegateIt()
        {
            // Counters only answer direct hits; area damage skips the modifier (unchanged behaviour).
            victim.DamageModifier = (amount, source) => 0f;

            bool? result = Strike(Hit.Direct(20f).AsAoe().WithSlow(1f, 0.3f));

            Assert.AreEqual(true, result);
            Assert.AreEqual(80f, victim.Current, 0.001f);
            Assert.AreEqual(0.7f, Slowed.Multiplier(victim.gameObject), 0.001f);
        }

        [Test]
        public void Hit_AcceptsOnlyThreeRiders()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("at most"));

            Hit hit = Hit.Direct(10f).WithStun(1f).WithSlow(1f, 0.2f).WithKnockback(Vector3.forward, 1f, 0.2f).WithRoot(1f);
            Strike(hit);

            Assert.IsTrue(Stunned.IsStunned(victim.gameObject));
            Assert.AreEqual(0.8f, Slowed.Multiplier(victim.gameObject), 0.001f);
            Assert.IsNotNull(victim.GetComponent<Knockback>());
            Assert.IsFalse(Rooted.IsRooted(victim.gameObject));
        }

        // --- Grey Knight riders (root, silence, panic, burn) ----------------------------------------------------

        [Test]
        public void LandedHit_AppliesRoot()
        {
            Strike(Hit.Direct(10f).WithRoot(1f));
            Assert.IsTrue(Rooted.IsRooted(victim.gameObject));
        }

        [Test]
        public void LandedHit_AppliesSilence()
        {
            Strike(Hit.Direct(10f).WithSilence(1f));
            Assert.IsTrue(Silenced.IsSilenced(victim.gameObject));
        }

        [Test]
        public void LandedHit_AppliesPanicFromTheAttacker()
        {
            attacker.transform.position = new Vector3(-3f, 0f, 0f);
            Strike(Hit.Direct(1f).WithPanic(1f));

            Assert.IsTrue(Panicked.IsPanicked(victim.gameObject));
            Assert.IsTrue(Panicked.TryGetFleeDirection(victim.gameObject, out Vector3 away));
            Assert.Greater(away.x, 0.9f, "The victim should run away from the attacker.");
        }

        [Test]
        public void LandedHit_AppliesBurn()
        {
            Strike(Hit.Direct(10f).WithBurn(6f, 4f));
            Assert.IsNotNull(victim.GetComponent<Burning>());
        }

        [Test]
        public void PurgatusStyleHit_AppliesAllThreeRiders()
        {
            Strike(Hit.Direct(10f).AsAoe().WithBurn(6f, 4f).WithKnockback(Vector3.forward, 2f, 0.2f).WithSilence(1f));

            Assert.IsNotNull(victim.GetComponent<Burning>());
            Assert.IsNotNull(victim.GetComponent<Knockback>());
            Assert.IsTrue(Silenced.IsSilenced(victim.gameObject));
        }

        // --- Healing and server-held effects (offline, this machine is the authority) ---------------------------

        [Test]
        public void OfflineHealth_IsTheAuthority()
        {
            Assert.IsTrue(victim.IsAuthority);
        }

        [Test]
        public void Heal_RestoresRecoverable_AndReportsTheHealer()
        {
            Strike(Hit.Direct(20f));
            float credited = 0f;
            GameObject creditedTo = null;
            victim.Healed += (amount, source) => { credited = amount; creditedTo = source; };

            float healed = victim.Heal(15f, attacker.gameObject);

            Assert.AreEqual(15f, healed, 0.001f);
            Assert.AreEqual(95f, victim.Current, 0.001f);
            Assert.AreEqual(15f, credited, 0.001f);
            Assert.AreSame(attacker.gameObject, creditedTo);
        }

        [Test]
        public void Bleed_DealsDamageHere_WhenThisMachineIsTheAuthority()
        {
            Strike(Hit.Direct(1f).WithBleed(10f, 3f));

            Assert.IsTrue(Bleeding.IsBleedingForReal(victim.gameObject));
        }

        [Test]
        public void BleedReplica_IsVisualOnly()
        {
            Bleeding.ShowReplica(victim.gameObject);

            Assert.IsNotNull(victim.GetComponent<Bleeding>());
            Assert.IsFalse(Bleeding.IsBleedingForReal(victim.gameObject));
        }

        [Test]
        public void ResetHealth_ClearsDamageAndLastStand()
        {
            victim.LastStand = true;
            Strike(Hit.Direct(500f)); // survives at 1 thanks to Last Stand
            Assert.AreEqual(1f, victim.Current, 0.001f);

            victim.ResetHealth();

            Assert.AreEqual(100f, victim.Current, 0.001f);
            Assert.AreEqual(0f, victim.RoundDamageTaken, 0.001f);
        }

        // --- Berserker riders (bleed, maim) ---------------------------------------------------------------------

        [Test]
        public void LandedHit_AppliesBleedAndMaim()
        {
            Strike(Hit.Direct(10f).WithMaim(2f, 0.3f).WithBleed(4f, 3f));

            Assert.IsNotNull(victim.GetComponent<Maimed>());
            Assert.IsNotNull(victim.GetComponent<Bleeding>());
        }

        [Test]
        public void CounteredHit_AppliesNoBleedOrMaim()
        {
            victim.DamageModifier = (amount, source) => 0f;

            Strike(Hit.Direct(10f).WithMaim(2f, 0.3f).WithBleed(4f, 3f));

            Assert.IsNull(victim.GetComponent<Maimed>());
            Assert.IsNull(victim.GetComponent<Bleeding>());
        }

        [Test]
        public void BleedAndMaim_SurviveNetworkSerialization()
        {
            Hit sent = Hit.Direct(5f).WithMaim(2f, 0.3f).WithBleed(4f, 3f).WithStun(1f);
            Hit received = default;
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                    reader.ReadNetworkSerializable(out received);
            }

            Assert.AreEqual(sent.Rider0.Kind, received.Rider0.Kind);
            Assert.AreEqual(0.3f, received.Rider0.B);
            Assert.AreEqual(sent.Rider1.Kind, received.Rider1.Kind);
            Assert.AreEqual(4f, received.Rider1.A);
            Assert.AreEqual(sent.Rider2.Kind, received.Rider2.Kind);
            Assert.AreEqual(1f, received.Rider2.A);
        }

        [Test]
        public void CounteredHit_AppliesNoRootSilencePanicOrBurn()
        {
            victim.DamageModifier = (amount, source) => 0f;

            Strike(Hit.Direct(20f).WithRoot(1f).WithSilence(1f).WithBurn(6f, 4f));
            Strike(Hit.Direct(20f).WithPanic(1f));

            Assert.IsFalse(Rooted.IsRooted(victim.gameObject));
            Assert.IsFalse(Silenced.IsSilenced(victim.gameObject));
            Assert.IsNull(victim.GetComponent<Burning>());
            Assert.IsFalse(Panicked.IsPanicked(victim.gameObject));
        }

        // --- Wire format ----------------------------------------------------------------------------------------

        [Test]
        public void Hit_SurvivesNetworkSerialization()
        {
            Hit sent = Hit.Direct(42.5f).WithPierce(0.25f).WithShieldBreak(1.5f)
                .WithStun(1.25f)
                .WithKnockback(new Vector3(0f, 0f, 1f), 3f, 0.4f);
            sent.Dot = true;

            Hit received = default;
            using (var writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);

                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out received);
                }
            }

            Assert.AreEqual(42.5f, received.Damage);
            Assert.IsTrue(received.Dot);
            Assert.IsFalse(received.Aoe);
            Assert.AreEqual(0.25f, received.Pierce);
            Assert.AreEqual(1.5f, received.ShieldBreak);
            Assert.AreEqual(sent.Rider0.Kind, received.Rider0.Kind);
            Assert.AreEqual(1.25f, received.Rider0.A);
            Assert.AreEqual(sent.Rider1.Kind, received.Rider1.Kind);
            Assert.AreEqual(3f, received.Rider1.A);
            Assert.AreEqual(0.4f, received.Rider1.B);
            Assert.AreEqual(Vector3.forward, received.Rider1.Direction);
        }

        [Test]
        public void EmptyRider_IsNoneAfterSerialization()
        {
            Hit sent = Hit.Direct(10f);
            Hit received = default;
            using (var writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (var reader = new FastBufferReader(writer, Allocator.Temp))
                    reader.ReadNetworkSerializable(out received);
            }

            Assert.IsTrue(received.Rider0.IsNone);
            Assert.IsTrue(received.Rider1.IsNone);
            Assert.IsTrue(received.Rider2.IsNone);
        }

        [Test]
        public void DefaultHit_IsNotMistakenForAnEmptyRiderSet()
        {
            // A default(Hit) has Kind 0 (Stun), not None: always build hits with Hit.Direct.
            Hit raw = default;
            Assert.IsFalse(raw.Rider0.IsNone);
            Assert.IsTrue(Hit.Direct(1f).Rider0.IsNone);
        }
    }
}
