using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // Q: the Mandrake steps into one shadow and out of another at the cursor (pulled in to max range; it passes over
    // walls but never lands inside one). Arriving charges its blade: the next strike within a few seconds (Glimmersteel, Balefire
    // or Shadow Rend) deals bonus damage. Jumping doesn't break stealth, and while hidden from a viewer it leaves no flash there.
    // EX (Shift+Q, costs energy): Umbral Return. The jump is the same, but it leaves a shadow mark where the Mandrake
    // stood. Pressing Q again within a few seconds snaps it back to the mark (if it is still within jump range). The
    // return doesn't touch the cooldown, doesn't break stealth and doesn't charge the blade again. C, death, or the
    // window running out lets the mark fade.
    public class ShadowJump : Ability
    {
        [SerializeField] float range = 7f;
        [SerializeField] float bonusDamage = 10f;
        [SerializeField] float bonusWindow = 3f;

        [Header("EX: Umbral Return")]
        [SerializeField] float exEnergyCost = 35f;
        [SerializeField] float returnWindow = 3f;

        CharacterController controller;
        Energy energy;
        float bonusUntil = float.NegativeInfinity;
        GameObject aura;
        Material auraMaterial;

        // The Umbral Return mark: where the Mandrake will snap back to, and until when. Kept on every machine (a
        // replica records it from the EX cue) so the mark and the return flash are drawn everywhere.
        bool markActive;
        Vector3 markPosition;
        float markUntil;
        GameObject markRing;
        Material markMaterial;
        bool returnedThisFrame;

        public override string DisplayName => "Shadow Jump";
        public override string KeyLabel => "Q";
        public override float ExCost => exEnergyCost;
        public override string ExName => "Umbral Return";
        public override string ExDescription => "Jump back within " + returnWindow.ToString("0.#") + "s";

        // The EX may move the Mandrake twice (out and back), so the server allows twice the range, for the whole window.
        public override float MovementRange => ExActivation ? range * 2f : range;
        public override float MovementWindow => ExActivation ? returnWindow + 0.5f : base.MovementWindow;

        // Online: the owner publishes the charge so every machine shows the glowing blade aura.
        public override bool ShowsEmpowered => Time.time < bonusUntil;

        public override void ShowReplicaEmpowered(bool on)
        {
            if (!IsReplica) return;
            bonusUntil = on ? float.PositiveInfinity : float.NegativeInfinity;
        }

        void Reset()
        {
            cooldown = 9f;
        }

        protected override void Awake()
        {
            base.Awake();
            controller = GetComponent<CharacterController>();
            energy = GetComponent<Energy>();

            aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = "Shadow Charge Aura";
            Destroy(aura.GetComponent<Collider>());
            aura.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            auraMaterial = VisualUtil.UnlitMaterial(MandrakeKit.Baleflame, 3001);
            aura.GetComponent<Renderer>().sharedMaterial = auraMaterial;
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0.16f, 0f);
            aura.transform.localScale = new Vector3(1.9f, 0.01f, 1.9f);
            aura.SetActive(false);

            // The return mark sits on the ground in the world, not on the Mandrake.
            markRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            markRing.name = "Umbral Return Mark";
            Destroy(markRing.GetComponent<Collider>());
            markRing.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markMaterial = VisualUtil.UnlitMaterial(MandrakeKit.Shadow, 3001);
            markRing.GetComponent<Renderer>().sharedMaterial = markMaterial;
            markRing.transform.localScale = new Vector3(1.4f, 0.01f, 1.4f);
            markRing.SetActive(false);
        }

        void OnDestroy()
        {
            if (auraMaterial != null) Destroy(auraMaterial);
            if (markMaterial != null) Destroy(markMaterial);
            if (markRing != null) Destroy(markRing);
        }

        // Owner: the bonus damage for the strike being thrown now (spends the charge), or 0.
        public float ConsumeBonus()
        {
            if (Time.time >= bonusUntil) return 0f;
            bonusUntil = float.NegativeInfinity;
            return bonusDamage;
        }

        // While a return mark is waiting, Q is the return, never a fresh jump.
        protected override bool IsTriggered() => Input.GetKeyDown(KeyCode.Q) && !markActive && !returnedThisFrame;

        protected override void Activate()
        {
            // A replica gets the return as a second EX cue while its copy of the mark is still up.
            if (IsReplica && ExActivation && markActive)
            {
                DrawReturn();
                return;
            }

            if (!IsReplica) ExActivation = TrySpendEx(energy, exEnergyCost);

            Vector3 from = transform.position;
            Vector3 dir = Aim.AimPoint - from;
            dir.y = 0f;
            float distance = Mathf.Min(range, dir.magnitude);
            dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;
            distance = LandingSpot.Clear(transform, from, dir, distance);
            Vector3 to = from + dir * distance;

            if (!IsReplica && distance >= 0.5f) MoveTo(to);

            if (!Stealthed.HiddenHere(gameObject) && distance >= 0.5f) AbilityFx.Teleport(from, to, MandrakeKit.Baleflame);
            if (!IsReplica) bonusUntil = Time.time + bonusWindow;

            if (ExActivation) SetMark(from);
        }

        void SetMark(Vector3 at)
        {
            markActive = true;
            markPosition = at;
            markUntil = Time.time + returnWindow;
            markRing.transform.position = new Vector3(at.x, at.y + 0.14f, at.z);
        }

        void ClearMark()
        {
            markActive = false;
            if (markRing != null && markRing.activeSelf) markRing.SetActive(false);
        }

        void MoveTo(Vector3 position)
        {
            if (controller != null) controller.enabled = false;
            transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        // Owner: snap back to the mark, if nothing stops it and it is still within reach.
        void TryReturn()
        {
            if (IsDisabled || Rooted.IsRooted(gameObject) || Anathema.Net.NetGame.Frozen) return;
            Vector3 flat = markPosition - transform.position;
            flat.y = 0f;
            if (flat.magnitude > range) return; // walked too far away: the mark stays until it fades

            DrawReturn();
            returnedThisFrame = true;

            // Replicas (and the server, which grants the move) replay it as a second EX cue.
            if (Health.Net != null && Health.Net.IsSpawned)
            {
                int index = System.Array.IndexOf(GetComponents<Ability>(), this);
                Health.Net.SendCue(index + ExCueOffset, Aim.AimPoint);
            }
        }

        // Every machine: the return flash; the owner also moves. Clears the mark.
        void DrawReturn()
        {
            Vector3 from = transform.position;
            Vector3 to = markPosition;
            ClearMark();
            if (!IsReplica) MoveTo(to);
            if (!Stealthed.HiddenHere(gameObject)) AbilityFx.Teleport(from, to, MandrakeKit.Shadow);
        }

        protected override void Update()
        {
            returnedThisFrame = false;
            if (!IsReplica && markActive && Input.GetKeyDown(KeyCode.Q) && !Health.IsDead) TryReturn();

            base.Update();

            if (markActive && (Time.time >= markUntil || Health.IsDead)) ClearMark();
            bool showMark = markActive && !Stealthed.HiddenHere(gameObject);
            if (markRing.activeSelf != showMark) markRing.SetActive(showMark);
            if (showMark)
            {
                // Fades out over the last second so the player can see the window closing.
                float left = Mathf.Clamp01(markUntil - Time.time);
                Color c = MandrakeKit.Baleflame;
                c.a *= (0.35f + 0.25f * Mathf.Sin(Time.time * 8f)) * left;
                markMaterial.color = c;
            }

            bool on = ShowsEmpowered && !Health.IsDead;
            if (aura.activeSelf != on) aura.SetActive(on);
            if (on)
            {
                Color c = MandrakeKit.Baleflame;
                c.a *= 0.45f + 0.35f * Mathf.Sin(Time.time * 12f);
                auraMaterial.color = c;
            }
        }

        // C or death: the mark fades.
        protected override void Cancel()
        {
            ClearMark();
        }
    }
}
