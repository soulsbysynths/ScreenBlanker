using System;
using System.Collections.Generic;
using ScreenBlanker.Blackout;
using ScreenBlanker.Keys;
using ScreenBlanker.Platform;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScreenBlanker
{
	/// <summary>
	/// The app: owns the timers and settings, decides when to black out, and hands the OS-specific
	/// work to an <see cref="IPlatformHost"/>. Lives on the ScreenBlanker object in SampleScene, next to
	/// the <see cref="SettingsView"/> that draws the window (ScreenBlanker > Set Up Scene puts it there).
	/// </summary>
	public sealed class BlankerApp : MonoBehaviour
	{
		/// <summary>
		/// Command-line switch passed by the login item: start with the settings window hidden.
		/// </summary>
		public const string HiddenArgument = "--hidden";

		// Busy while a window of ours is on screen, near idle otherwise. Timers run on wall-clock
		// time, so a slow loop only delays the check, never the countdown itself.
		private const int ForegroundFrameRate = 30;
		private const int BackgroundFrameRate = 4;
		private const int BackgroundRenderInterval = 4;

		private readonly CountdownTimer countdown = new CountdownTimer();
		private readonly DailySchedule daily = new DailySchedule();
		private readonly InAppBlackoutGui inAppBlackout = new InAppBlackoutGui();
		private readonly List<KeyCode> recordedKeys = new List<KeyCode>();

		private BlankerSettings settings;
		private IPlatformHost host;
		private BlackoutGraphic blackoutGraphic;
		private string blackoutGraphicPath;
		private int blackoutGraphicMaxPixels;
		private Texture2D graphicPreview;
		private string graphicPreviewPath;
		private string graphicPreviewError;
		private bool isShutDown;
		private bool isGraphicChoiceRequested;

		/// <summary>
		/// The persisted settings. Call <see cref="ApplySettings"/> after changing them.
		/// </summary>
		public BlankerSettings Settings => settings;

		/// <summary>
		/// The one-off countdown.
		/// </summary>
		public CountdownTimer Countdown => countdown;

		/// <summary>
		/// The daily blackout schedule.
		/// </summary>
		public DailySchedule Daily => daily;

		/// <summary>
		/// The OS-specific host.
		/// </summary>
		public IPlatformHost Host => host;

		/// <summary>
		/// True while the settings window is recording a new exit sequence.
		/// </summary>
		public bool IsListening { get; private set; }

		/// <summary>
		/// The keys recorded so far while listening, e.g. "[Ctrl] Q".
		/// </summary>
		public string RecordedKeysDisplay => new KeySequence(recordedKeys).ToDisplayString();

		/// <summary>
		/// The key combination that stops listening without the mouse, e.g. "[Ctrl] [Enter]".
		/// </summary>
		public string StopListeningKeysDisplay => KeyCatalogue.DisplayName(KeyCode.LeftControl) + " " + KeyCatalogue.DisplayName(KeyCode.Return);

		/// <summary>
		/// The text the blackout prompt shows, e.g. "Press [Esc]".
		/// </summary>
		public string PromptText => "Press " + settings.ExitSequence.ToDisplayString();

		/// <summary>
		/// The blackout graphic as a texture for the settings preview, or null if it cannot be loaded
		/// (see <see cref="GraphicPreviewError"/>).
		/// </summary>
		public Texture2D GraphicPreview
		{
			get
			{
				RefreshGraphicPreview();
				return graphicPreview;
			}
		}

		/// <summary>
		/// Why the graphic could not be loaded, or null when it loaded.
		/// </summary>
		public string GraphicPreviewError
		{
			get
			{
				RefreshGraphicPreview();
				return graphicPreviewError;
			}
		}

		/// <summary>
		/// Saves settings and re-arms the daily schedule and login item from them.
		/// </summary>
		public void ApplySettings()
		{
			settings.Save();
			daily.Configure(settings.IsDailyEnabled, settings.DailyHour, settings.DailyMinute);
			host.ApplyStartAtLogin(settings.IsStartAtLoginEnabled);
		}

		/// <summary>
		/// Start when stopped, pause when running, resume when paused.
		/// </summary>
		public void ToggleTimer()
		{
			switch (countdown.State)
			{
				case CountdownTimer.TimerState.Stopped:
					countdown.Start(settings.CountdownTotalSeconds);
					break;
				case CountdownTimer.TimerState.Running:
					countdown.Pause();
					break;
				case CountdownTimer.TimerState.Paused:
					countdown.Resume();
					break;
			}
		}

		/// <summary>
		/// Cancels the countdown.
		/// </summary>
		public void StopTimer()
		{
			countdown.Stop();
		}

		/// <summary>
		/// Blacks out every display now.
		/// </summary>
		public void TriggerBlackout()
		{
			if (host.IsBlackoutVisible)
				return;

			// A blackout mid-recording would feed the recorder; abandon the recording instead.
			if (IsListening)
				CancelListening();

			host.ShowBlackout(BuildBlackoutContent());
		}

		/// <summary>
		/// Starts recording a new exit sequence from the keyboard.
		/// </summary>
		public void StartListening()
		{
			recordedKeys.Clear();
			IsListening = true;
		}

		/// <summary>
		/// Stops recording and saves what was typed as the exit sequence. Nothing typed keeps the old
		/// sequence.
		/// </summary>
		public void StopListening()
		{
			if (!IsListening)
				return;

			IsListening = false;
			if (recordedKeys.Count == 0)
				return;

			settings.ExitSequence = new KeySequence(recordedKeys);
			recordedKeys.Clear();
			ApplySettings();
		}

		/// <summary>
		/// Stops recording without changing the exit sequence.
		/// </summary>
		public void CancelListening()
		{
			IsListening = false;
			recordedKeys.Clear();
		}

		/// <summary>
		/// Puts the exit sequence back to a single Escape.
		/// </summary>
		public void ResetExitSequence()
		{
			CancelListening();
			settings.ExitSequence = KeySequence.Default;
			ApplySettings();
		}

		/// <summary>
		/// Asks for the file dialog. It opens on the next Update, outside OnGUI, because a modal
		/// native dialog must not run in the middle of IMGUI's layout and repaint passes.
		/// </summary>
		public void RequestGraphicChoice()
		{
			isGraphicChoiceRequested = true;
		}

		/// <summary>
		/// Goes back to the built-in logo.
		/// </summary>
		public void UseDefaultGraphic()
		{
			settings.GraphicPath = string.Empty;
			ApplySettings();
		}

		/// <summary>
		/// Quits the app, removing the status item.
		/// </summary>
		public void Quit()
		{
			ShutDownHost();
			Application.Quit();
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#endif
		}

		/// <summary>
		/// Builds the text for the status menu / tray icon.
		/// </summary>
		public StatusSnapshot BuildStatus()
		{
			StatusSnapshot status = new StatusSnapshot();
			TimeSpan remaining = countdown.Remaining;
			string preset = StatusFormatter.Duration(TimeSpan.FromSeconds(settings.CountdownTotalSeconds));

			switch (countdown.State)
			{
				case CountdownTimer.TimerState.Running:
					status.TimerLine = "Timer: " + StatusFormatter.Duration(remaining) + " remaining";
					status.BarTitle = StatusFormatter.CompactDuration(remaining);
					status.ToggleTimerLabel = "Pause timer";
					break;
				case CountdownTimer.TimerState.Paused:
					status.TimerLine = "Timer: paused at " + StatusFormatter.Duration(remaining);
					status.BarTitle = StatusFormatter.CompactDuration(remaining) + " ‖";
					status.ToggleTimerLabel = "Resume timer";
					break;
				default:
					status.TimerLine = "Timer: not running";
					status.ToggleTimerLabel = "Start timer (" + preset + ")";
					break;
			}

			status.DailyLine = daily.IsEnabled
				? "Next blackout: " + StatusFormatter.NextOccurrence(daily.NextLocal)
				: "Daily blackout: off";

			int displays = host.DisplayCount;
			status.DisplaysLine = displays == 1 ? "1 display detected" : displays + " displays detected";
			status.IsArmed = countdown.IsArmed || daily.IsEnabled;
			status.IsStopTimerEnabled = countdown.IsArmed;
			return status;
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void CheckSceneSetUp()
		{
			if (FindObjectOfType<BlankerApp>() == null)
				Debug.LogError("ScreenBlanker: no BlankerApp in the scene. Run ScreenBlanker > Set Up Scene in the editor.");
		}

		private static IPlatformHost CreateHost()
		{
			switch (Application.platform)
			{
				case RuntimePlatform.OSXPlayer:
				case RuntimePlatform.OSXEditor:
					return new MacPlatformHost();
				case RuntimePlatform.WindowsPlayer:
				case RuntimePlatform.WindowsEditor:
					return new WindowsPlatformHost();
				default:
					return new InAppPlatformHost();
			}
		}

		private static bool IsLaunchedHidden()
		{
			foreach (string argument in Environment.GetCommandLineArgs())
			{
				if (argument == HiddenArgument)
					return true;
			}

			return false;
		}

		private void Awake()
		{
			Application.runInBackground = true;
			QualitySettings.vSyncCount = 0;
			Application.targetFrameRate = ForegroundFrameRate;

			host = CreateHost();
			if (!host.TryClaimSingleInstance())
			{
				Debug.Log("ScreenBlanker: already running, exiting.");
				Application.Quit();
				return;
			}

			settings = BlankerSettings.Load();
			host.MenuCommandInvoked += OnMenuCommand;
			host.Initialise(BuildStatus, IsLaunchedHidden());
			host.SetWindowTitle("ScreenBlanker v" + Application.version);

			// Re-registering every launch keeps the login item pointing at wherever the app now lives.
			ApplySettings();

#if UNITY_EDITOR
			// Native windows hold pointers to managed callbacks; tear them down before a script reload
			// frees those callbacks.
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ShutDownHost;
#endif
		}

		private void Update()
		{
			if (settings == null)
				return;

			host.Tick();
			PollRecording();

			if (isGraphicChoiceRequested)
			{
				isGraphicChoiceRequested = false;
				string chosen = host.ChooseImageFile();
				if (chosen != null)
				{
					// Re-choosing the same path reloads it, in case the file was edited.
					blackoutGraphic = null;
					graphicPreviewPath = null;
					settings.GraphicPath = chosen;
					ApplySettings();
				}
			}

			if (countdown.PollExpired())
				TriggerBlackout();

			if (daily.PollDue())
				TriggerBlackout();

			bool isOnScreen = host.IsSettingsWindowVisible || host.IsBlackoutVisible || Application.isEditor;
			Application.targetFrameRate = isOnScreen ? ForegroundFrameRate : BackgroundFrameRate;
			OnDemandRendering.renderFrameInterval = isOnScreen ? 1 : BackgroundRenderInterval;
		}

		private void OnGUI()
		{
			// Only the fallback host draws its blackout in the Unity window; the settings window itself
			// is UI Toolkit (see SettingsView).
			if (settings == null || !host.IsBlackoutDrawnInApp || !host.IsBlackoutVisible)
				return;

			inAppBlackout.Draw(settings.IsPromptEnabled ? PromptText : null, settings.IsGraphicEnabled ? GraphicPreview : null);
		}

		private void OnMenuCommand(MenuCommand command)
		{
			switch (command)
			{
				case MenuCommand.ToggleTimer:
					ToggleTimer();
					break;
				case MenuCommand.StopTimer:
					StopTimer();
					break;
				case MenuCommand.BlackoutNow:
					TriggerBlackout();
					break;
				case MenuCommand.OpenSettings:
					host.ShowSettingsWindow();
					break;
				case MenuCommand.Quit:
					Quit();
					break;
			}
		}

		private void OnApplicationQuit()
		{
			ShutDownHost();
		}

		private void OnDestroy()
		{
			ShutDownHost();
		}

		/// <summary>
		/// While listening, appends each new key press. [Ctrl] [Enter] ends the recording, leaving out
		/// that final Ctrl, so the keyboard alone can finish it.
		/// </summary>
		private void PollRecording()
		{
			if (!IsListening || !Input.anyKeyDown)
				return;

			foreach (KeyCode key in KeyCatalogue.UnityKeysToPoll)
			{
				if (!Input.GetKeyDown(key))
					continue;

				KeyCode canonical = KeyCatalogue.Canonical(key);
				bool isControlHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
				if (canonical == KeyCode.Return && isControlHeld)
				{
					if (recordedKeys.Count > 0 && recordedKeys[recordedKeys.Count - 1] == KeyCode.LeftControl)
						recordedKeys.RemoveAt(recordedKeys.Count - 1);
					StopListening();
					return;
				}

				if (recordedKeys.Count < KeySequence.MaxLength)
					recordedKeys.Add(canonical);
			}
		}

		private BlackoutContent BuildBlackoutContent()
		{
			string promptText = settings.IsPromptEnabled ? PromptText : null;
			BlackoutGraphic graphic = settings.IsGraphicEnabled ? GetBlackoutGraphic() : null;
			return new BlackoutContent(promptText, graphic, settings.ExitSequence);
		}

		/// <summary>
		/// The scaled graphic for the native hosts, cached until the file setting changes.
		/// </summary>
		private BlackoutGraphic GetBlackoutGraphic()
		{
			string path = GraphicLoader.ResolvePath(settings.GraphicPath);
			if (blackoutGraphic != null && path == blackoutGraphicPath && host.GraphicMaxPixels == blackoutGraphicMaxPixels)
				return blackoutGraphic;

			blackoutGraphicPath = path;
			blackoutGraphicMaxPixels = host.GraphicMaxPixels;
			blackoutGraphic = GraphicLoader.Load(path, blackoutGraphicMaxPixels);
			return blackoutGraphic;
		}

		private void RefreshGraphicPreview()
		{
			string path = GraphicLoader.ResolvePath(settings.GraphicPath);
			if (path == graphicPreviewPath)
				return;

			if (graphicPreview != null)
				Destroy(graphicPreview);

			graphicPreviewPath = path;
			graphicPreview = GraphicLoader.LoadTexture(path, out graphicPreviewError);
		}

		private void ShutDownHost()
		{
			if (isShutDown || host == null)
				return;

			isShutDown = true;
			host.Shutdown();
#if UNITY_EDITOR
			UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ShutDownHost;
#endif
		}
	}
}
