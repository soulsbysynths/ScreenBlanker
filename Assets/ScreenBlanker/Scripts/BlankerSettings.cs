using UnityEngine;

namespace ScreenBlanker
{
	/// <summary>
	/// User settings persisted between runs via <see cref="PlayerPrefs"/>.
	/// </summary>
	public sealed class BlankerSettings
	{
		private const string KeyCountdownHours = "countdown.hours";
		private const string KeyCountdownMinutes = "countdown.minutes";
		private const string KeyCountdownSeconds = "countdown.seconds";
		private const string KeyDailyEnabled = "daily.enabled";
		private const string KeyDailyHour = "daily.hour";
		private const string KeyDailyMinute = "daily.minute";
		private const string KeyStartAtLogin = "startAtLogin";
		private const string KeyPromptEnabled = "blackout.showPrompt";

		/// <summary>
		/// Countdown length, hours part (0-99).
		/// </summary>
		public int CountdownHours { get; set; }

		/// <summary>
		/// Countdown length, minutes part (0-59).
		/// </summary>
		public int CountdownMinutes { get; set; }

		/// <summary>
		/// Countdown length, seconds part (0-59).
		/// </summary>
		public int CountdownSeconds { get; set; }

		/// <summary>
		/// Whether the daily blackout is armed.
		/// </summary>
		public bool IsDailyEnabled { get; set; }

		/// <summary>
		/// Local-time hour of the daily blackout (0-23).
		/// </summary>
		public int DailyHour { get; set; }

		/// <summary>
		/// Local-time minute of the daily blackout (0-59).
		/// </summary>
		public int DailyMinute { get; set; }

		/// <summary>
		/// Whether the app registers itself to launch when the user logs in.
		/// </summary>
		public bool IsStartAtLoginEnabled { get; set; }

		/// <summary>
		/// Whether the blackout shows the wandering "Press Esc" prompt. Off gives plain black.
		/// </summary>
		public bool IsPromptEnabled { get; set; }

		/// <summary>
		/// Total countdown length in seconds.
		/// </summary>
		public int CountdownTotalSeconds => (CountdownHours * 3600) + (CountdownMinutes * 60) + CountdownSeconds;

		/// <summary>
		/// Loads settings, falling back to defaults for anything never saved.
		/// </summary>
		public static BlankerSettings Load()
		{
			BlankerSettings settings = new BlankerSettings();
			settings.CountdownHours = PlayerPrefs.GetInt(KeyCountdownHours, 0);
			settings.CountdownMinutes = PlayerPrefs.GetInt(KeyCountdownMinutes, 25);
			settings.CountdownSeconds = PlayerPrefs.GetInt(KeyCountdownSeconds, 0);
			settings.IsDailyEnabled = PlayerPrefs.GetInt(KeyDailyEnabled, 0) != 0;
			settings.DailyHour = PlayerPrefs.GetInt(KeyDailyHour, 22);
			settings.DailyMinute = PlayerPrefs.GetInt(KeyDailyMinute, 0);
			settings.IsStartAtLoginEnabled = PlayerPrefs.GetInt(KeyStartAtLogin, 1) != 0;
			settings.IsPromptEnabled = PlayerPrefs.GetInt(KeyPromptEnabled, 1) != 0;
			settings.Clamp();
			return settings;
		}

		/// <summary>
		/// Writes all settings to disk immediately.
		/// </summary>
		public void Save()
		{
			Clamp();
			PlayerPrefs.SetInt(KeyCountdownHours, CountdownHours);
			PlayerPrefs.SetInt(KeyCountdownMinutes, CountdownMinutes);
			PlayerPrefs.SetInt(KeyCountdownSeconds, CountdownSeconds);
			PlayerPrefs.SetInt(KeyDailyEnabled, IsDailyEnabled ? 1 : 0);
			PlayerPrefs.SetInt(KeyDailyHour, DailyHour);
			PlayerPrefs.SetInt(KeyDailyMinute, DailyMinute);
			PlayerPrefs.SetInt(KeyStartAtLogin, IsStartAtLoginEnabled ? 1 : 0);
			PlayerPrefs.SetInt(KeyPromptEnabled, IsPromptEnabled ? 1 : 0);
			PlayerPrefs.Save();
		}

		private void Clamp()
		{
			CountdownHours = Mathf.Clamp(CountdownHours, 0, 99);
			CountdownMinutes = Mathf.Clamp(CountdownMinutes, 0, 59);
			CountdownSeconds = Mathf.Clamp(CountdownSeconds, 0, 59);
			DailyHour = Mathf.Clamp(DailyHour, 0, 23);
			DailyMinute = Mathf.Clamp(DailyMinute, 0, 59);
		}
	}
}
