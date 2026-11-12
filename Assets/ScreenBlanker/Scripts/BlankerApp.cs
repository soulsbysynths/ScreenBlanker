using System;
using ScreenBlanker.Platform;
using UnityEngine;
using UnityEngine.Rendering;

namespace ScreenBlanker
{
	/// <summary>
	/// The app: owns the timers and settings, decides when to black out, and hands the OS-specific
	/// work to an <see cref="IPlatformHost"/>. Created automatically at startup; no scene setup needed.
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
		private readonly SettingsGui gui = new SettingsGui();

		private BlankerSettings settings;
		private IPlatformHost host;
		private bool isShutDown;

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
			host.ShowBlackout(settings.IsPromptEnabled);
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
		private static void Bootstrap()
		{
			GameObject root = new GameObject("ScreenBlanker");
			DontDestroyOnLoad(root);
			root.AddComponent<BlankerApp>();
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

			if (countdown.PollExpired())
				host.ShowBlackout(settings.IsPromptEnabled);

			if (daily.PollDue())
				host.ShowBlackout(settings.IsPromptEnabled);

			bool isOnScreen = host.IsSettingsWindowVisible || host.IsBlackoutVisible || Application.isEditor;
			Application.targetFrameRate = isOnScreen ? ForegroundFrameRate : BackgroundFrameRate;
			OnDemandRendering.renderFrameInterval = isOnScreen ? 1 : BackgroundRenderInterval;
		}

		private void OnGUI()
		{
			if (settings == null)
				return;

			if (host.IsBlackoutDrawnInApp && host.IsBlackoutVisible)
			{
				gui.DrawInAppBlackout(settings.IsPromptEnabled);
				return;
			}

			gui.Draw(this);
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
