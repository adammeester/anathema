using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Abilities.KhorneBerserker
{
    // Shows the weapon the Berserker currently wields. The model (Art/khorne_berserker.py) is one skinned mesh that
    // carries every weapon, each weighted to its own bone: Axe_L + Axe_R (the dual chainaxes), Heavy_Chainaxe and
    // Eviscerator. The weapons that are not wielded are hidden by scaling their bones to zero, which collapses the
    // geometry weighted to them. Follows UpgradeState's synced mask, so it is right on every machine.
    // Added by UpgradeState at runtime.
    public class BerserkerWeaponVisual : MonoBehaviour
    {
        Transform axeL, axeR, heavy, evisc;
        BerserkerWeapon shown = (BerserkerWeapon)(-1);

        void Awake()
        {
            Transform model = transform.Find("Model");
            if (model == null) { enabled = false; return; }
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                switch (t.name)
                {
                    case "Axe_L": if (axeL == null) axeL = t; break;
                    case "Axe_R": if (axeR == null) axeR = t; break;
                    case "Heavy_Chainaxe": if (heavy == null) heavy = t; break;
                    case "Eviscerator": if (evisc == null) evisc = t; break;
                }
            }
            Apply(BerserkerWeapons.Of(gameObject));
        }

        void Update()
        {
            BerserkerWeapon w = BerserkerWeapons.Of(gameObject);
            if (w != shown) Apply(w);
        }

        void Apply(BerserkerWeapon w)
        {
            shown = w;
            Show(axeL, w == BerserkerWeapon.DualChainaxes);
            Show(axeR, w == BerserkerWeapon.DualChainaxes);
            Show(heavy, w == BerserkerWeapon.HeavyChainaxe);
            Show(evisc, w == BerserkerWeapon.Eviscerator);
        }

        static void Show(Transform bone, bool visible)
        {
            if (bone != null) bone.localScale = visible ? Vector3.one : Vector3.zero;
        }
    }
}
