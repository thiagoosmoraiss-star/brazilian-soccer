using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>One shared material per colour (TECHNICAL_SPEC §18: "materiais compartilhados"). The template is the
    /// generated <see cref="TemplateResource"/> material in player builds (it keeps the lit shader in the build, X-62)
    /// and the render pipeline's default material in the editor. Created at set-up, never per frame.</summary>
    public sealed class MaterialCache
    {
        public const string TemplateResource = "AcessoRuntimeMaterial";

        private readonly Material _template;
        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();

        public MaterialCache()
        {
            _template = Resources.Load<Material>(TemplateResource);
            if (_template != null) return;
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _template = probe.GetComponent<Renderer>().sharedMaterial;
            Object.Destroy(probe);
        }

        public Material Get(Color color)
        {
            if (_byColor.TryGetValue(color, out var m)) return m;
            m = new Material(_template) { color = color, name = "Shared " + ColorUtility.ToHtmlStringRGB(color) };
            _byColor.Add(color, m);
            return m;
        }

        /// <summary>A primitive without collider, parented, with a shared material.</summary>
        public GameObject Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Color color, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = Get(color);
            return go;
        }
    }
}
