using System;
using System.IO;
using Game.Core.Contracts.Match;
using Game.Data.Loading;
using Game.Data.Match;
using Game.Data.Presentation;
using Game.Input;
using Game.Match;
using Game.Match.AI;
using Game.Presentation;
using UnityEngine;

namespace Game.App
{
    /// <summary>
    /// Entry point of the Match scene (A7b; TECHNICAL_SPEC §15 "Match | jogadores, câmera, HUD, pausa; paisagem" and §19
    /// vertical slice): the user's side (the strong vertical-slice team by default, always attacking left to right)
    /// against the AI, 2×2 min with the 90-minute clock. Wires the pure <see cref="MatchSession"/> to the views (pitch,
    /// animated placeholder players, ball, broadcast camera, HUD with radar, pause menu, final screen) and the touch input.
    /// Going to the background (or quitting) saves the whole match and pauses it; the next launch resumes it paused
    /// ("pausa + retomar após segundo plano"). Not pure: it is the only place that reads the frame time.
    /// </summary>
    public sealed class MatchBootstrap : MonoBehaviour
    {
        private const float StepSeconds = 1f / 50f; // GAME_DESIGN §25: passo fixo 50-60 Hz
        private const string SnapshotFile = "match_in_progress.bin";
        private const int SnapshotFileMagic = 0x4D534341; // "ACSM"
        private const int SnapshotFileVersion = 1;

        [Tooltip("You play the strong vertical-slice team (off = the weak one).")]
        public bool PlayStrongTeam = true;
        [Tooltip("Real minutes; the clock shows 90 (TECHNICAL_SPEC §19: 2×2 min).")]
        public int DurationMinutes = 4;

        private GameDatabase _db;
        private MatchViewDefinition _view;
        private VerticalSliceDefinition _teams;
        private MatchSession _session;
        private InputAdapter _input;
        private MaterialCache _materials;
        private PlayerView[] _players;
        private Transform _ball, _ballShadow;
        private BroadcastCamera _camera;
        private MatchHudView _hud;
        private bool _userStrong;
        private ulong _seed;
        private bool _finalShown;
        private string _error;

        private string SnapshotPath => Path.Combine(Application.persistentDataPath, SnapshotFile);

        private void Start()
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Application.targetFrameRate = 60; // Android caps at 30 by default; the VS aims at ~60 on mid-range devices
            var source = UnityDataRoot.Resolve();
            if (!source.IsSuccess) { Fail(source.ToString()); return; }
            var db = GameDataLoader.Load(source.Value);
            if (!db.IsSuccess) { Fail(db.ToString()); return; }
            _db = db.Value;
            var teams = GameDataLoader.LoadVerticalSlice(source.Value, _db);
            if (!teams.IsSuccess) { Fail(teams.ToString()); return; }
            _teams = teams.Value;
            var view = GameDataLoader.LoadMatchView(source.Value);
            if (!view.IsSuccess) { Fail(view.ToString()); return; }
            _view = view.Value;

            var pitch = Pitch.From(_db.Ball.Pitch);
            PitchView.Build(pitch, "Pitch");
            if (FindAnyObjectByType<Light>() == null)
            {
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            _materials = new MaterialCache();

            _input = gameObject.AddComponent<InputAdapter>();
            _input.Configure(new KickTimings(_db.Kicking.Common.TapMaxSeconds, _db.Kicking.Pass.PowerBarSeconds, _db.Kicking.Shot.PowerBarSeconds),
                _db.Movement.SprintMemorySeconds);

            if (!TryResume()) NewMatch(PlayStrongTeam, (ulong)Environment.TickCount);
        }

        private void Fail(string error)
        {
            _error = error;
            Debug.LogError("MatchBootstrap: " + error);
        }

        // ---- match life cycle ----

        private void NewMatch(bool userStrong, ulong seed)
        {
            DeleteSnapshot();
            var setup = Setup(userStrong, seed, DurationMinutes);
            Build(new MatchSession(_db, setup, StepSeconds, MatchSide.Home), userStrong, seed);
        }

        private MatchSetup Setup(bool userStrong, ulong seed, int minutes)
        {
            var mine = _teams.Team(userStrong ? "strong" : "weak");
            var theirs = _teams.Team(userStrong ? "weak" : "strong");
            return new MatchSetup(mine.ToSetup(), theirs.ToSetup(), false, minutes, 5, seed);
        }

        /// <summary>(Re)creates the views for a session: the user is always the home side, attacking +x (left to right).</summary>
        private void Build(MatchSession session, bool userStrong, ulong seed)
        {
            DestroyViews();
            _session = session;
            _userStrong = userStrong;
            _seed = seed;
            _finalShown = false;
            var m = session.Match;
            var mine = _teams.Team(userStrong ? "strong" : "weak");
            var theirs = _teams.Team(userStrong ? "weak" : "strong");

            var homeShirt = new Color(0.15f, 0.3f, 0.95f);
            var awayShirt = new Color(0.9f, 0.15f, 0.15f);
            var skin = new Color(0.85f, 0.65f, 0.5f);
            _players = new PlayerView[m.Players.Length];
            for (int i = 0; i < m.Players.Length; i++)
            {
                var p = m.Players[i];
                bool home = p.Team == m.Home;
                var shirt = p.IsGoalkeeper ? (home ? new Color(0.1f, 0.7f, 0.3f) : new Color(0.95f, 0.6f, 0.1f)) : (home ? homeShirt : awayShirt);
                _players[i] = new PlayerView(_materials, shirt, home ? Color.white : Color.black, skin,
                    (home ? mine.Name : theirs.Name) + " " + (p.Local + 1));
            }
            float r = _db.Ball.Ball.Radius;
            _ball = _materials.Part(PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * r * 2f * 1.6f, Color.white, "Ball").transform;
            _ballShadow = _materials.Part(PrimitiveType.Cylinder, null, Vector3.zero, new Vector3(r * 3f, 0.005f, r * 3f), new Color(0f, 0f, 0f, 1f), "Ball shadow").transform;

            _camera = new BroadcastCamera(_view, m.Pitch.HalfLength, m.Pitch.HalfWidth, userAttacksPositiveX: true);
            _hud = new MatchHudView(_view, Resources.Load<UnityEngine.UIElements.ThemeStyleSheet>("AcessoRuntimeTheme"), mine.Name, theirs.Name,
                homeShirt, awayShirt, m.Pitch.HalfLength, m.Pitch.HalfWidth, true, m.Players.Length);
            _hud.PauseRequested += Pause;
            _hud.ResumeRequested += Resume;
            _hud.RestartRequested += () => NewMatch(_userStrong, (ulong)Environment.TickCount);
            _input.ReservedTopFraction = _hud.ReservedTopFraction;
            _hud.ShowPause(session.Paused);
        }

        private void DestroyViews()
        {
            if (_players != null)
                foreach (var p in _players) Destroy(p.Root.gameObject);
            if (_ball != null) Destroy(_ball.gameObject);
            if (_ballShadow != null) Destroy(_ballShadow.gameObject);
            foreach (var name in new[] { "Match HUD", "Broadcast VCam", "Camera focus" })
            {
                var go = GameObject.Find(name);
                if (go != null) Destroy(go);
            }
            _players = null;
        }

        private void Pause()
        {
            if (_session == null || _session.Match.Finished) return;
            _session.Pause();
            _hud.ShowPause(true);
        }

        private void Resume()
        {
            if (_session == null) return;
            _session.Resume();
            _hud.ShowPause(false);
        }

        // ---- frame ----

        // Frame-rate measurement for the device acceptance (≥ 30 fps entry, ~60 mid-range): average and worst frame per second.
        private float _fpsWindow, _fpsWorstFrame;
        private int _fpsFrames;

        private void MeasureFps()
        {
            float dt = Time.unscaledDeltaTime;
            _fpsWindow += dt;
            _fpsFrames++;
            if (dt > _fpsWorstFrame) _fpsWorstFrame = dt;
            if (_fpsWindow < 1f) return;
            _hud?.SetFps(_fpsFrames / _fpsWindow, 1f / Mathf.Max(1e-4f, _fpsWorstFrame));
            _fpsWindow = 0f;
            _fpsFrames = 0;
            _fpsWorstFrame = 0f;
        }

        private void Update()
        {
            MeasureFps();
            if (_session == null) return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape) || UnityEngine.Input.GetKeyDown(KeyCode.P))
            {
                if (_session.Paused) Resume(); else Pause();
            }

            var m = _session.Match;
            _input.DefenseMode = !m.Home.HasPossession;
            var command = _input.TakeCommand();
            bool switchTap = _input.TakeSwitch();
            if (!_session.Paused)
            {
                _session.Queue(command, switchTap);
                _session.Advance(Time.deltaTime, _input.MoveIntent, _input.SprintHeld, _input.ContainHeld);
            }

            if (m.Finished && !_finalShown)
            {
                _finalShown = true;
                DeleteSnapshot();
                _hud.ShowFinal(m.Result(), m);
            }
        }

        private void LateUpdate()
        {
            if (_session == null) return;
            var m = _session.Match;
            float dt = Time.deltaTime;
            var controlled = m.Controlled;
            for (int i = 0; i < _players.Length; i++)
            {
                var p = m.Players[i];
                var b = p.Body;
                KeeperState? keeper = p.IsGoalkeeper ? m.KeeperOf(p.Team).State : (KeeperState?)null;
                float speed = new Vector2(b.Velocity.X, b.Velocity.Y).magnitude;
                _players[i].Update(ToUnity(_session.PlayerPosition(i)), new Vector3(b.Facing.X, 0f, b.Facing.Y), speed, b.Sprinting,
                    _session.SecondsSinceKick(i), keeper, _view, _session.Paused ? 0f : dt);
                _players[i].SetHighlighted(p == controlled && !m.Finished);
            }

            var ball = ToUnity(_session.BallPosition);
            float r = _db.Ball.Ball.Radius;
            _ball.position = ball + Vector3.up * r * 1.6f;
            _ballShadow.position = new Vector3(ball.x, 0.015f, ball.z);
            var v = m.Ball.Velocity;
            bool setPiece = m.Restart == RestartKind.ThrowIn || m.Restart == RestartKind.Corner || m.Restart == RestartKind.GoalKick;
            _camera.Update(ball, new Vector3(v.X, v.Z, v.Y), setPiece, dt);
            _hud.UpdateLive(_session);
        }

        private void OnGUI()
        {
            if (_error != null) GUI.Label(new Rect(10, 10, Screen.width - 20, 200), "MatchBootstrap error: " + _error);
        }

        private static Vector3 ToUnity(System.Numerics.Vector3 v) => new Vector3(v.X, v.Z, v.Y);

        // ---- background / resume (TECHNICAL_SPEC §5 "Snapshot e pausa") ----

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveAndPause();
        }

        private void OnApplicationQuit() => SaveAndPause();

        private void SaveAndPause()
        {
            if (_session == null || _session.Match.Finished) return;
            Pause();
            try
            {
                string tmp = SnapshotPath + ".tmp";
                using (var w = new BinaryWriter(File.Create(tmp)))
                {
                    var snap = _session.Snapshot();
                    w.Write(SnapshotFileMagic);
                    w.Write(SnapshotFileVersion);
                    w.Write(_userStrong);
                    w.Write(_seed);
                    w.Write(_session.Setup.DurationMinutes);
                    w.Write(snap.Length);
                    w.Write(snap);
                }
                if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath);
                File.Move(tmp, SnapshotPath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("MatchBootstrap: could not save the match in progress: " + e.Message);
            }
        }

        /// <summary>A match saved when the app went to the background comes back paused; anything unreadable (other version,
        /// other data) is discarded and a new match starts.</summary>
        private bool TryResume()
        {
            if (!File.Exists(SnapshotPath)) return false;
            try
            {
                using (var r = new BinaryReader(File.OpenRead(SnapshotPath)))
                {
                    if (r.ReadInt32() != SnapshotFileMagic || r.ReadInt32() != SnapshotFileVersion) throw new InvalidDataException("old match file");
                    bool userStrong = r.ReadBoolean();
                    ulong seed = r.ReadUInt64();
                    int minutes = r.ReadInt32();
                    var snap = r.ReadBytes(r.ReadInt32());
                    var session = MatchSession.Restore(_db, Setup(userStrong, seed, minutes), StepSeconds, snap);
                    Build(session, userStrong, seed);
                    return true;
                }
            }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                Debug.LogWarning("MatchBootstrap: discarding the saved match: " + e.Message);
                DeleteSnapshot();
                return false;
            }
        }

        private void DeleteSnapshot()
        {
            try
            {
                if (File.Exists(SnapshotPath)) File.Delete(SnapshotPath);
            }
            catch (IOException) { }
        }
    }
}
