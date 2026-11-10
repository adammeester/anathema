using System.Collections.Generic;
using UnityEngine;

namespace Anathema.Net
{
    // Head-shot portraits for the in-match ally frames. Each champion is rendered once, on first request, with a throwaway
    // stage far from the arena (the same framing the character-select tiles use), then cached for the rest of the session.
    public static class AllyPortraits
    {
        const int Size = 128;
        static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();

        // `key` is the upgrade-catalog key ("GreyKnight", "KhorneBerserker", ...). Null when nothing could be rendered.
        public static Texture Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (cache.TryGetValue(key, out RenderTexture rt) && rt != null) return rt;

            rt = Render(key);
            cache[key] = rt;   // also caches a failure (null) so it is not retried every frame
            return rt;
        }

        static RenderTexture Render(string key)
        {
            string path = null;
            foreach (var info in CharacterSelectView.Roster)
                if (info.prefabPath != null && info.prefabPath.EndsWith("Player_" + key, System.StringComparison.Ordinal)) { path = info.prefabPath; break; }
            if (path == null) return null;

            var prefab = Resources.Load<GameObject>(path);
            Transform source = prefab != null ? prefab.transform.Find("Model") : null;
            if (source == null) return null;

            var stage = new GameObject("AllyPortraitStage");
            stage.transform.position = new Vector3(0f, -2500f, 0f);

            var camGo = new GameObject("Camera");
            camGo.transform.SetParent(stage.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;   // rendered by hand
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            cam.allowHDR = false;

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(stage.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;

            var model = Object.Instantiate(source.gameObject, stage.transform);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            CharacterSelectView.HideExtraGear(model);

            var ambientMode = RenderSettings.ambientMode;
            var ambientLight = RenderSettings.ambientLight;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            CharacterSelectView.RenderHeadShot(cam, model, rt);

            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            Object.Destroy(stage);
            return rt;
        }
    }
}
