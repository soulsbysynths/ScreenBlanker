using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ScreenBlanker.EditorTools
{
	/// <summary>
	/// Puts the settings window into the scene: a ScreenBlanker object carrying the UIDocument (which
	/// shows SettingsWindow.uxml in the Game view, even outside Play mode), the app and its view.
	/// Safe to run again; it only adds what is missing and re-points the UI assets.
	/// </summary>
	public static class SceneSetup
	{
		private const string ScenePath = "Assets/Scenes/SampleScene.unity";
		private const string ObjectName = "ScreenBlanker";
		private const string UiFolder = "Assets/ScreenBlanker/UI";
		private const string LayoutPath = UiFolder + "/SettingsWindow.uxml";
		private const string ThemePath = UiFolder + "/ScreenBlankerTheme.tss";
		private const string PanelSettingsPath = UiFolder + "/ScreenBlankerPanelSettings.asset";

		// The layout is designed at this size and scaled to fit the window (Shrink keeps all of it
		// visible whatever the window's shape).
		private static readonly Vector2Int ReferenceResolution = new Vector2Int(860, 620);
		private static readonly Color BackgroundColour = new Color32(0x1C, 0x1D, 0x21, 0xFF);

		/// <summary>
		/// Creates or updates the PanelSettings asset and the ScreenBlanker object in SampleScene.
		/// </summary>
		[MenuItem("ScreenBlanker/Set Up Scene")]
		public static void Apply()
		{
			if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			CreateOrUpdatePanelSettings();
			Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

			// Load the assets only after opening the scene: OpenScene unloads unreferenced objects, which
			// would leave a reference taken earlier pointing at a destroyed object (saved as "None").
			PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
			VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
			if (panelSettings == null || layout == null)
			{
				Debug.LogError("ScreenBlanker: UI assets not found in " + UiFolder);
				return;
			}

			GameObject root = GameObject.Find(ObjectName);
			if (root == null)
				root = new GameObject(ObjectName);

			UIDocument document = GetOrAdd<UIDocument>(root);
			document.panelSettings = panelSettings;
			document.visualTreeAsset = layout;
			GetOrAdd<BlankerApp>(root);
			GetOrAdd<SettingsView>(root);

			EditorUtility.SetDirty(document);
			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			Selection.activeGameObject = root;
			Debug.Log("ScreenBlanker: scene set up. Edit the window in Window > UI Toolkit > UI Builder (open " + LayoutPath + ").");
		}

		private static void CreateOrUpdatePanelSettings()
		{
			PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
			if (panelSettings == null)
			{
				panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
				AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
			}

			panelSettings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
			panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
			panelSettings.referenceResolution = ReferenceResolution;
			panelSettings.screenMatchMode = PanelScreenMatchMode.Shrink;
			panelSettings.clearColor = true;
			panelSettings.colorClearValue = BackgroundColour;
			EditorUtility.SetDirty(panelSettings);
			AssetDatabase.SaveAssets();
		}

		private static T GetOrAdd<T>(GameObject target)
			where T : Component
		{
			T component = target.GetComponent<T>();
			return component != null ? component : target.AddComponent<T>();
		}
	}
}
