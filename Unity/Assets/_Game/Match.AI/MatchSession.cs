using System;
using System.Numerics;
using Game.Core.Contracts.Match;
using Game.Data.Loading;
using Game.Match;

namespace Game.Match.AI
{
    /// <summary>
    /// The match as the app runs it (A7b; TECHNICAL_SPEC §5: "simulação em passo fixo desacoplada do render"): a frame's
    /// real time goes into an accumulator that steps the <see cref="AiMatch"/> at its fixed step; the user's one-shot
    /// actions (released kick, switch tap) reach exactly one step; pause stops time; the previous step's positions are
    /// kept so the view can interpolate (<see cref="Alpha"/>); recent events are kept for the view (kick animations,
    /// goal banner). Snapshot/restore wraps <see cref="AiMatch.Snapshot"/> and always restores paused. Pure: no Unity,
    /// no real clock (the caller passes the frame time). Zero allocation per frame.
    /// </summary>
    public sealed class MatchSession
    {
        /// <summary>A long frame (a hitch, the app coming back) never runs more steps than this; the rest is dropped.</summary>
        public const int MaxStepsPerFrame = 10;
        private const int EventCapacity = 32;

        public AiMatch Match { get; }
        public MatchSetup Setup { get; }
        public bool Paused { get; private set; }
        /// <summary>0-1: how far the render time is between the previous step and the current one.</summary>
        public float Alpha => Match.StepSeconds > 0f ? MathF.Min(1f, _accumulator / Match.StepSeconds) : 1f;

        private readonly Vector3[] _previous;
        private readonly Vector3[] _current;
        private Vector3 _previousBall, _currentBall;
        private readonly long[] _lastKickStep;
        private float _accumulator;
        private long _steps;
        private ActionCommand _pendingAction = ActionCommand.None;
        private bool _pendingSwitch;

        private readonly AiMatchEvent[] _eventKinds = new AiMatchEvent[EventCapacity];
        private readonly long[] _eventSteps = new long[EventCapacity];
        private int _eventHead, _eventCount;

        public MatchSession(GameDatabase db, MatchSetup setup, float stepSeconds, MatchSide? humanSide)
            : this(new AiMatch(db, setup, stepSeconds), setup, humanSide, enableHuman: true)
        {
        }

        private MatchSession(AiMatch match, MatchSetup setup, MatchSide? humanSide, bool enableHuman)
        {
            Match = match;
            Setup = setup;
            if (enableHuman && humanSide.HasValue) Match.EnableHuman(humanSide.Value);
            _previous = new Vector3[Match.Players.Length];
            _current = new Vector3[Match.Players.Length];
            _lastKickStep = new long[Match.Players.Length];
            for (int i = 0; i < _lastKickStep.Length; i++) _lastKickStep[i] = long.MinValue / 2;
            CaptureCurrent();
            CopyCurrentToPrevious();
        }

        /// <summary>Rebuilds a session from <see cref="Snapshot"/> (same data and setup), paused.</summary>
        public static MatchSession Restore(GameDatabase db, MatchSetup setup, float stepSeconds, byte[] snapshot)
        {
            var match = AiMatch.Restore(db, setup, stepSeconds, snapshot);
            var s = new MatchSession(match, setup, null, enableHuman: false) { Paused = true };
            return s;
        }

        /// <summary>The whole match state (pausing is the caller's business; restore always comes back paused).</summary>
        public byte[] Snapshot() => Match.Snapshot();

        public void Pause() => Paused = true;

        /// <summary>Resumes; the time spent paused is not caught up.</summary>
        public void Resume()
        {
            Paused = false;
            _accumulator = 0f;
        }

        /// <summary>Queues a released action and/or a switch tap for the next step (they reach exactly one step).</summary>
        public void Queue(ActionCommand action, bool switchPressed)
        {
            if (action.Kind != ActionKind.None) _pendingAction = action;
            if (switchPressed) _pendingSwitch = true;
        }

        /// <summary>Advances by one rendered frame. <paramref name="move"/>, <paramref name="sprint"/> and
        /// <paramref name="containHeld"/> are the held inputs of this frame. Returns the steps run.</summary>
        public int Advance(float frameSeconds, Vector2 move, bool sprint, bool containHeld)
        {
            if (Paused || Match.Finished || frameSeconds <= 0f) return 0;
            _accumulator += frameSeconds;
            int steps = 0;
            while (_accumulator >= Match.StepSeconds && steps < MaxStepsPerFrame && !Match.Finished)
            {
                CopyCurrentToPrevious();
                var input = new HumanInput(move, sprint, _pendingAction, containHeld, _pendingSwitch);
                _pendingAction = ActionCommand.None;
                _pendingSwitch = false;
                var ev = Match.Step(input);
                _steps++;
                CaptureCurrent();
                Record(ev);
                _accumulator -= Match.StepSeconds;
                steps++;
            }
            if (steps == MaxStepsPerFrame) _accumulator = 0f;
            if (Match.Finished) _accumulator = 0f;
            return steps;
        }

        /// <summary>Interpolated ground position of player <paramref name="global"/> for this frame.</summary>
        public Vector3 PlayerPosition(int global) => Vector3.Lerp(_previous[global], _current[global], Alpha);

        public Vector3 BallPosition => Vector3.Lerp(_previousBall, _currentBall, Alpha);

        /// <summary>Seconds since player <paramref name="global"/> last kicked (pass, shot or restart), for the kick animation.</summary>
        public float SecondsSinceKick(int global) => (_steps - _lastKickStep[global]) * Match.StepSeconds + _accumulator;

        /// <summary>Seconds since the last event of <paramref name="kind"/> (positive infinity if none is remembered).</summary>
        public float SecondsSince(AiMatchEvent kind)
        {
            for (int k = 0; k < _eventCount; k++)
            {
                int i = (_eventHead - 1 - k + EventCapacity) % EventCapacity;
                if (_eventKinds[i] == kind) return (_steps - _eventSteps[i]) * Match.StepSeconds + _accumulator;
            }
            return float.PositiveInfinity;
        }

        private void Record(AiMatchEvent ev)
        {
            if (ev == AiMatchEvent.None) return;
            if ((ev == AiMatchEvent.Pass || ev == AiMatchEvent.Shot) && Match.Ball.LastTouch >= 0) _lastKickStep[Match.Ball.LastTouch] = _steps;
            _eventKinds[_eventHead] = ev;
            _eventSteps[_eventHead] = _steps;
            _eventHead = (_eventHead + 1) % EventCapacity;
            if (_eventCount < EventCapacity) _eventCount++;
        }

        private void CaptureCurrent()
        {
            for (int i = 0; i < _current.Length; i++) _current[i] = Match.Players[i].Body.Position;
            _currentBall = Match.Ball.Position;
        }

        private void CopyCurrentToPrevious()
        {
            Array.Copy(_current, _previous, _current.Length);
            _previousBall = _currentBall;
        }
    }
}
