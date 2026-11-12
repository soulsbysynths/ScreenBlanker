using System;
using System.Globalization;

namespace ScreenBlanker
{
	/// <summary>
	/// Turns times and durations into the short strings used in the menu and settings window.
	/// </summary>
	public static class StatusFormatter
	{
		/// <summary>
		/// Formats as hh:mm:ss, rounding partial seconds up so the display reads 00:00:01 until the
		/// timer actually expires.
		/// </summary>
		public static string Duration(TimeSpan duration)
		{
			int totalSeconds = (int)Math.Ceiling(Math.Max(0.0, duration.TotalSeconds));
			return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", totalSeconds / 3600, (totalSeconds / 60) % 60, totalSeconds % 60);
		}

		/// <summary>
		/// Compact form for the menu bar: "4:59" under an hour, "1:04:59" above.
		/// </summary>
		public static string CompactDuration(TimeSpan duration)
		{
			int totalSeconds = (int)Math.Ceiling(Math.Max(0.0, duration.TotalSeconds));
			int hours = totalSeconds / 3600;
			int minutes = (totalSeconds / 60) % 60;
			int seconds = totalSeconds % 60;
			if (hours > 0)
				return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}:{2:00}", hours, minutes, seconds);
			return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", minutes, seconds);
		}

		/// <summary>
		/// "Today 22:00", "Tomorrow 07:30", or a weekday name further out.
		/// </summary>
		public static string NextOccurrence(DateTime nextLocal)
		{
			DateTime today = DateTime.Now.Date;
			string time = nextLocal.ToString("HH:mm", CultureInfo.InvariantCulture);
			if (nextLocal.Date == today)
				return "Today " + time;
			if (nextLocal.Date == today.AddDays(1))
				return "Tomorrow " + time;
			return nextLocal.ToString("ddd", CultureInfo.InvariantCulture) + " " + time;
		}

		/// <summary>
		/// Rough "in 3 h 12 min" phrase for how far away a time is.
		/// </summary>
		public static string TimeUntil(DateTime nextLocal)
		{
			TimeSpan until = nextLocal - DateTime.Now;
			int totalMinutes = (int)Math.Ceiling(Math.Max(0.0, until.TotalMinutes));
			if (totalMinutes < 60)
				return "in " + totalMinutes.ToString(CultureInfo.InvariantCulture) + " min";
			return string.Format(CultureInfo.InvariantCulture, "in {0} h {1} min", totalMinutes / 60, totalMinutes % 60);
		}
	}
}
