using UnityEngine;

namespace Anathema.Abilities.Bullgryn
{
    // Shows what the Bullgryn is holding. The model (Art/bullgryn.py) is one skinned mesh carrying both weapons and the
    // slabshield, each weighted to its own bone: Maul (on the right hand), Gauntlet (on the right forearm) and Shield (on
    // the left hand). Anything not in hand is hidden by scaling its bone to zero:
    //   - the weapon he isn't wielding (follows UpgradeState's synced mask);
    //   - the shield while Living Wall stands, planted in the ground or sliding from a shove (every machine builds the
    //     wall from the cue, so this is right everywhere).
    // Added by UpgradeState at runtime.
    public class BullgrynWeaponVisual : MonoBehaviour
    {
        Transform maul, gauntlet, shield;
        int shown = -1;

        void Awake()
        {
            Transform model = transform.Find("Model");
            if (model == null) { enabled = false; return; }
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Maul" && maul == null) maul = t;
                else if (t.name == "Gauntlet" && gauntlet == null) gauntlet = t;
                else if (t.name == "Shield" && shield == null) shield = t;
            }
            Apply();
        }

        int State => (BullgrynUpgrade.HasGauntlet(gameObject) ? 1 : 0) | (BullgrynKit.ShieldPlanted(gameObject) ? 2 : 0);

        void Update()
        {
            if (State != shown) Apply();
        }

        void Apply()
        {
            shown = State;
            bool g = (shown & 1) != 0;
            bool planted = (shown & 2) != 0;
            if (maul != null) maul.localScale = g ? Vector3.zero : Vector3.one;
            if (gauntlet != null) gauntlet.localScale = g ? Vector3.one : Vector3.zero;
            if (shield != null) shield.localScale = planted ? Vector3.zero : Vector3.one;
        }
    }
}
