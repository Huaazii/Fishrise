using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Game
{
    /// <summary>Scene composition root and explicit fixed-step bridge between pure rules and Unity.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private FishingConfig config;
        [SerializeField] private FishDefinition[] species;
        [SerializeField] private FishActor fishPrefab;
        [SerializeField] private HookMotor hookPrefab;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private bool startInMenu;
        [SerializeField] private FishingSceneView sceneView;
        private readonly GameClock _clock = new GameClock();
        private readonly DragInput _input = new DragInput();
        private FishingFlowController _flow;
        private FishSpawner _spawner;
        private HookMotor _hook;
        private ContactDetector _detector;
        private CameraRig _cameraRig;
        private GameUi _ui;
        private SimulationMode2D _previousSimulation;
        private bool _ownsSimulation;
        private int _screenWidth, _screenHeight;
        /// <summary>Current authoritative run for acceptance checks and presentation.</summary>
        public RunModel Run => _flow?.Run;
        /// <summary>Current scene's fish ownership and pool.</summary>
        public FishSpawner Spawner => _spawner;
        /// <summary>Main hook view, never used as a capture count source.</summary>
        public HookMotor Hook => _hook;
        /// <summary>Scene-owned environment and fisherman presentation.</summary>
        public FishingSceneView SceneView => sceneView;
        /// <summary>Camera driver shared by runtime updates and saved-scene acceptance.</summary>
        public CameraRig CameraRig => _cameraRig;
        /// <summary>How many visual capture reactions ran in this run.</summary>
        public int AppliedCaptureEvents { get; private set; }
        /// <summary>Whether all serialized asset bindings are present.</summary>
        public bool HasBindings => config != null && species != null && species.Length > 0 &&
            fishPrefab != null && hookPrefab != null && gameCamera != null;
        /// <summary>Sets the composition bindings while preparing saved scenes in the Editor.</summary>
        public void Bind(FishingConfig rules, FishDefinition[] fish, FishActor fishAsset, HookMotor hookAsset, Camera camera, bool menu)
        { config = rules; species = fish; fishPrefab = fishAsset; hookPrefab = hookAsset; gameCamera = camera; startInMenu = menu; }
        /// <summary>Sets the saved environment reference during scene preparation.</summary>
        public void BindSceneView(FishingSceneView view) { sceneView = view; }
        private void Start()
        {
            if (!HasBindings) { Debug.LogError("Fishing scene has missing asset bindings.", this); enabled = false; return; }
            Application.runInBackground = true;
            _previousSimulation = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
            _ownsSimulation = true;
            var pool = new GameObject("Fish Pool").transform; pool.SetParent(transform);
            _spawner = new FishSpawner(fishPrefab, pool);
            _hook = Instantiate(hookPrefab, transform); _hook.name = "Main Hook";
            if (sceneView != null) { sceneView.ApplyState(RunState.Menu); _hook.SetRopeOrigin(sceneView.RodTip, sceneView.LineGuide); }
            _detector = new ContactDetector(_hook); _hook.SetDetector(_detector);
            _cameraRig = new CameraRig(gameCamera, config); _cameraRig.Reset();
            _ui = new GameUi(transform, StartFromMenu, PauseRun, ResumeRun, BeginRun, ExitToMenu, Application.Quit);
            if (!startInMenu) BeginRun();
        }
        /// <summary>Creates a fresh controller/model, recycles all prior views and reconnects exactly once.</summary>
        public void BeginRun()
        {
            Unsubscribe(); _spawner.Clear(); _detector.Begin(); _clock.IsPaused = false;
            var rules = new List<FishRules>(); foreach (var definition in species) rules.Add(definition.Snapshot());
            _flow = new FishingFlowController(config.Snapshot(), rules); AppliedCaptureEvents = 0;
            _flow.OnStateChanged += StateChanged; _flow.OnFishCaptured += Captured; _flow.OnRunFinished += Finished;
            Run.OnDepthChanged += DepthChanged; Run.OnProtectionChanged += ProtectionChanged; Run.OnBecameFull += Full;
            _hook.transform.position = Vector3.zero; _hook.ResetPresentation(); _input.Reset(0); _cameraRig.Reset();
            if (sceneView != null) sceneView.FollowCamera(gameCamera);
            _spawner.SpawnRun(config, species); _ui.Pause(false); _flow.Start();
            RefreshPresentation();
        }
        /// <summary>Freezes only gameplay, preserving the flow state and interactive pause UI.</summary>
        public void PauseRun()
        {
            if (Run == null) return;
            _flow.SetPaused(true); _clock.IsPaused = Run.IsPaused;
            _input.Reset(_hook.transform.position.x); _ui.Pause(Run.IsPaused);
        }
        /// <summary>Continues from the exact paused state.</summary>
        public void ResumeRun()
        {
            if (Run == null) return;
            _flow.SetPaused(false); _clock.IsPaused = Run.IsPaused;
            _input.Reset(_hook.transform.position.x); _ui.Pause(false);
        }
        private void StartFromMenu()
        { if (SceneManager.GetActiveScene().name == "Menu") SceneManager.LoadScene("Main"); else BeginRun(); }
        private void ExitToMenu() { SceneManager.LoadScene("Menu"); }
        private void Update()
        {
            if (_ui == null) return;
            if (_screenWidth != Screen.width || _screenHeight != Screen.height)
            { _screenWidth = Screen.width; _screenHeight = Screen.height; _ui.ApplySafeArea(); }
            if (Run == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (Run.IsPaused) ResumeRun(); else PauseRun(); }
            bool controllable = Run.State == RunState.Diving || Run.State == RunState.BottomHold || Run.State == RunState.Ascending;
            if (!Run.IsPaused && controllable) _input.Read(gameCamera, _hook.transform.position.x, config.followTouchPoint);
            _ui.Countdown(Run, _flow.StateElapsed);
        }
        private void FixedUpdate() { AdvanceGameplay(Time.fixedUnscaledDeltaTime); }
        /// <summary>Runs one explicit fixed step. Also used by deterministic PlayMode acceptance.</summary>
        public void AdvanceGameplay(float unscaledDt)
        {
            if (_flow == null || Run.IsPaused || Run.State == RunState.Result) return;
            float dt = _clock.Delta(unscaledDt); Vector2 previous = _hook.transform.position;
            long step = _flow.BeginStep(dt);
            _detector.Begin(); _spawner.Tick(dt);
            _hook.Move(_input.TargetX, Run.Depth, config.unitsPerMetre, config.horizontalSpeed, config.halfWidth, dt);
            Physics2D.SyncTransforms();
            // Simulate delivers this step's trigger messages before Finish submits the complete batch.
            Physics2D.Simulate(dt);
            _flow.ResolveContacts(_detector.Finish(Run, step, previous));
            RefreshPresentation();
        }
        private void LateUpdate()
        {
            if (_cameraRig == null || _clock.IsPaused) return;
            _cameraRig.Follow(Run, Time.unscaledDeltaTime);
            if (sceneView != null) sceneView.FollowCamera(gameCamera);
        }
        private void Captured(CaptureRecord record)
        {
            AppliedCaptureEvents++;
            int index = Run.TotalCaptureCount - 1;
            if (_spawner.Take(record.FishInstanceId, out var fish)) fish.Attach(_hook.transform, index, _hook.HangPosition(index));
            _ui.Refresh(Run);
        }
        private void StateChanged(RunState state)
        { _input.Reset(_hook.transform.position.x); _ui.StateChanged(Run); RefreshPresentation(); }
        private void Finished(RunModel run) { _ui.ShowResult(run); }
        private void DepthChanged(float depth) { _ui.Refresh(Run); }
        private void ProtectionChanged(int charges) { _ui.Refresh(Run); }
        private void Full() { _ui.StateChanged(Run); }
        private void RefreshPresentation()
        {
            if (Run == null) return;
            if (sceneView != null) sceneView.ApplyState(Run.State);
            bool ascent = Run.State == RunState.Ascending;
            Color lamp = ascent && Run.IsFull ? new Color(1, .25f, .2f) :
                ascent && Run.MainCaptureCount > 0 ? new Color(1, .8f, .15f) : new Color(.3f, 1, .6f);
            _hook.Show(lamp, Run.State == RunState.Diving && Run.Depth < Run.Rules.BottomZoneDepth && Run.Protection > 0);
        }
        private void Unsubscribe()
        {
            if (_flow == null) return;
            _flow.OnStateChanged -= StateChanged; _flow.OnFishCaptured -= Captured; _flow.OnRunFinished -= Finished;
            Run.OnDepthChanged -= DepthChanged; Run.OnProtectionChanged -= ProtectionChanged; Run.OnBecameFull -= Full;
        }
        private void OnDestroy()
        {
            // Scene-owned children are already being destroyed; recycling here can touch torn-down colliders.
            Unsubscribe();
            if (_ownsSimulation) Physics2D.simulationMode = _previousSimulation;
        }
        private void OnApplicationPause(bool paused) { if (paused && _flow != null) PauseRun(); }
    }
}
