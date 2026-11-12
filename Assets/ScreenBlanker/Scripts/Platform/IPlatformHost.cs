using System;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// The OS-specific half of the app: status menu / tray icon, the multi-display blackout,
	/// hiding and showing the settings window, and launch-at-login registration.
	/// All members are called on the Unity main thread.
	/// </summary>
	public interface IPlatformHost
	{
		/// <summary>
		/// Raised when the user picks an entry in the status menu / tray menu.
		/// </summary>
		event Action<MenuCommand> MenuCommandInvoked;

		/// <summary>
		/// Number of physical displays currently attached (mirrored displays count once).
		/// </summary>
		int DisplayCount { get; }

		/// <summary>
		/// True while the blackout is on screen.
		/// </summary>
		bool IsBlackoutVisible { get; }

		/// <summary>
		/// True while the settings window is shown.
		/// </summary>
		bool IsSettingsWindowVisible { get; }

		/// <summary>
		/// True when this host cannot draw native blackout windows and the app must draw the
		/// blackout itself inside the Unity window.
		/// </summary>
		bool IsBlackoutDrawnInApp { get; }

		/// <summary>
		/// Returns false when another copy of the app is already running.
		/// </summary>
		bool TryClaimSingleInstance();

		/// <summary>
		/// Creates the status item / tray icon.
		/// </summary>
		/// <param name="statusProvider">Called whenever the host needs fresh status text, e.g. as
		/// the menu is opened. Must be cheap.</param>
		/// <param name="isStartHidden">True when launched at login: the settings window is hidden
		/// immediately.</param>
		void Initialise(Func<StatusSnapshot> statusProvider, bool isStartHidden);

		/// <summary>
		/// Called every frame.
		/// </summary>
		void Tick();

		/// <summary>
		/// Covers every display in black and takes keyboard focus. Escape dismisses it.
		/// </summary>
		/// <param name="isPromptVisible">Show the "Press Esc" prompt, moved to a new random spot every
		/// second. False leaves the screens plain black.</param>
		void ShowBlackout(bool isPromptVisible);

		/// <summary>
		/// Removes the blackout and hands focus back to whatever had it.
		/// </summary>
		void HideBlackout();

		/// <summary>
		/// Brings the settings window to the front.
		/// </summary>
		void ShowSettingsWindow();

		/// <summary>
		/// Hides the settings window, leaving only the status item.
		/// </summary>
		void HideSettingsWindow();

		/// <summary>
		/// Registers or unregisters the app to start at login. Re-registering refreshes the stored
		/// path, so a moved app still starts.
		/// </summary>
		void ApplyStartAtLogin(bool isEnabled);

		/// <summary>
		/// Removes the status item and all native windows. Safe to call more than once.
		/// </summary>
		void Shutdown();
	}
}
