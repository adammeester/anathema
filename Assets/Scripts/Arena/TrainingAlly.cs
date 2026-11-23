using Anathema.Combat;
using Anathema.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Anathema.Arena
{
    // A friendly champion standing in the offline training arena, on the player's team (1), for trying heals, barriers
    // and other ally-targeted abilities. Added at runtime to any offline scene that has champion training dummies (the
    // scenes ArenaSceneBuilder makes), so existing training scenes get one without a rebuild.
    //
    // It keeps itself wounded: it starts with a full bar of healable (white) health missing, and whenever that has all
    // been healed or has decayed away it is put back to the same wound a moment later, so heals can be tried over and
    // over. The wound bypasses shields, so a barrier you give it stays up until something actually hits it.
    [RequireComponent(typeof(Health))]
    public class TrainingAlly : MonoBehaviour
    {
        const int Team = 1;
        const string PrefabPath = "Net/Player_SanguinaryPriest";
        const float MaxHealth = 170f;
        const float Height = 2f;
        const float Radius = 0.5f;
        const float BarHeight = 2.7f;
        static readonly Vector3 Spot = new Vector3(-5f, 0f, -3f);

        const float RewoundDelay = 1.5f; // pause at full (or fully decayed) before the next wound, to see the bar fill

        Health health;
        float rewoundAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TrySpawn();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TrySpawn();

        static void TrySpawn()
        {
            if (NetGame.Online) return;
            if (FindAnyObjectByType<CharacterDummy>() == null) return; // not a training arena
            if (FindAnyObjectByType<TrainingEliminations>() == null)
                new GameObject("Training Eliminations").AddComponent<TrainingEliminations>();
            if (FindAnyObjectByType<TrainingAlly>() != null) return;

            var go = new GameObject("Training Ally (Sanguinary Priest)");
            Vector3 toCentre = new Vector3(-Spot.x, 0f, -Spot.z);
            go.transform.SetPositionAndRotation(Spot, Quaternion.LookRotation(toCentre));

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = Height;
            capsule.radius = Radius;
            capsule.center = new Vector3(0f, Height * 0.5f, 0f);

            go.AddComponent<CharacterDummy>().Configure(PrefabPath);
            go.AddComponent<Health>().Configure(MaxHealth, Team);
            go.AddComponent<HealthBar>().Configure(BarHeight);
            go.AddComponent<Respawner>().Configure(4f);
            go.AddComponent<TrainingAlly>();
        }

        void Awake() => health = GetComponent<Health>();

        void Start() => Wound();

        void Update()
        {
            if (health.IsDead)
            {
                rewoundAt = -1f;
                return;
            }

            // Back from the dead at full health, or nothing left to heal: wound it again shortly.
            if (health.Recoverable <= 0.01f)
            {
                if (rewoundAt < 0f) rewoundAt = Time.time + RewoundDelay;
                else if (Time.time >= rewoundAt) Wound();
            }
            else rewoundAt = -1f;
        }

        void Wound()
        {
            rewoundAt = -1f;
            health.TrainingWound(health.MaxRecoverable);
        }
    }
}
