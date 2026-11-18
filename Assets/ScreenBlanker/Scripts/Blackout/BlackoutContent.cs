using ScreenBlanker.Keys;

namespace ScreenBlanker.Blackout
{
	/// <summary>
	/// What a blackout shows and what dismisses it. The prompt text and graphic move together, to a
	/// new random spot every second; either or both may be absent, leaving plain black.
	/// </summary>
	public sealed class BlackoutContent
	{
		/// <summary>
		/// Creates the content.
		/// </summary>
		/// <param name="promptText">Text such as "Press [Ctrl] Q", or null for none.</param>
		/// <param name="graphic">Image shown above the text, or null for none.</param>
		public BlackoutContent(string promptText, BlackoutGraphic graphic, KeySequence exitSequence)
		{
			PromptText = string.IsNullOrEmpty(promptText) ? null : promptText;
			Graphic = graphic;
			ExitSequence = exitSequence ?? KeySequence.Default;
		}

		/// <summary>
		/// Prompt text, or null when the prompt is switched off.
		/// </summary>
		public string PromptText { get; }

		/// <summary>
		/// Graphic, or null when the graphic is switched off.
		/// </summary>
		public BlackoutGraphic Graphic { get; }

		/// <summary>
		/// Key presses that dismiss the blackout.
		/// </summary>
		public KeySequence ExitSequence { get; }

		/// <summary>
		/// True when there is anything to draw on the black.
		/// </summary>
		public bool HasOverlay => PromptText != null || Graphic != null;
	}
}
