using System;

namespace Game.Match
{
    public enum ActionKind
    {
        None = 0,
        Pass = 1,
        Through = 2,
        Shot = 3,
    }

    /// <summary>One action released by the player. <see cref="Power"/> is 0-1 from the hold time, or
    /// <see cref="AutoPower"/> when a pass/through ball was only tapped (GAME_DESIGN §19: "toque = o jogo resolve a força").</summary>
    public readonly struct ActionCommand
    {
        public const float AutoPower = -1f;
        public static readonly ActionCommand None = new ActionCommand(ActionKind.None, 0f);

        public readonly ActionKind Kind;
        public readonly float Power;

        public ActionCommand(ActionKind kind, float power)
        {
            Kind = kind;
            Power = power;
        }

        public bool IsAuto => Power < 0f;
    }

    /// <summary>Button timings, taken from Data/Balance/kicking.json by the caller (the Input assembly cannot read Data).</summary>
    public readonly struct KickTimings
    {
        public readonly float TapMaxSeconds;
        public readonly float PassBarSeconds;
        public readonly float ShotBarSeconds;

        public KickTimings(float tapMaxSeconds, float passBarSeconds, float shotBarSeconds)
        {
            TapMaxSeconds = tapMaxSeconds;
            PassBarSeconds = passBarSeconds;
            ShotBarSeconds = shotBarSeconds;
        }
    }

    /// <summary>
    /// Pass / Through / Shot buttons → <see cref="ActionCommand"/> on release (GAME_DESIGN §19/§20: toque = força
    /// automática, segurar = você resolve; chute = segurar e soltar, força pelo tempo). One action charges at a time:
    /// other buttons are ignored until the charging one is released. Pure, so it is dotnet-tested; the Unity
    /// <c>InputAdapter</c> only reads devices and feeds it.
    /// </summary>
    public sealed class ActionInputFilter
    {
        private ActionKind _charging;
        private float _heldSeconds;
        private KickTimings _lastTimings;

        public ActionKind Charging => _charging;

        /// <summary>0-1 fill of the bar currently charging (for the HUD).</summary>
        public float ChargeFraction => _charging == ActionKind.None ? 0f : PowerFor(_charging, _heldSeconds, _lastTimings, forHud: true);

        public ActionCommand Step(bool passHeld, bool throughHeld, bool shotHeld, KickTimings timings, float dt)
        {
            _lastTimings = timings;
            if (_charging == ActionKind.None)
            {
                // Shot first: the big button is the most deliberate press when several land on the same frame.
                if (shotHeld) _charging = ActionKind.Shot;
                else if (passHeld) _charging = ActionKind.Pass;
                else if (throughHeld) _charging = ActionKind.Through;
                _heldSeconds = 0f;
                return ActionCommand.None;
            }

            bool stillHeld = _charging == ActionKind.Shot ? shotHeld : _charging == ActionKind.Pass ? passHeld : throughHeld;
            if (stillHeld)
            {
                _heldSeconds += dt;
                return ActionCommand.None;
            }

            var command = new ActionCommand(_charging, PowerFor(_charging, _heldSeconds, timings, forHud: false));
            _charging = ActionKind.None;
            _heldSeconds = 0f;
            return command;
        }

        private static float PowerFor(ActionKind kind, float held, KickTimings t, bool forHud)
        {
            if (kind == ActionKind.Shot) return Clamp01(held / t.ShotBarSeconds);
            if (!forHud && held <= t.TapMaxSeconds) return ActionCommand.AutoPower;
            return Clamp01(held / t.PassBarSeconds);
        }

        private static float Clamp01(float v) => MathF.Max(0f, MathF.Min(1f, v));
    }
}
