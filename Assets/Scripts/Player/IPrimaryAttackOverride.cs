namespace Anathema.Player
{
    // Lets a character upgrade take over the left-mouse attack (BasicAttack) without BasicAttack knowing about it.
    // While Active, BasicAttack shows AttackName, winds up for AttackWindup, uses AttackCooldown, and calls
    // AttackBegin / AttackStrike / AttackCancel instead of firing its own bolt or swing. These run on the owner and on
    // replicas (replicas must only show visuals).
    public interface IPrimaryAttackOverride
    {
        bool Active { get; }
        string AttackName { get; }
        float AttackWindup { get; }
        float AttackCooldown { get; }
        void AttackBegin();
        void AttackStrike();
        void AttackCancel();
    }
}
