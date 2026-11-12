using System;
using UnityEngine;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// Fallback for platforms without a native host: no status item, and the blackout is drawn by the
	/// app inside its own (full-screen) Unity window on the main display only.
	/// </summary>
	public sealed class InAppPlatformHost : IPlatformHost
	{
		private bool wasFullScreen;

		/// <inheritdoc/>
		public event Action<MenuCommand> MenuCommandInvoked
		{
			add { }
			remove { }
		}

		/// <inheritdoc/>
		public int DisplayCount => Display.displays.Length;

		/// <inheritdoc/>
		public bool IsBlackoutVisible { get; private set; }

		/// <inheritdoc/>
		public bool IsSettingsWindowVisible { get; private set; } = true;

		/// <inheritdoc/>
		public bool IsBlackoutDrawnInApp => true;

		/// <inheritdoc/>
		public bool TryClaimSingleInstance()
		{
			return true;
		}

		/// <inheritdoc/>
		public void Initialise(Func<StatusSnapshot> statusProvider, bool isStartHidden)
		{
		}

		/// <inheritdoc/>
		public void Tick()
		{
			if (IsBlackoutVisible && Input.GetKeyDown(KeyCode.Escape))
				HideBlackout();
		}

		/// <inheritdoc/>
		public void ShowBlackout(bool isPromptVisible)
		{
			if (IsBlackoutVisible)
				return;

			IsBlackoutVisible = true;
			wasFullScreen = Screen.fullScreen;
			Cursor.visible = false;
			if (!Application.isEditor)
				Screen.fullScreen = true;
		}

		/// <inheritdoc/>
		public void HideBlackout()
		{
			if (!IsBlackoutVisible)
				return;

			IsBlackoutVisible = false;
			Cursor.visible = true;
			if (!Application.isEditor)
				Screen.fullScreen = wasFullScreen;
		}

		/// <inheritdoc/>
		public void ShowSettingsWindow()
		{
			IsSettingsWindowVisible = true;
		}

		/// <inheritdoc/>
		public void HideSettingsWindow()
		{
			IsSettingsWindowVisible = false;
		}

		/// <inheritdoc/>
		public void ApplyStartAtLogin(bool isEnabled)
		{
		}

		/// <inheritdoc/>
		public void Shutdown()
		{
		}
	}
}
