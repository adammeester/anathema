using System;
using System.Collections.Generic;
using System.Reflection;
using Anathema.Abilities;
using Anathema.Abilities.GreyKnightInterceptor;
using Anathema.Abilities.KhorneBerserker;
using Anathema.Abilities.PlagueSurgeon;
using Anathema.Combat;
using UnityEngine;

namespace Anathema.Player
{
    // Loads this character's tuning values from Resources/Stats/<Character>.json when it spawns and writes them over
    // the values baked into the prefab/scene. Edit the JSON and press Play; no scene or prefab rebuild is needed.
    //
    //   {
    //     "health": 150, "moveSpeed": 6.5, "maxEnergy": 100,
    //     "abilities": { "Maim": { "damage": 8, "cooldown": 7 } }
    //   }
    //
    // "abilities" is keyed by the ability's class name; each property is the name of one of its fields (the same names
    // the inspector shows, camelCase). Numbers, true/false are supported. Unknown names log a warning. Anything not
    // listed keeps its prefab value. Online, every machine reads its own copy, so keep the files identical.
    // PlayerMovement adds this component on Awake, so existing prefabs pick it up without being rebuilt.
    [DefaultExecutionOrder(-200)]
    public class CharacterStats : MonoBehaviour
    {
        const string ResourcePath = "Stats/";
        const BindingFlags AnyField = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void EnsureOn(GameObject player)
        {
            if (player.GetComponent<CharacterStats>() == null) player.AddComponent<CharacterStats>();
        }

        // Which character this is, judged by the abilities it carries. Null if unknown (e.g. a bare test player).
        string Identify()
        {
            if (GetComponent<BerserkerCounter>() != null) return "KhorneBerserker";
            if (GetComponent<InterceptorCounter>() != null) return "GreyKnight";
            if (GetComponent<PlagueSpit>() != null) return "PlagueSurgeon";
            return null;
        }

        void Awake()
        {
            string key = Identify();
            if (key == null) return;

            var asset = Resources.Load<TextAsset>(ResourcePath + key);
            if (asset == null)
            {
                Debug.LogWarning($"CharacterStats: no Resources/{ResourcePath}{key}.json, using the built-in values.");
                return;
            }

            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(asset.text) as Dictionary<string, object>;
            }
            catch (FormatException e)
            {
                Debug.LogError($"CharacterStats: {key}.json is not valid. {e.Message}");
                return;
            }
            if (root == null)
            {
                Debug.LogError($"CharacterStats: {key}.json must contain a JSON object.");
                return;
            }

            Apply(key, root);
        }

        void Apply(string key, Dictionary<string, object> root)
        {
            foreach (var entry in root)
            {
                switch (entry.Key)
                {
                    case "health":
                        var health = GetComponent<Health>();
                        if (health != null && entry.Value is double hp) health.Configure((float)hp, health.Team);
                        else Warn(key, "health", "needs a number");
                        break;
                    case "moveSpeed":
                        SetField(key, GetComponent<PlayerMovement>(), "moveSpeed", entry.Value);
                        break;
                    case "maxEnergy":
                        SetField(key, GetComponent<Energy>(), "maxEnergy", entry.Value);
                        break;
                    case "abilities":
                        ApplyAbilities(key, entry.Value as Dictionary<string, object>);
                        break;
                    default:
                        Warn(key, entry.Key, "is not a known stat");
                        break;
                }
            }
        }

        void ApplyAbilities(string key, Dictionary<string, object> abilities)
        {
            if (abilities == null)
            {
                Warn(key, "abilities", "must be an object");
                return;
            }

            foreach (var entry in abilities)
            {
                Ability target = null;
                foreach (Ability a in GetComponents<Ability>())
                    if (a.GetType().Name == entry.Key) { target = a; break; }

                var values = entry.Value as Dictionary<string, object>;
                if (target == null || values == null)
                {
                    Warn(key, entry.Key, target == null ? "is not an ability on this character" : "must be an object");
                    continue;
                }

                foreach (var field in values)
                    SetField(key, target, field.Key, field.Value);
            }
        }

        // Writes a number/bool into a field of `target`, searching up the class hierarchy (so "cooldown" on Ability works).
        void SetField(string key, object target, string name, object value)
        {
            if (target == null)
            {
                Warn(key, name, "has no matching component");
                return;
            }

            for (Type t = target.GetType(); t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, AnyField);
                if (field == null) continue;

                if (field.FieldType == typeof(float) && value is double d) field.SetValue(target, (float)d);
                else if (field.FieldType == typeof(int) && value is double n) field.SetValue(target, (int)Math.Round(n));
                else if (field.FieldType == typeof(bool) && value is bool b) field.SetValue(target, b);
                else Warn(key, target.GetType().Name + "." + name, "has the wrong kind of value (floats, ints and bools only)");
                return;
            }
            Warn(key, target.GetType().Name + "." + name, "is not a field");
        }

        static void Warn(string key, string what, string problem)
        {
            Debug.LogWarning($"CharacterStats ({key}.json): '{what}' {problem}.");
        }
    }
}
