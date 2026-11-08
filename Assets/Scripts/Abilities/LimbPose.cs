using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Abilities
{
    // Procedural posing of a rigged model's bones (the models export with a skeleton but no animation clips).
    // Bones are looked up by name under `modelRoot`, remembered at their rest rotation, and rotated about
    // world-space axes so the rig's bone roll does not matter. Call Reset() then Rotate() in hierarchy order
    // (parent bones first) each frame the pose is active, and Reset() once when it ends.
    public class LimbPose
    {
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        readonly Dictionary<string, Quaternion> rest = new Dictionary<string, Quaternion>();
        readonly Dictionary<string, Vector3> restPosition = new Dictionary<string, Vector3>();

        public LimbPose(Transform modelRoot, params string[] boneNames)
        {
            if (modelRoot == null) return;

            // Depth-first, so the first match is the real skeleton even if a copy of the model is added later.
            foreach (Transform t in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                foreach (string name in boneNames)
                {
                    if (t.name != name || bones.ContainsKey(name)) continue;
                    bones[name] = t;
                    rest[name] = t.localRotation;
                    restPosition[name] = t.localPosition;
                }
            }
        }

        public bool Has(string bone)
        {
            return bones.ContainsKey(bone);
        }

        public void Reset()
        {
            foreach (var pair in bones)
            {
                pair.Value.localRotation = rest[pair.Key];
                pair.Value.localPosition = restPosition[pair.Key];
            }
        }

        // The bone's transform, or null when the model does not have it.
        public Transform Get(string bone)
        {
            return bones.TryGetValue(bone, out Transform t) ? t : null;
        }

        // Rotates `bone` about its own joint by `degrees` around a world-space axis. Missing bones are ignored.
        public void Rotate(string bone, Vector3 worldAxis, float degrees)
        {
            if (!bones.TryGetValue(bone, out Transform t)) return;
            t.rotation = Quaternion.AngleAxis(degrees, worldAxis) * t.rotation;
        }
    }
}
