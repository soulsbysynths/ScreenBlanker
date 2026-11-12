using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

namespace ScreenBlanker.EditorTools
{
	/// <summary>
	/// One-click builds into the project's Builds folder.
	/// </summary>
	public static class BuildMenu
	{
		private const string WindowsExePath = "Builds/Windows/ScreenBlanker.exe";
		private const string InstallerScriptPath = "Installer/ScreenBlanker.iss";
		private const string InstallerOutputDirectory = "Builds/Installer";

		private static readonly string[] InnoCompilerPaths =
		{
			@"C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
			@"C:\Program Files\Inno Setup 6\ISCC.exe",
		};

		/// <summary>
		/// Builds the universal macOS app.
		/// </summary>
		[MenuItem("ScreenBlanker/Build macOS")]
		public static void BuildMac()
		{
			Build(BuildTarget.StandaloneOSX, "Builds/macOS/ScreenBlanker.app");
		}

		/// <summary>
		/// Builds the 64-bit Windows app.
		/// </summary>
		[MenuItem("ScreenBlanker/Build Windows")]
		public static void BuildWindows()
		{
			Build(BuildTarget.StandaloneWindows64, WindowsExePath);
		}

		/// <summary>
		/// Builds the Windows app, then packages it into a setup exe with Inno Setup 6.
		/// </summary>
		[MenuItem("ScreenBlanker/Build Windows Installer")]
		public static void BuildWindowsInstaller()
		{
			if (!Build(BuildTarget.StandaloneWindows64, WindowsExePath))
				return;

			string compiler = FindInnoCompiler();
			if (compiler == null)
			{
				Debug.LogError("ScreenBlanker: Inno Setup 6 (ISCC.exe) not found. Install it from jrsoftware.org.");
				return;
			}

			ProcessStartInfo startInfo = new ProcessStartInfo
			{
				FileName = compiler,
				Arguments = "/Q /DAppVersion=" + PlayerSettings.bundleVersion + " \"" + Path.GetFullPath(InstallerScriptPath) + "\"",
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				CreateNoWindow = true,
			};

			using (Process process = Process.Start(startInfo))
			{
				string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
				process.WaitForExit();
				if (process.ExitCode != 0)
				{
					Debug.LogError("ScreenBlanker: installer build failed (ISCC exit " + process.ExitCode + ")\n" + output);
					return;
				}
			}

			Debug.Log("ScreenBlanker: built installer in " + InstallerOutputDirectory);
			Reveal(InstallerOutputDirectory);
		}

		private static bool Build(BuildTarget target, string outputPath)
		{
			ProjectSetup.Apply();

			List<string> scenes = new List<string>();
			foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
			{
				if (scene.enabled)
					scenes.Add(scene.path);
			}

			BuildPlayerOptions options = new BuildPlayerOptions
			{
				scenes = scenes.ToArray(),
				locationPathName = outputPath,
				target = target,
				targetGroup = BuildTargetGroup.Standalone,
				options = BuildOptions.None,
			};

			BuildReport report = BuildPipeline.BuildPlayer(options);
			if (report.summary.result != BuildResult.Succeeded)
			{
				Debug.LogError("ScreenBlanker: " + target + " build " + report.summary.result);
				return false;
			}

			Debug.Log("ScreenBlanker: built " + outputPath);
			Reveal(outputPath);
			return true;
		}

		private static string FindInnoCompiler()
		{
			foreach (string path in InnoCompilerPaths)
			{
				if (File.Exists(path))
					return path;
			}

			return null;
		}

		private static void Reveal(string path)
		{
			// Don't pop up Explorer / Finder windows during command-line builds.
			if (!UnityEngine.Application.isBatchMode)
				EditorUtility.RevealInFinder(Path.GetFullPath(path));
		}
	}
}
