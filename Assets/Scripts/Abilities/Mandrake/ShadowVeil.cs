using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // E: the Mandrake melts into the shadows. Enemies can't see it at all (body, health bar and effects vanish from
    // their screens) and it moves faster. Any attack breaks the veil; Shadow Jump doesn't. A strike thrown from the veil
    // briefly stuns. Attacks still connect with a veiled Mandrake if they happen to hit it.
    // EX (Shift, costs energy): Shed the Light. The same veil, but as he fades he sheds every slow, root, poison, burn,
    // mark and healing cut on him (Purge) and surges away faster for a moment. His escape, not another stun.
    public class ShadowVeil : Ability
    {
        [SerializeField] float duration = 4f;

        [Header("EX: Shed the Light")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float exHasteSeconds = 2f;

        Energy energy;

        public override string DisplayName => "Shadow Veil";
        public override string KeyLabel => "E";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Shed the Light";
        public override string ExDescription => "Veil, shed every debuff, run";

        void Reset()
        {
            cooldown = 12f;
        }

        protected override void Awake()
        {
            base.Awake();
            energy = GetComponent<Energy>();
        }

        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.E);

        protected override void Activate()
        {
            // Replicas draw the veil (and the haste) from the owner's published status (NetPlayer), not from the cue;
            // no flash either, which would show enemies where he vanished.
            if (IsReplica) return;

            ExActivation = TrySpendEx(energy, exEnergyCost);
            Stealthed.Apply(gameObject, duration);
            if (!ExActivation) return;

            // Hasted's default bonus is what the server and replicas assume, so it is used as is.
            Purge.Apply(gameObject, gameObject);
            Hasted.Apply(gameObject, exHasteSeconds);
        }

        protected override void Cancel()
        {
        }
    }
}
