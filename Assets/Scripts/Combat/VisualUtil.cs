using UnityEngine;

namespace Anathema.Combat
{
    public static class VisualUtil
    {
        public static Material UnlitMaterial(Color color, int renderQueue = -1)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader) { color = color };
            if (renderQueue > 0) material.renderQueue = renderQueue;
            return material;
        }
    }
}
