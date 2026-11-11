using Anathema.Combat;
using UnityEngine;

namespace Anathema.Abilities.Mandrake
{
    // The Chain Hook thrown by Ambush's EX version: a barbed hook on a length of chain that flies straight out along the
    // aim, bites the first enemy it reaches (a wall or the end of its reach stops it), holds for a moment and is reeled
    // back in. Every machine flies its own copy along the same path; only the owner's (`real`) reports what it hit.
    public class ChainHook : MonoBehaviour
    {
        const float Height = 1.1f;
        const float HoldTime = 0.12f;
        const float ReelSpeedScale = 1.6f;

        static readonly RaycastHit[] Hits = new RaycastHit[16];
        static readonly Color ChainColor = new Color(0.16f, 0.18f, 0.17f, 1f);
        static readonly Color HookColor = new Color(0.32f, 0.36f, 0.33f, 1f);

        enum Phase { Out, Hold, Reel }

        Transform caster;
        Health casterHealth;
        Vector3 direction;
        float reach;
        float speed;
        float radius;
        bool real;
        System.Action<Health> onHit;

        Phase phase;
        float travelled;
        float holdEnd;
        Transform latched;
        Vector3 latchOffset;
        Vector3 tip;

        LineRenderer chain;
        LineRenderer glow;
        Transform hook;
        Material chainMaterial;
        Material glowMaterial;
        Material hookMaterial;

        // `onHitEnemy` runs (owner copy only) when the hook bites an enemy.
        public static ChainHook Launch(Transform caster, Health casterHealth, Vector3 direction, float reach, float speed,
                                       float radius, bool real, System.Action<Health> onHitEnemy)
        {
            var go = new GameObject("Chain Hook");
            var h = go.AddComponent<ChainHook>();
            h.caster = caster;
            h.casterHealth = casterHealth;
            h.direction = direction.normalized;
            h.reach = reach;
            h.speed = speed;
            h.radius = radius;
            h.real = real;
            h.onHit = onHitEnemy;
            h.tip = h.Origin;
            h.Build();
            return h;
        }

        Vector3 Origin => caster != null ? caster.position + Vector3.up * Height : tip;
        Vector3 Hand => Origin + direction * 0.35f;

        void Build()
        {
            chainMaterial = VisualUtil.UnlitMaterial(ChainColor, 3001);
            glowMaterial = VisualUtil.UnlitMaterial(new Color(MandrakeKit.Baleflame.r, MandrakeKit.Baleflame.g, MandrakeKit.Baleflame.b, 0.35f), 3000);
            hookMaterial = VisualUtil.UnlitMaterial(HookColor, 3002);

            chain = NewLine("Chain", chainMaterial, 0.07f);
            glow = NewLine("Chain Glow", glowMaterial, 0.18f);

            // The hook: a shank with two barbs swept back from the point, plus a baleflame-lit point.
            hook = new GameObject("Hook").transform;
            hook.SetParent(transform, false);
            AddPart(PrimitiveType.Cube, new Vector3(0f, 0f, -0.05f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.4f), hookMaterial);
            AddPart(PrimitiveType.Cube, new Vector3(0.1f, 0f, 0.02f), new Vector3(0f, -40f, 0f), new Vector3(0.05f, 0.05f, 0.3f), hookMaterial);
            AddPart(PrimitiveType.Cube, new Vector3(-0.1f, 0f, 0.02f), new Vector3(0f, 40f, 0f), new Vector3(0.05f, 0.05f, 0.3f), hookMaterial);
            AddPart(PrimitiveType.Sphere, new Vector3(0f, 0f, 0.16f), Vector3.zero, Vector3.one * 0.1f,
                    VisualUtil.UnlitMaterial(MandrakeKit.BaleflameBright, 3003), true);

            UpdateVisuals();
        }

        LineRenderer NewLine(string name, Material material, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.numCapVertices = 2;
            line.sharedMaterial = material;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        readonly System.Collections.Generic.List<Material> ownedPartMaterials = new System.Collections.Generic.List<Material>();

        void AddPart(PrimitiveType type, Vector3 position, Vector3 euler, Vector3 scale, Material material, bool ownsMaterial = false)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            Destroy(part.GetComponent<Collider>());
            var r = part.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            part.transform.SetParent(hook, false);
            part.transform.localPosition = position;
            part.transform.localEulerAngles = euler;
            part.transform.localScale = scale;
            if (ownsMaterial) ownedPartMaterials.Add(material);
        }

        void Update()
        {
            if (caster == null || casterHealth == null || casterHealth.IsDead)
            {
                Destroy(gameObject);
                return;
            }

            switch (phase)
            {
                case Phase.Out: FlyOut(); break;
                case Phase.Hold:
                    if (latched != null) tip = latched.position + latchOffset;
                    if (Time.time >= holdEnd) phase = Phase.Reel;
                    break;
                case Phase.Reel: ReelIn(); break;
            }
            UpdateVisuals();
        }

        void FlyOut()
        {
            Vector3 from = Origin + direction * travelled;
            float step = Mathf.Min(speed * Time.deltaTime, reach - travelled);

            if (FindHit(from, step, out RaycastHit hit, out Health target))
            {
                tip = from + direction * hit.distance;
                if (target != null)
                {
                    latched = target.transform;
                    latchOffset = tip - latched.position;
                    phase = Phase.Hold;
                    holdEnd = Time.time + HoldTime;
                    Sfx.Play(Sfx.MeleeImpact, 0.45f, 0.75f, 0.85f);
                    if (real) onHit?.Invoke(target);
                }
                else
                {
                    Sfx.Play(Sfx.Impact, 0.25f, 1.1f, 1.25f, 0.5f); // clanged off a wall or pillar
                    phase = Phase.Reel;
                }
                return;
            }

            travelled += step;
            tip = Origin + direction * travelled;
            if (travelled >= reach - 0.001f) phase = Phase.Reel;
        }

        void ReelIn()
        {
            Vector3 to = Hand - tip;
            float step = speed * ReelSpeedScale * Time.deltaTime;
            if (to.magnitude <= step)
            {
                Destroy(gameObject);
                return;
            }
            tip += to.normalized * step;
        }

        // Nearest hit along this frame's step: a living enemy (target set) or anything solid without Health (a wall).
        // The caster, its allies and the dead are passed through.
        bool FindHit(Vector3 from, float distance, out RaycastHit best, out Health target)
        {
            best = default;
            target = null;
            if (distance <= 0f) return false;

            int count = Physics.SphereCastNonAlloc(from, radius, direction, Hits, distance, ~0, QueryTriggerInteraction.Ignore);
            float bestDistance = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = Hits[i];
                Health health = h.collider.GetComponentInParent<Health>();
                if (health != null && (health == casterHealth || health.IsDead || health.Team == casterHealth.Team)) continue;
                if (h.distance >= bestDistance) continue;
                bestDistance = h.distance;
                best = h;
                target = health;
                found = true;
            }
            return found;
        }

        void UpdateVisuals()
        {
            Vector3 hand = Hand;
            chain.SetPosition(0, hand);
            chain.SetPosition(1, tip);
            glow.SetPosition(0, hand);
            glow.SetPosition(1, tip);

            Vector3 facing = phase == Phase.Reel ? tip - hand : direction;
            facing.y = 0f;
            hook.position = tip;
            if (facing.sqrMagnitude > 0.0001f) hook.rotation = Quaternion.LookRotation(facing.normalized);

            // Hidden with the rest of the caster on an enemy's screen while it is stealthed.
            bool show = !Stealthed.HiddenHere(caster.gameObject);
            chain.enabled = glow.enabled = show;
            hook.gameObject.SetActive(show);
        }

        void OnDestroy()
        {
            if (chainMaterial != null) Destroy(chainMaterial);
            if (glowMaterial != null) Destroy(glowMaterial);
            if (hookMaterial != null) Destroy(hookMaterial);
            foreach (Material m in ownedPartMaterials) if (m != null) Destroy(m);
        }
    }
}
