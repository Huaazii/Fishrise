using System.Collections.Generic;
using Fishing.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fishing.Game
{
    /// <summary>Scene composition root and explicit fixed-step bridge between pure rules and Unity.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private const float ProtectionFlashSeconds = .72f;
        [SerializeField] private FishingConfig config;
        [SerializeField] private FishDefinition[] species;
        [SerializeField] private HookMotor hookPrefab;
        [SerializeField] private Camera gameCamera;
        [SerializeField] private bool startInMenu;
        [SerializeField] private FishingSceneView sceneView;
        [SerializeField] private GameUiAssets uiAssets;
        [SerializeField] private SonarConfig sonarConfig;
        [SerializeField] private BranchHookConfig branchConfig;
        private readonly GameClock _clock = new GameClock();
        private DragInput _input;
        private FishingFlowController _flow;
        private FishSpawner _spawner;
        private HookMotor _hook;
        private ContactDetector _detector;
        private CameraRig _cameraRig;
        private GameUi _ui;
        private AbilityRig _abilityRig;
        private SkillTreasureRig _treasures;
        private GameFeedback _feedback;
        private int _lastProtection;
        private double _protectionHitAt = double.NegativeInfinity;
        private SimulationMode2D _previousSimulation;
        private bool _ownsSimulation;
        private bool _applicationPaused;
        private bool _fastAscentFeedback;
        private int _screenWidth, _screenHeight;
        private Rect _screenSafeArea;
        /// <summary>Persistent player controls shared by the input adapter and both settings entry points.</summary>
        public FishingControlSettings Controls { get; private set; }
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
        /// <summary>Scene-owned Chinese interface and its read-only presentation state.</summary>
        public GameUi Ui => _ui;
        /// <summary>Pure, per-run ability models and command strategies.</summary>
        public AbilityController Abilities => _flow?.Abilities;
        /// <summary>Scene-owned sonar and pooled branch presentation.</summary>
        public AbilityRig AbilityView => _abilityRig;
        /// <summary>Scene-owned optional skill pickups and their reusable visuals.</summary>
        public SkillTreasureRig Treasures => _treasures;
        /// <summary>Scene-owned sound preferences and committed-event feedback.</summary>
        public GameFeedback Feedback => _feedback;
        /// <summary>How many visual capture reactions ran in this run.</summary>
        public int AppliedCaptureEvents { get; private set; }
        /// <summary>How many committed losses have been applied to this run's views.</summary>
        public int AppliedEscapeEvents { get; private set; }
        /// <summary>Whether all serialized asset bindings are present.</summary>
        public bool HasBindings => config != null && species != null && species.Length > 0 &&
            System.Array.TrueForAll(species, f => f != null && f.sprite != null && f.prefab != null && f.prefab.Definition == f) &&
            hookPrefab != null && gameCamera != null && uiAssets != null && uiAssets.font != null &&
            sonarConfig != null && branchConfig != null && LayerMask.NameToLayer(FishActor.SmallFishLayer) >= 0 &&
            LayerMask.NameToLayer("FishingBranchHook") >= 0;
        /// <summary>Sets the composition bindings while preparing saved scenes in the Editor.</summary>
        public void Bind(FishingConfig rules, FishDefinition[] fish, HookMotor hookAsset, Camera camera, bool menu)
        { config = rules; species = fish; hookPrefab = hookAsset; gameCamera = camera; startInMenu = menu; }
        /// <summary>Sets the saved environment reference during scene preparation.</summary>
        public void BindSceneView(FishingSceneView view) { sceneView = view; }
        private UnderwaterAtmosphere _atmosphere;
        private void Start()
        {
            if (!HasBindings) { Debug.LogError("Fishing scene has missing asset bindings.", this); enabled = false; return; }
            Application.targetFrameRate = 120;
            Controls = new FishingControlSettings(); _input = new DragInput(Controls);
            Controls.Changed += ControlsChanged;
            if (sceneView != null) _atmosphere = sceneView.GetComponent<UnderwaterAtmosphere>();
            Application.runInBackground = true;
            _previousSimulation = Physics2D.simulationMode; Physics2D.simulationMode = SimulationMode2D.Script;
            _ownsSimulation = true;
            var pool = new GameObject("Fish Pool").transform; pool.SetParent(transform);
            _spawner = new FishSpawner(pool);
            _hook = Instantiate(hookPrefab, transform); _hook.name = "Main Hook";
            if (sceneView != null) { sceneView.ApplyState(RunState.Menu); _hook.SetRopeOrigin(sceneView.RodTip, sceneView.LineGuide, sceneView.GetComponent<SurfaceLighting>()); }
            _detector = new ContactDetector(_hook); _hook.SetDetector(_detector);
            _cameraRig = new CameraRig(gameCamera, config, sceneView != null ? sceneView.SeabedY : (float?)null, _hook.transform); _cameraRig.Reset();
            _abilityRig = new AbilityRig(transform, _hook, _spawner, config, branchConfig, uiAssets.hook,
                sceneView.GetComponent<SurfaceLighting>());
            _cameraRig.BindBranches(_abilityRig.Branches);
            _treasures = new SkillTreasureRig(transform, uiAssets, hookPrefab.GetComponentInChildren<SpriteRenderer>().sharedMaterial, _hook.LineMaterial);
            _treasures.Collected += TreasureCollected;
            _feedback = new GameFeedback(transform, uiAssets.audio);
            _feedback.Pause(_applicationPaused);
            _ui = new GameUi(transform, uiAssets, species, StartFromMenu, PauseRun, ResumeRun, BeginRun, ExitToMenu, Application.Quit,
                gameCamera, _feedback, Controls);
            _ui.Surface.BindWorld(transform, _hook, _spawner, sceneView, _abilityRig.Branches);
            if (!startInMenu) BeginRun();
        }
        /// <summary>Creates a fresh controller/model, recycles all prior views and reconnects exactly once.</summary>
        public void BeginRun()
        {
            Unsubscribe(); _spawner.Clear(); _detector.Begin(); _clock.IsPaused = _applicationPaused;
            _feedback.ResetRun(); _feedback.Pause(_applicationPaused); _ui.ResetRun();
            _fastAscentFeedback = false;
            var rules = new List<FishRules>(); foreach (var definition in species) rules.Add(definition.Snapshot());
            _flow = new FishingFlowController(config.Snapshot(), rules, sonarConfig.CreateRules(config.enableSkillTreasures),
                branchConfig.CreateRules(config.enableSkillTreasures)); AppliedCaptureEvents = AppliedEscapeEvents = 0;
            _lastProtection = Run.Protection;
            _protectionHitAt = double.NegativeInfinity;
            _flow.OnStateChanged += StateChanged; _flow.OnFishCaptured += Captured; _flow.OnRunFinished += Finished;
            _flow.OnFishEscaped += Escaped;
            Run.OnDepthChanged += DepthChanged; Run.OnProtectionChanged += ProtectionChanged; Run.OnBecameFull += Full;
            Abilities.Changed += AbilityChanged; _abilityRig.Bind(Abilities); _ui.SetAbilities(Run, Abilities);
            Abilities.Sonar.Pulsed += SonarPulsed; Abilities.Branches.Activated += BranchesActivated;
            _hook.transform.position = Vector3.zero; _hook.ResetPresentation(); _input.Reset(0); _cameraRig.Reset();
            _input.CalibrateTilt();
            _atmosphere?.ResetAnimation();
            if (sceneView != null) sceneView.FollowCamera(gameCamera);
            _spawner.SpawnRun(config, species, _hook.Radius * 2);
            var shape = _hook.Head;
            float pickupRadius = shape.radius * Mathf.Max(Mathf.Abs(shape.transform.lossyScale.x), Mathf.Abs(shape.transform.lossyScale.y));
            Vector2 pickupOffset = shape.transform.TransformPoint(shape.offset) - _hook.transform.position;
            _treasures.Bind(Run, Abilities, config, _spawner, pickupRadius, pickupOffset); _ui.Pause(false); _flow.Start();
            RefreshPresentation();
        }
        /// <summary>Freezes only gameplay, preserving the flow state and interactive pause UI.</summary>
        public void PauseRun()
        {
            if (Run == null) return;
            _flow.SetPaused(true); _clock.IsPaused = Run.IsPaused || _applicationPaused;
            _input.Reset(_hook.transform.position.x); _ui.Pause(Run.IsPaused);
            _feedback.Pause(_clock.IsPaused);
            AbilityChanged();
        }
        /// <summary>Continues from the exact paused state.</summary>
        public void ResumeRun()
        {
            if (Run == null) return;
            _flow.SetPaused(false); _clock.IsPaused = Run.IsPaused || _applicationPaused;
            _input.Reset(_hook.transform.position.x); _ui.Pause(false);
            _feedback.Pause(_clock.IsPaused);
            AbilityChanged();
        }
        /// <summary>Requests the diving-only sonar command; rejected requests consume nothing.</summary>
        public void FireSonar() { Abilities?.Sonar.Activate(); }
        /// <summary>Requests the ascent-only branch command; repeated input cannot redeploy.</summary>
        public void DeployBranches() { Abilities?.Branches.Activate(); }
        private void SonarPulsed() { _feedback.Play(GameFeedback.Cue.Sonar); }
        private void BranchesActivated() { _feedback.Play(GameFeedback.Cue.Branches); }
        /// <summary>Skips only the surface presentation, keeping the same capture records and settlement.</summary>
        public void SkipSurfacing() { _flow?.SkipSurfacing(); }
        /// <summary>Submits the same held/released intent used by touch and the Space key.</summary>
        public void SetReelingInput(bool held)
        { _flow?.SetReelingInput(held); }
        private void StartFromMenu()
        { if (SceneManager.GetActiveScene().name == "Menu") SceneManager.LoadScene("Main"); else BeginRun(); }
        private void ExitToMenu() { SceneManager.LoadScene("Menu"); }
        private void ControlsChanged() { Run?.Reeling.ClearInput(); }
        private void Update()
        {
            if (_ui == null || _applicationPaused) return;
            _input.SampleTilt(Time.unscaledDeltaTime);
            if (_screenWidth != Screen.width || _screenHeight != Screen.height || _screenSafeArea != Screen.safeArea)
            { _screenWidth = Screen.width; _screenHeight = Screen.height; _screenSafeArea = Screen.safeArea; _ui.ApplySafeArea(); }
            if (Input.GetKeyDown(KeyCode.Escape) && _ui.HandleBack()) return;
            if (Run == null) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (Run.IsPaused) ResumeRun(); else PauseRun(); }
            bool controllable = Run.State == RunState.Diving || Run.State == RunState.BottomHold || Run.State == RunState.Ascending;
            if (!Run.IsPaused && controllable)
            {
                _input.Read(gameCamera, _hook.transform.position.x, config.followTouchPoint,
                    Run.Rules.Reeling.Enabled && Run.State == RunState.Ascending && Run.TotalCaptureCount > 0 && !Run.IsFull);
                SetReelingInput(_input.ReelHeld);
            }
            _ui.Countdown(Run, _flow.StateElapsed);
        }
        private void FixedUpdate() { AdvanceGameplay(Time.fixedUnscaledDeltaTime); }
        /// <summary>Runs one explicit fixed step. Also used by deterministic PlayMode acceptance.</summary>
        public void AdvanceGameplay(float unscaledDt)
        {
            if (_clock.IsPaused) return;
            _feedback?.Tick(unscaledDt, Run);
            if (_flow == null) return;
            if (Run.State == RunState.Result) { _ui.Tick(_clock.Delta(unscaledDt)); return; }
            float dt = _clock.Delta(unscaledDt); Vector2 previous = _hook.transform.position;
            Vector2 previousPickupCentre = _hook.Head.transform.TransformPoint(_hook.Head.offset);
            long step = _flow.BeginStep(dt);
            _detector.Begin(); _spawner.Tick(dt);
            bool steerable = Run.State == RunState.Diving || Run.State == RunState.BottomHold || Run.State == RunState.Ascending;
            float targetX = steerable ? _input.MovementTarget(_hook.transform.position.x, config.horizontalSpeed, dt)
                : _hook.transform.position.x;
            _hook.Move(targetX, Run.Depth, config.unitsPerMetre, config.horizontalSpeed, config.halfWidth, dt);
            _abilityRig.Tick(Run, dt);
            Physics2D.SyncTransforms();
            // Simulate delivers this step's trigger messages before Finish submits the complete batch.
            Physics2D.Simulate(dt);
            _flow.ResolveContacts(_abilityRig.Combine(_detector.Finish(Run, step, previous), Run));
            var shape = _hook.Head;
            float pickupHookRadius = shape.radius * Mathf.Max(Mathf.Abs(shape.transform.lossyScale.x), Mathf.Abs(shape.transform.lossyScale.y));
            _treasures.Tick(Run, previousPickupCentre, shape.transform.TransformPoint(shape.offset), pickupHookRadius, dt, gameCamera);
            RefreshPresentation(); _ui.Tick(dt);
            _cameraRig.PresentSurface(_ui.Surface.Elapsed, Run.Rules.SurfacingSeconds, Run.State == RunState.Surfacing);
        }
        private void LateUpdate()
        {
            if (_cameraRig == null || _clock.IsPaused) return;
            _atmosphere?.Tick(Time.unscaledDeltaTime);
            _cameraRig.Follow(Run, Time.unscaledDeltaTime);
            _ui.Surface.RefreshScreenPositions();
            _ui.Reeling(Run, _hook.transform.position, _cameraRig.ShakeOffset, Time.unscaledDeltaTime);
            if (sceneView != null) sceneView.FollowCamera(gameCamera);
        }
        private void Captured(CaptureRecord record)
        {
            AppliedCaptureEvents++;
            _feedback.Play(GameFeedback.Cue.Capture);
            int index = Run.CountForHook(record.HookId) - 1;
            if (_spawner.Take(record.FishInstanceId, out var fish))
            {
                _ui.Capture(record, fish.transform.position);
                if (record.Source == CaptureSource.Main) fish.Attach(_hook.transform, index, _hook.HangPosition(index));
                else { var branch = _abilityRig.ForHook(record.HookId); fish.Attach(branch.transform, index, branch.HangPosition(index)); }
            }
            _ui.Refresh(Run);
        }
        private void TreasureCollected(SkillTreasureKind kind, Vector3 position)
        { _feedback.Play(GameFeedback.Cue.Treasure); _ui.Treasure(kind, position); }
        private void Escaped(CaptureRecord record)
        {
            AppliedEscapeEvents++;
            _feedback.Play(GameFeedback.Cue.Escape);
            if (_spawner.Escape(record.FishInstanceId, out var lost))
                _ui.Escaped(record, lost.Definition.displayName, lost.transform.position);
            int index = 0;
            foreach (var retained in Run.Captures)
            {
                if (retained.HookId != record.HookId) continue;
                if (_spawner.TryGetOwned(retained.FishInstanceId, out var fish))
                {
                    Vector3 slot = record.HookId == 0 ? _hook.HangPosition(index) : _abilityRig.ForHook(record.HookId).HangPosition(index);
                    fish.Rehang(index, slot);
                }
                index++;
            }
            _ui.StateChanged(Run); AbilityChanged();
        }
        private void StateChanged(RunState state)
        {
            _treasures?.RefreshVisibility(Run);
            if (state == RunState.Diving) _feedback.Play(GameFeedback.Cue.Dive);
            if (state == RunState.Surfacing)
            {
                _feedback.Play(GameFeedback.Cue.Surface);
            }
            _input.Reset(_hook.transform.position.x); _ui.StateChanged(Run); AbilityChanged(); RefreshPresentation();
        }
        private void Finished(RunModel run) { _feedback.Play(GameFeedback.Cue.Result); _ui.ShowResult(run); }
        private void DepthChanged(float depth) { _ui.Refresh(Run); }
        private void ProtectionChanged(int charges)
        {
            if (charges < _lastProtection)
            {
                _protectionHitAt = Run.GameTime;
                _feedback.Play(GameFeedback.Cue.Protection); _ui.ProtectionBurst(_hook.transform.position);
            }
            _lastProtection = charges; _ui.Refresh(Run);
        }
        private void Full() { _feedback.Play(GameFeedback.Cue.Full); _ui.StateChanged(Run); }
        private void AbilityChanged() { _ui.SetAbilities(Run, Abilities); }
        private void RefreshPresentation()
        {
            if (Run == null) return;
            if (sceneView != null)
            {
                sceneView.ApplyState(Run.State);
                if (Run.State == RunState.Ascending) sceneView.FrameSurface(_cameraRig.SurfaceFraming(Run));
            }
            bool ascent = Run.State == RunState.Ascending;
            if (ascent && Run.IsFull && !_fastAscentFeedback)
            {
                _fastAscentFeedback = true;
                _feedback.PulseFastAscent(config.fastAscentHapticMilliseconds, config.fastAscentHapticAmplitude);
            }
            Color lamp = ascent && Run.IsFull ? new Color(1, .25f, .2f) :
                ascent && Run.MainCaptureCount > 0 ? new Color(1, .8f, .15f) : new Color(.3f, 1, .6f);
            bool protectionPhase = Run.State == RunState.Diving && Run.Depth < Run.Rules.BottomZoneDepth;
            float hitAge = protectionPhase ? (float)(Run.GameTime - _protectionHitAt) : float.PositiveInfinity;
            bool flash = hitAge >= 0 && hitAge < ProtectionFlashSeconds;
            // Three off/on pulses make spending a charge visible, including the last charge.
            // This only changes renderers; it cannot grant protection or alter head contacts.
            bool protectionVisible = protectionPhase && (Run.Protection > 0 || flash) &&
                (!flash || ((int)(hitAge * (6f / ProtectionFlashSeconds)) & 1) != 0);
            _hook.Show(lamp, protectionVisible);
            _ui.Reeling(Run, _hook.transform.position, _cameraRig.ShakeOffset, Time.fixedUnscaledDeltaTime);
        }
        private void Unsubscribe()
        {
            if (_flow == null) return;
            _flow.OnStateChanged -= StateChanged; _flow.OnFishCaptured -= Captured; _flow.OnRunFinished -= Finished;
            _flow.OnFishEscaped -= Escaped;
            Run.OnDepthChanged -= DepthChanged; Run.OnProtectionChanged -= ProtectionChanged; Run.OnBecameFull -= Full;
            Abilities.Changed -= AbilityChanged;
            Abilities.Sonar.Pulsed -= SonarPulsed; Abilities.Branches.Activated -= BranchesActivated;
            _abilityRig.Unbind(); Abilities.Cancel();
        }
        private void OnDestroy()
        {
            // Scene-owned children are already being destroyed; recycling here can touch torn-down colliders.
            Unsubscribe();
            if (_treasures != null) _treasures.Collected -= TreasureCollected;
            _feedback?.Dispose();
            if (Controls != null) Controls.Changed -= ControlsChanged;
            _input?.Dispose(); _ui?.Dispose();
            if (_ownsSimulation) Physics2D.simulationMode = _previousSimulation;
        }
        private void OnApplicationPause(bool paused)
        {
            _applicationPaused = paused;
            if (paused && _flow != null) PauseRun();
            // Countdown/surface/result reject player pause, but still freeze while the app is backgrounded.
            _clock.IsPaused = paused || (Run != null && Run.IsPaused); _feedback?.Pause(_clock.IsPaused);
        }
    }
}
