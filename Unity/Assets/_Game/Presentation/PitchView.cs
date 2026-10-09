using Game.Match;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>The pitch drawn from the simulation's <see cref="Pitch"/>: grass, white lines (touchlines, goal lines,
    /// halfway, penalty areas) and both goals. Placeholder art (D-02 pending), shared by the match scene (A7b) and the dev
    /// sandboxes.</summary>
    public static class PitchView
    {
        private const float LineWidth = 0.12f; // rendering only

        public static Transform Build(Pitch p, string name)
        {
            var root = new GameObject(name).transform;

            var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
            grass.name = "Grass";
            grass.transform.SetParent(root);
            grass.transform.localScale = new Vector3((p.Length + 10f) / 10f, 1f, (p.Width + 10f) / 10f); // Unity plane = 10×10 units
            grass.GetComponent<Renderer>().material.color = new Color(0.2f, 0.55f, 0.2f);
            UnityEngine.Object.Destroy(grass.GetComponent<Collider>());

            float hl = p.HalfLength, hw = p.HalfWidth;
            Line(root, -hl, -hw, hl, -hw);
            Line(root, -hl, hw, hl, hw);
            Line(root, -hl, -hw, -hl, hw);
            Line(root, hl, -hw, hl, hw);
            Line(root, 0f, -hw, 0f, hw);
            for (int side = 0; side < 2; side++)
            {
                float sign = side == 0 ? 1f : -1f;
                float goalX = sign * hl, boxX = sign * (hl - p.PenaltyAreaDepth), bw = p.PenaltyAreaWidth * 0.5f;
                Line(root, boxX, -bw, boxX, bw);
                Line(root, boxX, -bw, goalX, -bw);
                Line(root, boxX, bw, goalX, bw);
                Bar(root, new Vector3(goalX, p.GoalHeight * 0.5f, -p.HalfGoalWidth), new Vector3(p.PostRadius * 2f, p.GoalHeight * 0.5f, p.PostRadius * 2f), false);
                Bar(root, new Vector3(goalX, p.GoalHeight * 0.5f, p.HalfGoalWidth), new Vector3(p.PostRadius * 2f, p.GoalHeight * 0.5f, p.PostRadius * 2f), false);
                Bar(root, new Vector3(goalX, p.GoalHeight, 0f), new Vector3(p.PostRadius * 2f, p.HalfGoalWidth, p.PostRadius * 2f), true);
            }
            return root;
        }

        // Simulation (x, y) on the ground → a thin white strip in Unity (x, z).
        private static void Line(Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Line";
            go.transform.SetParent(parent);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = new Vector3((x0 + x1) * 0.5f, 0.01f, (y0 + y1) * 0.5f);
            go.transform.localScale = new Vector3(Mathf.Max(LineWidth, Mathf.Abs(x1 - x0)), 0.01f, Mathf.Max(LineWidth, Mathf.Abs(y1 - y0)));
            go.GetComponent<Renderer>().material.color = Color.white;
        }

        private static void Bar(Transform parent, Vector3 center, Vector3 cylinderScale, bool crossbar)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // unit cylinder: height 2 along Y
            go.name = crossbar ? "Crossbar" : "Post";
            go.transform.SetParent(parent);
            UnityEngine.Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = center;
            if (crossbar) go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // lay it along Unity z (= simulation y)
            go.transform.localScale = cylinderScale;
            go.GetComponent<Renderer>().material.color = Color.white;
        }
    }
}
