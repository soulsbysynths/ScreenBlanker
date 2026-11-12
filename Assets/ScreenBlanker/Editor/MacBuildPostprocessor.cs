using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ScreenBlanker.EditorTools
{
	/// <summary>
	/// Finishes a macOS build: marks the app as a menu-bar agent in Info.plist and drops a script
	/// beside it that must be run once on the Mac (a build made on Windows loses its executable bit and
	/// needs re-signing after the plist edit).
	/// </summary>
	public sealed class MacBuildPostprocessor : IPostprocessBuildWithReport
	{
		private const string FinaliseScriptName = "finalise-mac-build.sh";
		private const string DmgScriptName = "make-dmg.sh";

		// LSUIElement: no Dock icon and no app menu from the moment of launch.
		// NSAppSleepDisabled: keep App Nap from throttling the timers (also requested at runtime).
		private const string ExtraPlistEntries =
			"\t<key>LSUIElement</key>\n"
			+ "\t<true/>\n"
			+ "\t<key>NSAppSleepDisabled</key>\n"
			+ "\t<true/>\n";

		/// <inheritdoc/>
		public int callbackOrder => 100;

		/// <inheritdoc/>
		public void OnPostprocessBuild(BuildReport report)
		{
			if (report.summary.platform != BuildTarget.StandaloneOSX)
				return;

			string appPath = report.summary.outputPath;
			PatchInfoPlist(Path.Combine(appPath, "Contents", "Info.plist"));
			WriteFinaliseScript(appPath);
			WriteDmgScript(appPath);
		}

		private static void PatchInfoPlist(string plistPath)
		{
			if (!File.Exists(plistPath))
			{
				Debug.LogWarning("ScreenBlanker: Info.plist not found at " + plistPath);
				return;
			}

			string plist = File.ReadAllText(plistPath);
			if (plist.Contains("<key>LSUIElement</key>"))
				return;

			int closingDict = plist.LastIndexOf("</dict>", System.StringComparison.Ordinal);
			if (closingDict < 0)
			{
				Debug.LogWarning("ScreenBlanker: unexpected Info.plist format; LSUIElement not added.");
				return;
			}

			// Insert at the start of the "</dict>" line so the new keys line up with the existing ones.
			int lineStart = plist.LastIndexOf('\n', closingDict) + 1;
			plist = plist.Insert(lineStart, ExtraPlistEntries);
			File.WriteAllText(plistPath, plist);
		}

		private static void WriteFinaliseScript(string appPath)
		{
			string appName = Path.GetFileName(appPath);
			string script = "#!/bin/bash\n"
				+ "# Run once on the Mac after copying the build across from the PC:\n"
				+ "#   bash " + FinaliseScriptName + "\n"
				+ "# Restores the executable bit (lost on NTFS), clears the download quarantine, and ad-hoc\n"
				+ "# signs the app (required on Apple silicon, and the Info.plist edit invalidates Unity's signature).\n"
				+ "set -e\n"
				+ "cd \"$(dirname \"$0\")\"\n"
				+ "APP=\"" + appName + "\"\n"
				+ "chmod +x \"$APP\"/Contents/MacOS/*\n"
				+ "xattr -cr \"$APP\"\n"
				+ "codesign --force --deep --sign - \"$APP\"\n"
				+ "echo \"Done. Move $APP to /Applications (optional) and open it.\"\n";

			WriteScript(appPath, FinaliseScriptName, script);
		}

		/// <summary>
		/// DMGs can only be made reliably on a Mac (hdiutil), and the app must be signed there first
		/// anyway, so the build ships a script that does both.
		/// </summary>
		private static void WriteDmgScript(string appPath)
		{
			string appName = Path.GetFileName(appPath);
			string volumeName = PlayerSettings.productName;
			string dmgName = volumeName + "-" + PlayerSettings.bundleVersion + ".dmg";
			string script = "#!/bin/bash\n"
				+ "# Run on the Mac after copying this folder across from the PC:\n"
				+ "#   bash " + DmgScriptName + "\n"
				+ "# Signs the app (via " + FinaliseScriptName + "), then packages it as " + dmgName + ",\n"
				+ "# a compressed disk image with the usual drag-to-Applications layout.\n"
				+ "set -e\n"
				+ "cd \"$(dirname \"$0\")\"\n"
				+ "APP=\"" + appName + "\"\n"
				+ "DMG=\"" + dmgName + "\"\n"
				+ "bash " + FinaliseScriptName + "\n"
				+ "STAGING=\"$(mktemp -d)\"\n"
				+ "trap 'rm -rf \"$STAGING\"' EXIT\n"
				+ "# ditto keeps the code signature and permissions intact.\n"
				+ "ditto \"$APP\" \"$STAGING/$APP\"\n"
				+ "ln -s /Applications \"$STAGING/Applications\"\n"
				+ "hdiutil create -volname \"" + volumeName + "\" -srcfolder \"$STAGING\" -fs HFS+ -format UDZO -ov \"$DMG\"\n"
				+ "echo \"Created $(pwd)/$DMG\"\n";

			WriteScript(appPath, DmgScriptName, script);
		}

		private static void WriteScript(string appPath, string fileName, string script)
		{
			string scriptPath = Path.Combine(Path.GetDirectoryName(appPath), fileName);
			File.WriteAllText(scriptPath, script.Replace("\r\n", "\n"));
		}
	}
}
