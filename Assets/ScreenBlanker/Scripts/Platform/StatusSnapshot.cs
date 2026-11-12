namespace ScreenBlanker.Platform
{
	/// <summary>
	/// Everything the status menu / tray icon shows, already formatted for display.
	/// </summary>
	public sealed class StatusSnapshot
	{
		/// <summary>
		/// Short text shown beside the menu-bar icon (e.g. the countdown). Empty for icon only.
		/// </summary>
		public string BarTitle { get; set; } = string.Empty;

		/// <summary>
		/// True when anything is scheduled. The icon is drawn dimmed when nothing is armed.
		/// </summary>
		public bool IsArmed { get; set; }

		/// <summary>
		/// Informational menu line about the countdown.
		/// </summary>
		public string TimerLine { get; set; } = string.Empty;

		/// <summary>
		/// Informational menu line about the next daily blackout.
		/// </summary>
		public string DailyLine { get; set; } = string.Empty;

		/// <summary>
		/// Informational menu line with the number of displays detected.
		/// </summary>
		public string DisplaysLine { get; set; } = string.Empty;

		/// <summary>
		/// Label for the start / pause / resume menu item.
		/// </summary>
		public string ToggleTimerLabel { get; set; } = string.Empty;

		/// <summary>
		/// Whether "Stop timer" is currently meaningful.
		/// </summary>
		public bool IsStopTimerEnabled { get; set; }
	}
}
