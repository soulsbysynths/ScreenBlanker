using System;

namespace ScreenBlanker
{
	/// <summary>
	/// Wall-clock countdown. Works from absolute UTC end times rather than frame deltas, so it stays
	/// accurate when the OS throttles a background app or the machine sleeps.
	/// </summary>
	public sealed class CountdownTimer
	{
		private DateTime endUtc;
		private TimeSpan pausedRemaining;

		/// <summary>
		/// Countdown state.
		/// </summary>
		public enum TimerState
		{
			Stopped,
			Running,
			Paused,
		}

		/// <summary>
		/// Current state of the countdown.
		/// </summary>
		public TimerState State { get; private set; } = TimerState.Stopped;

		/// <summary>
		/// True while counting down or paused part-way.
		/// </summary>
		public bool IsArmed => State != TimerState.Stopped;

		/// <summary>
		/// Time left. Zero when stopped.
		/// </summary>
		public TimeSpan Remaining
		{
			get
			{
				if (State == TimerState.Paused)
					return pausedRemaining;
				if (State != TimerState.Running)
					return TimeSpan.Zero;

				TimeSpan left = endUtc - DateTime.UtcNow;
				return left < TimeSpan.Zero ? TimeSpan.Zero : left;
			}
		}

		/// <summary>
		/// Starts a fresh countdown.
		/// </summary>
		/// <param name="totalSeconds">Length of the countdown. Zero or less is ignored.</param>
		public void Start(int totalSeconds)
		{
			if (totalSeconds <= 0)
				return;

			endUtc = DateTime.UtcNow.AddSeconds(totalSeconds);
			State = TimerState.Running;
		}

		/// <summary>
		/// Freezes the countdown where it is.
		/// </summary>
		public void Pause()
		{
			if (State != TimerState.Running)
				return;

			pausedRemaining = Remaining;
			State = TimerState.Paused;
		}

		/// <summary>
		/// Continues a paused countdown.
		/// </summary>
		public void Resume()
		{
			if (State != TimerState.Paused)
				return;

			endUtc = DateTime.UtcNow + pausedRemaining;
			State = TimerState.Running;
		}

		/// <summary>
		/// Cancels the countdown.
		/// </summary>
		public void Stop()
		{
			State = TimerState.Stopped;
		}

		/// <summary>
		/// Polled once per frame. Returns true exactly once, on the tick the countdown reaches zero,
		/// after which the timer is stopped.
		/// </summary>
		public bool PollExpired()
		{
			if (State != TimerState.Running)
				return false;
			if (DateTime.UtcNow < endUtc)
				return false;

			State = TimerState.Stopped;
			return true;
		}
	}
}
