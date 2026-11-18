using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace ScreenBlanker.EditorTools
{
	/// <summary>
	/// Automatic version numbering: every build from the ScreenBlanker menu bumps the patch number
	/// (1.0.3 becomes 1.0.4). The version lives in ProjectSettings, so it is committed with the project
	/// and shown at runtime as <see cref="Application.version"/>.
	/// </summary>
	public static class BuildVersion
	{
		/// <summary>
		/// Version used when the project has none, or one that is not major.minor.patch.
		/// </summary>
		public const string InitialVersion = "1.0.0";

		/// <summary>
		/// Increments the patch number, saves it, and returns the new version. Also bumps the macOS
		/// build number (CFBundleVersion), which must rise with every release.
		/// </summary>
		public static string Bump()
		{
			string next = Increment(PlayerSettings.bundleVersion);
			PlayerSettings.bundleVersion = next;

			int.TryParse(PlayerSettings.macOS.buildNumber, NumberStyles.None, CultureInfo.InvariantCulture, out int buildNumber);
			PlayerSettings.macOS.buildNumber = (buildNumber + 1).ToString(CultureInfo.InvariantCulture);

			AssetDatabase.SaveAssets();
			Debug.Log("ScreenBlanker: version " + next + " (build " + PlayerSettings.macOS.buildNumber + ")");
			return next;
		}

		/// <summary>
		/// "1.2.3" becomes "1.2.4". Anything unparseable restarts from <see cref="InitialVersion"/>.
		/// </summary>
		public static string Increment(string version)
		{
			string[] parts = (version ?? string.Empty).Split('.');
			if (parts.Length != 3)
				return InitialVersion;

			int[] numbers = new int[3];
			for (int i = 0; i < 3; ++i)
			{
				if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]))
					return InitialVersion;
			}

			return string.Join(".", Array.ConvertAll(new[] { numbers[0], numbers[1], numbers[2] + 1 }, n => n.ToString(CultureInfo.InvariantCulture)));
		}
	}
}
