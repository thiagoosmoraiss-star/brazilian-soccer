using System.Collections.Generic;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>One shared material per colour (TECHNICAL_SPEC §18: "materiais compartilhados"), cloned from the render
    /// pipeline's default material so it works on Built-in and URP alike. Created at set-up, never per frame.</summary>
    public sealed class MaterialCache
    {
        private readonly Material _template;
        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();

        public MaterialCache()
        {
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
