using System;
using ScreenBlanker.Blackout;
using ScreenBlanker.Keys;
using UnityEngine;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// Fallback for platforms without a native host: no status item, and the blackout is drawn by the
	/// app inside its own (full-screen) Unity window on the main display only.
	/// </summary>
	public sealed class InAppPlatformHost : IPlatformHost
	{
		private KeySequence.Matcher exitMatcher;
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
		public int GraphicMaxPixels => 180;

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
			if (!IsBlackoutVisible || !Input.anyKeyDown)
				return;

			foreach (KeyCode key in KeyCatalogue.UnityKeysToPoll)
			{
				if (Input.GetKeyDown(key) && exitMatcher.Feed(key))
				{
					HideBlackout();
					return;
				}
			}
		}

		/// <inheritdoc/>
		public void ShowBlackout(BlackoutContent content)
		{
			if (IsBlackoutVisible)
				return;

			exitMatcher = content.ExitSequence.CreateMatcher();
			IsBlackoutVisible = true;
			wasFullScreen = Screen.fullScreen;
			Cursor.visible = false;
			if (!Application.isEditor)
				Screen.fullScreen = true;
		}

		/// <inheritdoc/>
		public string ChooseImageFile()
		{
#if UNITY_EDITOR
			string path = UnityEditor.EditorUtility.OpenFilePanel("Choose blackout graphic", string.Empty, "png,jpg,jpeg");
			return string.IsNullOrEmpty(path) ? null : path;
#else
			return null;
#endif
		}

		/// <inheritdoc/>
		public void SetWindowTitle(string title)
		{
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
