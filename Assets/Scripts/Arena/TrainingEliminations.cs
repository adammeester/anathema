using System.Collections.Generic;
using Anathema.Combat;
using Anathema.Net;
using Anathema.Player;
using UnityEngine;

namespace Anathema.Arena
{
    // Offline training arena stand-in for the online "X ELIMINATED" banner: shows it when you, the training ally or a
    // champion-shaped enemy dummy goes down (the plain capsule dummies die too often to be worth a banner).
    // TrainingAlly creates it alongside the ally.
    public class TrainingEliminations : MonoBehaviour
    {
        const float RescanInterval = 0.5f; // fighters can appear after this does (the ally, a rebuilt player)

        readonly HashSet<Health> watched = new HashSet<Health>();
        float nextScan;
        string bannerName = "";
        bool bannerFriendly;
        float bannerAt = -999f;

        void Update()
        {
            if (Time.time < nextScan) return;
            nextScan = Time.time + RescanInterval;
            foreach (Health h in FindObjectsByType<Health>())
            {
                if (watched.Contains(h) || !IsFighter(h)) continue;
                watched.Add(h);
                h.Died += OnDied;
            }
        }

        void OnDestroy()
        {
            foreach (Health h in watched)
                if (h != null) h.Died -= OnDied;
        }

        static bool IsFighter(Health h) =>
            h.TryGetComponent(out MouseAim _) || h.TryGetComponent(out CharacterDummy _);

        void OnDied(Health victim, GameObject killer)
        {
            if (NetGame.Online) return;
            Health me = LocalPlayer();
            int myTeam = me != null ? me.Team : 1;
            bannerFriendly = victim.Team == myTeam;
            bannerName = victim == me ? "YOU WERE"
                : (bannerFriendly ? "TEAMMATE " : "ENEMY ") + DisplayName(victim.gameObject).ToUpperInvariant();
            bannerAt = Time.time;
        }

        Health LocalPlayer()
        {
            foreach (Health h in watched)
                if (h != null && h.TryGetComponent(out MouseAim _)) return h;
            return null;
        }

        // "Training Dummy (Psyker)" -> "Psyker"; anything else by its object name.
        static string DisplayName(GameObject go)
        {
            string n = go.name;
            int open = n.IndexOf('('), close = n.LastIndexOf(')');
            return open >= 0 && close > open + 1 ? n.Substring(open + 1, close - open - 1) : n;
        }

        void OnGUI()
        {
            float t = Time.time - bannerAt;
            if (t > EliminationBanner.Duration) return;
            float scale = Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            EliminationBanner.Draw(Screen.width / scale, 1080f, t, bannerName, bannerFriendly);
            GUI.matrix = Matrix4x4.identity;
        }
    }
}
