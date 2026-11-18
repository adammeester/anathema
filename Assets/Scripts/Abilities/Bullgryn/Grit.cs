using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // Passive: Grit. Damage blocked by the slabshield (Brace) builds stacks: one per `damagePerStack`, up to `maxStacks`.
    // Each stack gives armour and shortens the crowd control he suffers. At full Grit his next primary attack is
    // empowered and spends every stack (Slab Slam with the maul, a Krak Shell with the gauntlet). After `decayDelay`
    // seconds without blocking, stacks fade one every `decayInterval`.
    //
    // Every machine counts the same blocks (the server resolves them and tells the others, see Brace), so every copy
    // holds the same stacks: the server's copy needs them for the armour, the owner's for the empowered swing and HUD.
    public class Grit : MonoBehaviour, IPassiveStacks
    {
        [SerializeField] float damagePerStack = 15f;
        [SerializeField] int maxStacks = 5;
        [SerializeField] float armorPerStack = 0.04f;
        [SerializeField] float crowdControlPerStack = 0.08f;
        [SerializeField] float decayDelay = 4f;
        [SerializeField] float decayInterval = 1.5f;

        Health health;
        float blocked;          // damage banked toward the next stack
        int stacks;
        float lastBlock = float.NegativeInfinity;
        float nextDecay;

        public string PassiveName => "Grit";
        public int Stacks => stacks;
        public int MaxStacks => maxStacks;
        public bool Full => stacks >= maxStacks;
        public Color StackColor => BullgrynKit.Hazard;
        public float NextStackProgress => Full ? 0f : Mathf.Clamp01(blocked / damagePerStack);

        void Awake()
        {
            health = GetComponent<Health>();
        }

        public void AddBlocked(float amount)
        {
            if (amount <= 0f) return;
            lastBlock = Time.time;
            nextDecay = Time.time + decayDelay;
            if (Full) return;
            blocked += amount;
            while (blocked >= damagePerStack && stacks < maxStacks)
            {
                blocked -= damagePerStack;
                stacks++;
            }
            if (Full) blocked = 0f;
        }

        // The empowered attack spends everything.
        public void Spend()
        {
            stacks = 0;
            blocked = 0f;
        }

        public void Clear() => Spend();

        void Update()
        {
            if (health.IsDead)
            {
                if (stacks > 0 || blocked > 0f) Clear();
                return;
            }

            if (stacks > 0 && Time.time - lastBlock >= decayDelay && Time.time >= nextDecay)
            {
                stacks--;
                nextDecay = Time.time + decayInterval;
            }
            if (stacks == 0 && Time.time - lastBlock >= decayDelay) blocked = 0f;

            // Armour is resolved where damage is (the authority), refreshed every frame so it follows the stacks.
            if (health.IsAuthority) health.HoldArmor(stacks * armorPerStack, 0.25f);
            // Crowd control is shortened on every copy that applies it (the server and the owner).
            health.CrowdControlScale = Mathf.Max(0.2f, 1f - stacks * crowdControlPerStack);
        }
    }
}
