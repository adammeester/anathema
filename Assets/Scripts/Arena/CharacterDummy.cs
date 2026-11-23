using UnityEngine;

namespace Anathema.Arena
{
    // A training target that looks like a real champion: at start-up it copies the "Model" of a network player prefab
    // (Resources/Net/Player_<Key>) onto itself, stripped of every script, so it stands there in the champion's rest pose.
    // Health, health bar, collider and Respawner are set up by the scene builder like the capsule dummies, so it can be
    // hit, killed (leaving a Corpse, like a real fighter) and comes back after the Respawner's delay.
    public class CharacterDummy : MonoBehaviour
    {
        [SerializeField] string prefabPath = "Net/Player_Psyker";

        public void Configure(string path) => prefabPath = path;

        void Awake()
        {
            if (transform.Find("Model") != null) return;

            var prefab = Resources.Load<GameObject>(prefabPath);
            Transform source = prefab != null ? prefab.transform.Find("Model") : null;
            if (source == null)
            {
                Debug.LogWarning($"CharacterDummy: no Model under Resources/{prefabPath}.");
                return;
            }

            GameObject model = Instantiate(source.gameObject, transform);
            model.name = "Model";
            model.transform.localPosition = source.localPosition;
            model.transform.localRotation = source.localRotation;
            model.transform.localScale = source.localScale;

            // Visuals only: no gameplay scripts riding along on the copy.
            foreach (MonoBehaviour b in model.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(b);
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Net.CharacterSelectView.HideExtraGear(model);
        }
    }
}
