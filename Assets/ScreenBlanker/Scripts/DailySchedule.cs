using System;

namespace ScreenBlanker
{
	/// <summary>
	/// Fires once a day at a fixed local time of day.
	/// </summary>
	public sealed class DailySchedule
	{
		/// <summary>
		/// How late a check may run and still count as "on time". Covers background throttling of
		/// the frame loop. A time missed by more than this (machine asleep or app not running) is
		/// skipped rather than fired late.
		/// </summary>
		private static readonly TimeSpan LateTolerance = TimeSpan.FromMinutes(2);

		private int hour;
		private int minute;

		/// <summary>
		/// Whether the schedule is armed.
		/// </summary>
		public bool IsEnabled { get; private set; }

		/// <summary>
		/// The next local time the blackout will fire. Only meaningful while <see cref="IsEnabled"/>.
		/// </summary>
		public DateTime NextLocal { get; private set; }

		/// <summary>
		/// Arms or re-arms the schedule, recalculating the next occurrence from now.
		/// </summary>
		public void Configure(bool isEnabled, int hourOfDay, int minuteOfHour)
		{
			IsEnabled = isEnabled;
			hour = hourOfDay;
			minute = minuteOfHour;
			NextLocal = NextOccurrenceAfter(DateTime.Now);
		}

		/// <summary>
		/// Polled once per frame. Returns true when the scheduled time has just been reached.
		/// </summary>
		public bool PollDue()
		{
			if (!IsEnabled)
				return false;

			DateTime now = DateTime.Now;
			if (now < NextLocal)
				return false;

			bool isOnTime = (now - NextLocal) <= LateTolerance;
			NextLocal = NextOccurrenceAfter(now);
			return isOnTime;
		}

		private DateTime NextOccurrenceAfter(DateTime nowLocal)
		{
			DateTime candidate = nowLocal.Date.AddHours(hour).AddMinutes(minute);
			if (candidate <= nowLocal)
				candidate = candidate.AddDays(1);
			return candidate;
		}
	}
}
