using System.Collections.Generic;
using UnityEngine;

namespace ScreenBlanker.Keys
{
	/// <summary>
	/// The keys that can be used in an exit sequence, with their display names and native key codes.
	/// Unity's <see cref="KeyCode"/> is the canonical identity: sequences are recorded from Unity input
	/// in the settings window and matched from native events during a blackout.
	/// </summary>
	/// <remarks>
	/// Left and right modifier keys are treated as one key, so a sequence recorded with left Shift also
	/// accepts right Shift. Native codes were checked against primary sources: Windows VK_ values in the
	/// Windows SDK WinUser.h, macOS kVK_ values in HIToolbox Events.h (MacOSX11.3 SDK), and Unity KeyCode
	/// values in UnityEngine.CoreModule 2022.3.
	/// </remarks>
	public static class KeyCatalogue
	{
		private static readonly List<KeyInfo> Keys = new List<KeyInfo>();
		private static readonly Dictionary<KeyCode, KeyInfo> ByKeyCode = new Dictionary<KeyCode, KeyInfo>();
		private static readonly Dictionary<KeyCode, KeyCode> Aliases = new Dictionary<KeyCode, KeyCode>();
		private static readonly Dictionary<int, KeyCode> ByWindowsVirtualKey = new Dictionary<int, KeyCode>();
		private static readonly Dictionary<int, KeyCode> ByMacKeyCode = new Dictionary<int, KeyCode>();
		private static readonly List<KeyCode> RawUnityKeys = new List<KeyCode>();

		static KeyCatalogue()
		{
			// Modifiers come first, so keys pressed in the same frame are polled modifier-first and a
			// combination such as Ctrl+Q records as [Ctrl] Q. Windows polls the side-neutral VK_SHIFT / VK_CONTROL /
			// VK_MENU; macOS reports left and right keys separately.
			Add(KeyCode.LeftShift, "Shift", null, true, new[] { 0x10 }, new[] { 0x38, 0x3C });
			Add(KeyCode.LeftControl, "Ctrl", "Control", true, new[] { 0x11 }, new[] { 0x3B, 0x3E });
			Add(KeyCode.LeftAlt, "Alt", "Option", true, new[] { 0x12 }, new[] { 0x3A, 0x3D });
			Add(KeyCode.LeftCommand, "Win", "Cmd", true, new[] { 0x5B, 0x5C }, new[] { 0x37, 0x36 });

			AddAlias(KeyCode.RightShift, KeyCode.LeftShift);
			AddAlias(KeyCode.RightControl, KeyCode.LeftControl);
			AddAlias(KeyCode.RightAlt, KeyCode.LeftAlt);
			AddAlias(KeyCode.AltGr, KeyCode.LeftAlt);
			AddAlias(KeyCode.RightCommand, KeyCode.LeftCommand);
			AddAlias(KeyCode.LeftWindows, KeyCode.LeftCommand);
			AddAlias(KeyCode.RightWindows, KeyCode.LeftCommand);

			// Letters: VK_A..VK_Z are ASCII 'A'..'Z'. macOS kVK_ANSI_ codes are not in order.
			int[] macLetters =
			{
				0x00, 0x0B, 0x08, 0x02, 0x0E, 0x03, 0x05, 0x04, 0x22, 0x26, 0x28, 0x25, 0x2E,
				0x2D, 0x1F, 0x23, 0x0C, 0x0F, 0x01, 0x11, 0x20, 0x09, 0x0D, 0x07, 0x10, 0x06,
			};
			for (int i = 0; i < 26; ++i)
				Add(KeyCode.A + i, ((char)('A' + i)).ToString(), null, false, new[] { 'A' + i }, new[] { macLetters[i] });

			// Digits on the main keyboard: VK_0..VK_9 are ASCII '0'..'9'.
			int[] macDigits = { 0x1D, 0x12, 0x13, 0x14, 0x15, 0x17, 0x16, 0x1A, 0x1C, 0x19 };
			for (int i = 0; i < 10; ++i)
				Add(KeyCode.Alpha0 + i, ((char)('0' + i)).ToString(), null, false, new[] { '0' + i }, new[] { macDigits[i] });

			// Function keys: VK_F1..VK_F12 are contiguous from 0x70.
			int[] macFunctionKeys = { 0x7A, 0x78, 0x63, 0x76, 0x60, 0x61, 0x62, 0x64, 0x65, 0x6D, 0x67, 0x6F };
			for (int i = 0; i < 12; ++i)
				Add(KeyCode.F1 + i, "F" + (i + 1), null, true, new[] { 0x70 + i }, new[] { macFunctionKeys[i] });

			Add(KeyCode.Escape, "Esc", null, true, new[] { 0x1B }, new[] { 0x35 });
			Add(KeyCode.Return, "Enter", "Return", true, new[] { 0x0D }, new[] { 0x24 });
			Add(KeyCode.Space, "Space", null, true, new[] { 0x20 }, new[] { 0x31 });
			Add(KeyCode.Tab, "Tab", null, true, new[] { 0x09 }, new[] { 0x30 });
			Add(KeyCode.Backspace, "Backspace", "Delete", true, new[] { 0x08 }, new[] { 0x33 });
			Add(KeyCode.Delete, "Del", "Fwd Delete", true, new[] { 0x2E }, new[] { 0x75 });
			Add(KeyCode.UpArrow, "Up", null, true, new[] { 0x26 }, new[] { 0x7E });
			Add(KeyCode.DownArrow, "Down", null, true, new[] { 0x28 }, new[] { 0x7D });
			Add(KeyCode.LeftArrow, "Left", null, true, new[] { 0x25 }, new[] { 0x7B });
			Add(KeyCode.RightArrow, "Right", null, true, new[] { 0x27 }, new[] { 0x7C });
			Add(KeyCode.Home, "Home", null, true, new[] { 0x24 }, new[] { 0x73 });
			Add(KeyCode.End, "End", null, true, new[] { 0x23 }, new[] { 0x77 });
			Add(KeyCode.PageUp, "Page Up", null, true, new[] { 0x21 }, new[] { 0x74 });
			Add(KeyCode.PageDown, "Page Down", null, true, new[] { 0x22 }, new[] { 0x79 });

			AddAlias(KeyCode.KeypadEnter, KeyCode.Return);
		}

		/// <summary>
		/// Every Unity key code worth polling while recording, aliases included.
		/// </summary>
		public static IReadOnlyList<KeyCode> UnityKeysToPoll => RawUnityKeys;

		/// <summary>
		/// Every Windows virtual-key code in the catalogue, for polling during a blackout.
		/// </summary>
		public static IEnumerable<int> WindowsVirtualKeys => ByWindowsVirtualKey.Keys;

		/// <summary>
		/// Maps any supported key (including right-hand modifiers) to its canonical code.
		/// Returns <see cref="KeyCode.None"/> for keys that cannot be used.
		/// </summary>
		public static KeyCode Canonical(KeyCode key)
		{
			if (Aliases.TryGetValue(key, out KeyCode canonical))
				return canonical;
			return ByKeyCode.ContainsKey(key) ? key : KeyCode.None;
		}

		/// <summary>
		/// Canonical key for a Windows virtual-key code, or <see cref="KeyCode.None"/>.
		/// </summary>
		public static KeyCode FromWindowsVirtualKey(int virtualKey)
		{
			return ByWindowsVirtualKey.TryGetValue(virtualKey, out KeyCode key) ? key : KeyCode.None;
		}

		/// <summary>
		/// Canonical key for a macOS virtual key code (kVK_*), or <see cref="KeyCode.None"/>.
		/// </summary>
		public static KeyCode FromMacKeyCode(int macKeyCode)
		{
			return ByMacKeyCode.TryGetValue(macKeyCode, out KeyCode key) ? key : KeyCode.None;
		}

		/// <summary>
		/// True for keys shown in square brackets: everything except letters and digits.
		/// </summary>
		public static bool IsControlKey(KeyCode key)
		{
			return ByKeyCode.TryGetValue(Canonical(key), out KeyInfo info) && info.IsControl;
		}

		/// <summary>
		/// The key as shown to the user: "Q" or "7" for letters and digits, "[Esc]", "[Ctrl]" etc. for
		/// everything else. Names follow the platform the app is running on ("[Cmd]" vs "[Win]").
		/// </summary>
		public static string DisplayName(KeyCode key)
		{
			if (!ByKeyCode.TryGetValue(Canonical(key), out KeyInfo info))
				return "[?]";

			bool isMac = Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
			string name = isMac && info.MacName != null ? info.MacName : info.Name;
			return info.IsControl ? "[" + name + "]" : name;
		}

		private static void Add(KeyCode key, string name, string macName, bool isControl, int[] windowsVirtualKeys, int[] macKeyCodes)
		{
			KeyInfo info = new KeyInfo(name, macName, isControl);
			Keys.Add(info);
			ByKeyCode[key] = info;
			RawUnityKeys.Add(key);
			foreach (int virtualKey in windowsVirtualKeys)
				ByWindowsVirtualKey[virtualKey] = key;
			foreach (int macKeyCode in macKeyCodes)
				ByMacKeyCode[macKeyCode] = key;
		}

		private static void AddAlias(KeyCode alias, KeyCode canonical)
		{
			Aliases[alias] = canonical;
			RawUnityKeys.Add(alias);
		}

		private sealed class KeyInfo
		{
			public KeyInfo(string name, string macName, bool isControl)
			{
				Name = name;
				MacName = macName;
				IsControl = isControl;
			}

			public string Name { get; }

			public string MacName { get; }

			public bool IsControl { get; }
		}
	}
}
