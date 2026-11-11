using Anathema.Combat;
using UnityEngine;

namespace Anathema.Arena
{
    // A practice target standing at the back of a ready room, there to hit while waiting for the next round. ReadyRoom
    // builds them and shows them only during prep.
    //
    // A dummy is a plain local object (no NetPlayer), so every machine has its own copy and is the authority for it:
    // you see your own hits land on your copy, and other players' attacks only replay their visuals on it. Hitting a
    // dummy never charges energy (Energy.Gain ignores everything during prep). It refills a moment after it falls.
    [RequireComponent(typeof(Health))]
    public class TrainingDummy : MonoBehaviour
    {
        // Matches no player's team (those start at 1), so everyone's attacks treat it as an enemy.
        public const int Team = -1;
        const float MaxHealth = 200f;
        const float ReviveDelay = 2f;

        static Material material;

        Health health;
        Vector3 home;
        Quaternion homeRotation;
        float reviveAt = -1f;

        public static TrainingDummy Create(Transform parent, Vector3 worldPosition, Quaternion rotation)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Training Dummy";
            go.transform.SetPositionAndRotation(worldPosition, rotation);
            go.transform.SetParent(parent, true);

            Renderer renderer = go.GetComponent<Renderer>();
            if (material == null)
            {
                material = new Material(renderer.sharedMaterial) { name = "Ready Room Dummy" };
                material.color = new Color(0.7f, 0.15f, 0.15f);
            }
            renderer.sharedMaterial = material;

            go.AddComponent<Health>().Configure(MaxHealth, Team);
            go.AddComponent<HealthBar>().Configure(1.7f);
            return go.AddComponent<TrainingDummy>();
        }

        void Awake()
        {
            health = GetComponent<Health>();
            home = transform.position;
            homeRotation = transform.rotation;
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(Health _, GameObject killer)
        {
            SetVisible(false);
            reviveAt = Time.time + ReviveDelay;
        }

        void Update()
        {
            if (reviveAt >= 0f && Time.time >= reviveAt) ResetDummy();
        }

        // Back on its spot at full health with nothing left on it (bleeds, burns, stuns, knockback...).
        public void ResetDummy()
        {
            reviveAt = -1f;
            foreach (MonoBehaviour b in GetComponents<MonoBehaviour>())
                if (!(b is Health || b is HealthBar || b is TrainingDummy)) Destroy(b);
            transform.SetPositionAndRotation(home, homeRotation);
            health.ResetHealth();
            SetVisible(true);
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = visible;
        }
    }
}
