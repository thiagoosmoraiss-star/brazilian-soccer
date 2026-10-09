using Game.Data.Presentation;
using Game.Match.AI;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Placeholder low-poly player (TECHNICAL_SPEC §19: "low-poly placeholder: parado, correr, sprint, chute, passe";
    /// D-02 pending): torso, head, arms and legs from primitives, animated procedurally from the simulation — legs and
    /// arms swing with the distance run (wider when sprinting), a kicking leg swings through on a pass or shot, the
    /// keeper lies along his dive and raises his arms with the ball. Reads state only (CLAUDE.md §5.2); no allocation
    /// per frame.
    /// </summary>
    public sealed class PlayerView
    {
        // Rig proportions (placeholder art, not tuning): a ~1.8 m figure.
        private const float HipHeight = 0.9f, ShoulderHeight = 1.45f, LegLength = 0.88f, ArmLength = 0.62f;

        public readonly Transform Root;
        private readonly Transform _body, _legL, _legR, _armL, _armR;
        private readonly Renderer _torso;
        private readonly Material _shirt, _highlight;
        private readonly GameObject _marker;
        private float _phase;
        private bool _highlighted;

        public PlayerView(MaterialCache materials, Color shirt, Color shorts, Color skin, string name)
        {
            Root = new GameObject(name).transform;
            _body = new GameObject("Body").transform;
            _body.SetParent(Root, false);
            _shirt = materials.Get(shirt);
            _highlight = materials.Get(Color.yellow);
            _torso = materials.Part(PrimitiveType.Capsule, _body, new Vector3(0f, 1.2f, 0f), new Vector3(0.5f, 0.32f, 0.32f), shirt, "Torso").GetComponent<Renderer>();
            materials.Part(PrimitiveType.Sphere, _body, new Vector3(0f, 1.68f, 0f), new Vector3(0.24f, 0.26f, 0.24f), skin, "Head");
            _legL = Limb(materials, new Vector3(-0.12f, HipHeight, 0f), LegLength, 0.15f, shorts, "LegL");
            _legR = Limb(materials, new Vector3(0.12f, HipHeight, 0f), LegLength, 0.15f, shorts, "LegR");
            _armL = Limb(materials, new Vector3(-0.32f, ShoulderHeight, 0f), ArmLength, 0.1f, shirt, "ArmL");
            _armR = Limb(materials, new Vector3(0.32f, ShoulderHeight, 0f), ArmLength, 0.1f, shirt, "ArmR");
            _marker = materials.Part(PrimitiveType.Cylinder, Root, new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.01f, 1.4f), Color.yellow, "Controlled marker");
            _marker.SetActive(false);
        }

        private Transform Limb(MaterialCache materials, Vector3 pivot, float length, float thickness, Color color, string name)
        {
            var joint = new GameObject(name).transform;
            joint.SetParent(_body, false);
            joint.localPosition = pivot;
            materials.Part(PrimitiveType.Cube, joint, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length, thickness), color, name + " mesh");
            return joint;
        }

        /// <summary>Marks the user's player (yellow shirt and a disc at his feet).</summary>
        public void SetHighlighted(bool on)
        {
            if (on == _highlighted) return;
            _highlighted = on;
            _torso.sharedMaterial = on ? _highlight : _shirt;
            _marker.SetActive(on);
        }

        /// <summary>Places and animates the player for this frame.</summary>
        /// <param name="ground">Interpolated position on the ground (Unity space).</param>
        /// <param name="facing">Facing on the ground (Unity space, need not be normalized).</param>
        /// <param name="speed">Ground speed (m/s).</param>
        public void Update(Vector3 ground, Vector3 facing, float speed, bool sprinting, float secondsSinceKick, KeeperState? keeper,
            MatchViewDefinition v, float dt)
        {
            Root.position = ground;
            if (facing.sqrMagnitude > 1e-4f) Root.rotation = Quaternion.LookRotation(facing, Vector3.up);

            // Keeper on the ground: the whole body lies along the dive (sideways), arms stretched.
            bool down = keeper == KeeperState.Diving || keeper == KeeperState.Grounded;
            _body.localRotation = down ? Quaternion.Euler(0f, 0f, 80f) : Quaternion.identity;
            _body.localPosition = down ? new Vector3(0.9f, 0.25f, 0f) : Vector3.zero;

            float swing = 0f;
            if (speed > v.MoveSpeedThreshold)
            {
                _phase += speed * dt / v.StrideMeters * 2f * Mathf.PI;
                if (_phase > 2f * Mathf.PI) _phase -= 2f * Mathf.PI;
                swing = Mathf.Sin(_phase) * (sprinting ? v.SprintLegSwingDegrees : v.LegSwingDegrees);
            }
            else _phase = 0f;

            float kick = secondsSinceKick < v.KickSeconds ? Mathf.Sin(Mathf.PI * secondsSinceKick / v.KickSeconds) * v.KickSwingDegrees : 0f;
            _legL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _legR.localRotation = Quaternion.Euler(kick > 0f ? -kick : -swing, 0f, 0f);

            if (down || keeper == KeeperState.Holding)
            {
                _armL.localRotation = Quaternion.Euler(0f, 0f, -170f);
                _armR.localRotation = Quaternion.Euler(0f, 0f, 170f);
            }
            else
            {
                float arm = swing * v.ArmSwingFraction;
                _armL.localRotation = Quaternion.Euler(-arm, 0f, 0f);
                _armR.localRotation = Quaternion.Euler(arm, 0f, 0f);
            }
        }
    }
}
