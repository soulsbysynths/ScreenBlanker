using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ScreenBlanker.EditorTools
{
	/// <summary>
	/// Puts the player settings the app depends on in place (small fixed window, keeps running in the
	/// background, icons, bundle identifier, universal macOS binary). Runs automatically once when the
	/// project is opened, and on demand from the ScreenBlanker menu.
	/// </summary>
	[InitializeOnLoad]
	public static class ProjectSetup
	{
		/// <summary>
		/// Bundle identifier. Also used as the LaunchAgent label on macOS.
		/// </summary>
		public const string BundleIdentifier = "com.soulsbysynths.screenblanker";

		private const string ProductName = "ScreenBlanker";
		private const string CompanyName = "Soulsby Synths";
		private const string AppIconPath = "Assets/ScreenBlanker/Branding/AppIcon.png";
		private const string AppliedMarkerKey = "ScreenBlanker.ProjectSetup.Applied.v6";

		static ProjectSetup()
		{
			// Deferred: asset loading is not reliable inside an [InitializeOnLoad] constructor.
			EditorApplication.delayCall += ApplyOnce;
		}

		/// <summary>
		/// Applies all player settings. Safe to run repeatedly.
		/// </summary>
		[MenuItem("ScreenBlanker/Apply Project Settings")]
		public static void Apply()
		{
			PlayerSettings.companyName = CompanyName;
			PlayerSettings.productName = ProductName;

			// The version is bumped by every menu build (see BuildVersion); only seed it here.
			if (string.IsNullOrEmpty(PlayerSettings.bundleVersion) || PlayerSettings.bundleVersion == "0.1")
				PlayerSettings.bundleVersion = BuildVersion.InitialVersion;

			PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, BundleIdentifier);

			// A fixed settings window, not a full-screen game: the two-column layout at 1.25x, which
			// leaves room for the taskbar / Dock and menu bar on a 1920x1080 screen.
			PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
			PlayerSettings.defaultScreenWidth = 1075;
			PlayerSettings.defaultScreenHeight = 775;
			PlayerSettings.defaultIsNativeResolution = false;
			PlayerSettings.resizableWindow = false;
			PlayerSettings.allowFullscreenSwitch = false;
			PlayerSettings.macRetinaSupport = true;

			// Timers must keep running while the user works in other apps.
			PlayerSettings.runInBackground = true;
			PlayerSettings.visibleInBackground = true;

			// Windows: a second launch just exits rather than adding a second tray icon.
			PlayerSettings.forceSingleInstance = true;

			// One binary for Intel and Apple-silicon Macs.
			EditorUserBuildSettings.SetPlatformSettings("Standalone", "OSXUniversal", "Architecture", "x64ARM64");

			Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconPath);
			if (icon != null)
				PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
			else
				Debug.LogWarning("ScreenBlanker: app icon not found at " + AppIconPath);

			AssetDatabase.SaveAssets();
			EditorPrefs.SetBool(AppliedMarkerKey + "." + Application.dataPath, true);
			Debug.Log("ScreenBlanker: project settings applied.");
		}

		private static void ApplyOnce()
		{
			if (EditorPrefs.GetBool(AppliedMarkerKey + "." + Application.dataPath, false))
				return;

			Apply();
		}
	}
}
