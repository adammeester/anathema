using Anathema.Abilities;
using Anathema.Arena;
using Anathema.Combat;
using Anathema.Upgrades;
using UnityEngine;

namespace Anathema.Net
{
    // Offline stand-in for the online prep phase, so the prep area and the upgrade pick can be tried in the training
    // arena without hosting a match. Start it from the upgrade test panel (F1 -> "Restart round into prep", or F2).
    //
    // It follows the same flow as NetGame's Prep phase: you are put back at your spawn at full health, confined to the
    // barrier ring, unable to fight and invulnerable (NetGame.InPrep is true, so the leash, the ability block and the
    // gas hold all apply), the upgrade menu opens, then picks lock (idle = random non-weapon pick), a 3s countdown
    // runs and the "round" begins. Training dummies keep behaving normally.
    public class OfflinePrep : MonoBehaviour
    {
        public static bool Active { get; private set; }

        UpgradeState state;
        Health health;
        Respawner respawner;
        Energy energy;
        CharacterController controller;
        PrepArea area;
        readonly UpgradePickerView picker = new UpgradePickerView();
        Vector3 fallbackSpawn;
        Quaternion fallbackRotation;
        float startTime;
        float goTime;
        bool locked;
        int round;
        GUIStyle big, small;

        public static void Begin(GameObject player)
        {
            if (NetGame.Online) return;
            if (!player.TryGetComponent(out OfflinePrep prep)) prep = player.AddComponent<OfflinePrep>();
            prep.Enter();
        }

        void Awake()
        {
            state = GetComponent<UpgradeState>();
            health = GetComponent<Health>();
            respawner = GetComponent<Respawner>();
            energy = GetComponent<Energy>();
            controller = GetComponent<CharacterController>();
            fallbackSpawn = transform.position;
            fallbackRotation = transform.rotation;
        }

        void Enter()
        {
            if (Active) Leave();

            Active = true;
            locked = false;
            startTime = Time.time;
            round++;

            foreach (Ability a in GetComponents<Ability>()) a.ReplayCancel();
            Teleport();
            if (energy != null) energy.Clear();
            health.Invulnerable = true;

            Vector3 spawn = respawner != null ? respawner.SpawnPosition : fallbackSpawn;
            area = PrepArea.Show(spawn);

            GasZone gas = FindFirstObjectByType<GasZone>();
            if (gas != null) gas.Restart();
            HealingOrb.ClearAll();

            if (state != null) state.BeginPick(round);
        }

        // Back to the spawn point at full health (the CharacterController would otherwise override a teleport).
        void Teleport()
        {
            if (controller != null) controller.enabled = false;
            if (respawner != null) respawner.ResetNow();
            else
            {
                transform.SetPositionAndRotation(fallbackSpawn, fallbackRotation);
                health.ResetHealth();
            }
            if (controller != null) controller.enabled = true;
        }

        void Leave()
        {
            Active = false;
            if (area != null) Destroy(area.gameObject);
            if (health != null) health.Invulnerable = false;
        }

        // The "round" starts: barrier down, fighting allowed again.
        void StartRound()
        {
            Leave();
            Teleport();
            GasZone gas = FindFirstObjectByType<GasZone>();
            if (gas != null) gas.Restart();
        }

        void Update()
        {
            if (!Active) return;

            if (!locked)
            {
                picker.HandleKeys(state);
                bool chosen = state == null || !state.Pending;
                if ((chosen && Time.time >= startTime + NetGame.PrepMin) || Time.time >= startTime + NetGame.PrepWindow)
                {
                    if (state != null) state.AutoPickIfPending();
                    locked = true;
                    goTime = Time.time + NetGame.PrepCountdown;
                }
            }
            else if (Time.time >= goTime)
            {
                StartRound();
            }
        }

        void OnDestroy()
        {
            if (Active) Leave();
        }

        void OnGUI()
        {
            if (!Active) return;

            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float w = Screen.width / scale, h = 1080f;

            if (big == null)
            {
                big = new GUIStyle(GUI.skin.label) { fontSize = 120, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                big.normal.textColor = new Color(0.86f, 0.70f, 0.30f);
                small = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
                small.normal.textColor = new Color(0.8f, 0.78f, 0.72f);
            }

            if (locked)
            {
                int go = Mathf.Max(1, Mathf.CeilToInt(goTime - Time.time));
                GUI.Label(new Rect(0, h * 0.28f, w, 50), $"ROUND BEGINS IN", small);
                GUI.Label(new Rect(0, h * 0.28f + 40, w, 170), go.ToString(), big);
            }
            else if (state != null && state.Pending)
            {
                picker.Draw(w, h, state, round + 1, startTime + NetGame.PrepWindow - Time.time);
            }
            else
            {
                GUI.Label(new Rect(0, h * 0.14f, w, 50), "PREP  -  walk about inside the ring (you can't leave it or fight)", small);
            }

            GUI.matrix = Matrix4x4.identity;
        }
    }
}
