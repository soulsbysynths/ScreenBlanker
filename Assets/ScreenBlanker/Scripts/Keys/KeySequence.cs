using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScreenBlanker.Keys
{
	/// <summary>
	/// An ordered list of key presses that dismisses the blackout, e.g. [Ctrl] [Shift] Q or E X I T.
	/// Immutable; keys are stored in canonical form (see <see cref="KeyCatalogue"/>).
	/// </summary>
	public sealed class KeySequence
	{
		/// <summary>
		/// Longest sequence that can be recorded.
		/// </summary>
		public const int MaxLength = 10;

		private readonly KeyCode[] keys;

		/// <summary>
		/// Creates a sequence, dropping keys the catalogue does not support.
		/// </summary>
		public KeySequence(IEnumerable<KeyCode> keys)
		{
			List<KeyCode> canonical = new List<KeyCode>();
			foreach (KeyCode key in keys)
			{
				KeyCode mapped = KeyCatalogue.Canonical(key);
				if (mapped != KeyCode.None && canonical.Count < MaxLength)
					canonical.Add(mapped);
			}

			this.keys = canonical.ToArray();
		}

		/// <summary>
		/// The default: a single press of Escape.
		/// </summary>
		public static KeySequence Default => new KeySequence(new[] { KeyCode.Escape });

		/// <summary>
		/// The keys in order.
		/// </summary>
		public IReadOnlyList<KeyCode> Keys => keys;

		/// <summary>
		/// Number of key presses in the sequence.
		/// </summary>
		public int Length => keys.Length;

		/// <summary>
		/// Parses the form written by <see cref="Serialise"/>, falling back to <see cref="Default"/>
		/// for anything empty or unreadable.
		/// </summary>
		public static KeySequence Parse(string text)
		{
			if (string.IsNullOrWhiteSpace(text))
				return Default;

			List<KeyCode> parsed = new List<KeyCode>();
			foreach (string part in text.Split(','))
			{
				if (Enum.TryParse(part.Trim(), out KeyCode key))
					parsed.Add(key);
			}

			KeySequence sequence = new KeySequence(parsed);
			return sequence.Length > 0 ? sequence : Default;
		}

		/// <summary>
		/// Comma-separated Unity key names, for saving.
		/// </summary>
		public string Serialise()
		{
			return string.Join(",", Array.ConvertAll(keys, key => key.ToString()));
		}

		/// <summary>
		/// Human-readable form, e.g. "[Ctrl] [Shift] Q". Control keys are bracketed, letters and digits
		/// are not.
		/// </summary>
		public string ToDisplayString()
		{
			return string.Join(" ", Array.ConvertAll(keys, KeyCatalogue.DisplayName));
		}

		/// <summary>
		/// Creates a fresh matcher for this sequence.
		/// </summary>
		public Matcher CreateMatcher()
		{
			return new Matcher(keys);
		}

		/// <summary>
		/// Follows key presses as they arrive and reports when the whole sequence has been typed.
		/// A wrong key restarts the sequence (and counts as its first key if it matches it).
		/// </summary>
		public sealed class Matcher
		{
			private readonly KeyCode[] keys;
			private int progress;

			/// <summary>
			/// Creates a matcher for the given canonical keys.
			/// </summary>
			public Matcher(KeyCode[] keys)
			{
				this.keys = keys;
			}

			/// <summary>
			/// Feeds one key press (not repeats). Returns true when it completes the sequence.
			/// </summary>
			public bool Feed(KeyCode key)
			{
				key = KeyCatalogue.Canonical(key);
				if (key == KeyCode.None || keys.Length == 0)
					return false;

				if (key == keys[progress])
					++progress;
				else
					progress = key == keys[0] ? 1 : 0;

				if (progress < keys.Length)
					return false;

				progress = 0;
				return true;
			}

			/// <summary>
			/// Forgets any partly typed sequence.
			/// </summary>
			public void Reset()
			{
				progress = 0;
			}
		}
	}
}
