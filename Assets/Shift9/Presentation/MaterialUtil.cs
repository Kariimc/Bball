using UnityEngine;

namespace Shift9.Presentation
{
    /// <summary>Creates a simple solid-color material, working under URP or the built-in pipeline.</summary>
    public static class MaterialUtil
    {
        public static Material Solid(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }
}
