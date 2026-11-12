using System.Globalization;
using UnityEngine;

namespace ScreenBlanker
{
	/// <summary>
	/// The settings window, drawn with IMGUI so it needs no scene or prefab assets. Laid out at a fixed
	/// design size and scaled to fit the window, which also takes care of Retina / high-DPI screens.
	/// </summary>
	public sealed class SettingsGui
	{
		private const float DesignWidth = 420.0f;
		private const float DesignHeight = 640.0f;
		private const float Padding = 16.0f;
		private const float CardGap = 12.0f;

		private static readonly Color BackgroundColour = new Color32(0x1C, 0x1D, 0x21, 0xFF);
		private static readonly Color CardColour = new Color32(0x2A, 0x2C, 0x32, 0xFF);
		private static readonly Color FieldColour = new Color32(0x1C, 0x1D, 0x21, 0xFF);
		private static readonly Color TextColour = new Color32(0xEE, 0xEE, 0xF0, 0xFF);
		private static readonly Color MutedTextColour = new Color32(0x9A, 0x9C, 0xA4, 0xFF);
		private static readonly Color GreenColour = new Color32(0x00, 0xA6, 0x57, 0xFF);
		private static readonly Color RedColour = new Color32(0xF0, 0x2B, 0x2C, 0xFF);
		private static readonly Color NeutralColour = new Color32(0x44, 0x47, 0x50, 0xFF);

		private bool areStylesReady;
		private Texture2D backgroundTexture;
		private Texture2D cardTexture;
		private GUIStyle titleStyle;
		private GUIStyle headingStyle;
		private GUIStyle labelStyle;
		private GUIStyle mutedStyle;
		private GUIStyle bigTimeStyle;
		private GUIStyle separatorStyle;
		private GUIStyle cardStyle;
		private GUIStyle fieldStyle;
		private GUIStyle stepperStyle;
		private GUIStyle toggleStyle;
		private GUIStyle primaryButtonStyle;
		private GUIStyle dangerButtonStyle;
		private GUIStyle neutralButtonStyle;
		private GUIStyle promptStyle;
		private GUIStyle unitStyle;
		private float measuredContentHeight;
		private float promptMoveTime;
		private Vector2 promptPosition;

		/// <summary>
		/// Draws the whole settings window. Call from OnGUI.
		/// </summary>
		public void Draw(BlankerApp app)
		{
			EnsureStyles();

			// Fit whichever is larger: the design height or the height the layout actually needed last
			// frame. So nothing is ever clipped, whatever the window or Game view size or font metrics.
			float neededHeight = Mathf.Max(DesignHeight, measuredContentHeight + (2.0f * Padding));
			float scale = Mathf.Min(Screen.width / DesignWidth, Screen.height / neededHeight);
			GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1.0f));
			float width = Screen.width / scale;
			float height = Screen.height / scale;
			GUI.DrawTexture(new Rect(0.0f, 0.0f, width, height), backgroundTexture);

			// The editor can't hide its Game view, so show a stand-in for the hidden window instead.
			if (!app.Host.IsSettingsWindowVisible)
			{
				DrawHiddenPlaceholder(app, width, height);
				return;
			}

			GUILayout.BeginArea(new Rect(Padding, Padding, width - (2.0f * Padding), height - (2.0f * Padding)));
			DrawHeader(app);
			GUILayout.Space(CardGap);
			DrawCountdownCard(app);
			GUILayout.Space(CardGap);
			DrawDailyCard(app);
			GUILayout.Space(CardGap);
			DrawGeneralCard(app);

			// Layout rects are only final during Repaint.
			if (Event.current.type == EventType.Repaint)
				measuredContentHeight = GUILayoutUtility.GetLastRect().yMax;

			GUILayout.EndArea();
		}

		/// <summary>
		/// Full-window blackout for hosts that cannot create native windows. Call from OnGUI.
		/// </summary>
		public void DrawInAppBlackout(bool isPromptVisible)
		{
			EnsureStyles();
			GUI.matrix = Matrix4x4.identity;
			GUI.color = Color.black;
			GUI.DrawTexture(new Rect(0.0f, 0.0f, Screen.width, Screen.height), Texture2D.whiteTexture);
			GUI.color = Color.white;

			if (!isPromptVisible)
				return;

			if (Time.unscaledTime - promptMoveTime >= 1.0f)
			{
				promptMoveTime = Time.unscaledTime;
				promptPosition = new Vector2(Random.Range(0.0f, Mathf.Max(0.0f, Screen.width - 120.0f)), Random.Range(0.0f, Mathf.Max(0.0f, Screen.height - 30.0f)));
			}

			GUI.Label(new Rect(promptPosition.x, promptPosition.y, 120.0f, 30.0f), "Press Esc", promptStyle);
		}

		private static Texture2D SolidTexture(Color colour)
		{
			Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
			texture.SetPixel(0, 0, colour);
			texture.Apply();
			texture.hideFlags = HideFlags.HideAndDontSave;
			return texture;
		}

		private static GUIStyle ButtonStyle(Color colour)
		{
			GUIStyle style = new GUIStyle(GUI.skin.button);
			style.normal.background = SolidTexture(colour);
			style.hover.background = SolidTexture(Color.Lerp(colour, Color.white, 0.15f));
			style.active.background = SolidTexture(Color.Lerp(colour, Color.black, 0.2f));
			style.normal.textColor = Color.white;
			style.hover.textColor = Color.white;
			style.active.textColor = Color.white;
			style.border = new RectOffset(0, 0, 0, 0);
			style.fontSize = 14;
			style.fontStyle = FontStyle.Bold;
			style.fixedHeight = 34.0f;
			style.margin = new RectOffset(4, 4, 4, 4);
			return style;
		}

		private static GUIStyle TextStyle(int size, Color colour, FontStyle fontStyle)
		{
			GUIStyle style = new GUIStyle(GUI.skin.label);
			style.fontSize = size;
			style.fontStyle = fontStyle;
			style.normal.textColor = colour;
			style.wordWrap = false;
			return style;
		}

		private void EnsureStyles()
		{
			if (areStylesReady)
				return;

			areStylesReady = true;
			backgroundTexture = SolidTexture(BackgroundColour);
			cardTexture = SolidTexture(CardColour);

			titleStyle = TextStyle(22, TextColour, FontStyle.Bold);
			headingStyle = TextStyle(15, TextColour, FontStyle.Bold);
			labelStyle = TextStyle(14, TextColour, FontStyle.Normal);
			mutedStyle = TextStyle(13, MutedTextColour, FontStyle.Normal);
			mutedStyle.wordWrap = true;
			unitStyle = TextStyle(13, MutedTextColour, FontStyle.Normal);
			unitStyle.alignment = TextAnchor.UpperCenter;
			bigTimeStyle = TextStyle(46, TextColour, FontStyle.Bold);
			bigTimeStyle.alignment = TextAnchor.MiddleCenter;
			separatorStyle = TextStyle(30, MutedTextColour, FontStyle.Bold);
			separatorStyle.alignment = TextAnchor.MiddleCenter;
			promptStyle = TextStyle(16, new Color(0.55f, 0.55f, 0.55f), FontStyle.Normal);

			cardStyle = new GUIStyle();
			cardStyle.normal.background = cardTexture;
			cardStyle.padding = new RectOffset(14, 14, 12, 12);

			fieldStyle = new GUIStyle(GUI.skin.textField);
			fieldStyle.normal.background = SolidTexture(FieldColour);
			fieldStyle.focused.background = SolidTexture(Color.Lerp(FieldColour, GreenColour, 0.25f));
			fieldStyle.hover.background = fieldStyle.normal.background;
			fieldStyle.normal.textColor = TextColour;
			fieldStyle.focused.textColor = TextColour;
			fieldStyle.hover.textColor = TextColour;
			fieldStyle.border = new RectOffset(0, 0, 0, 0);
			fieldStyle.fontSize = 30;
			fieldStyle.fontStyle = FontStyle.Bold;
			fieldStyle.alignment = TextAnchor.MiddleCenter;
			fieldStyle.fixedHeight = 48.0f;

			stepperStyle = ButtonStyle(NeutralColour);
			stepperStyle.fixedHeight = 22.0f;
			stepperStyle.fontSize = 14;
			stepperStyle.margin = new RectOffset(0, 0, 2, 2);

			toggleStyle = new GUIStyle(GUI.skin.toggle);
			toggleStyle.fontSize = 14;
			toggleStyle.normal.textColor = TextColour;
			toggleStyle.onNormal.textColor = TextColour;
			toggleStyle.hover.textColor = TextColour;
			toggleStyle.onHover.textColor = TextColour;
			toggleStyle.active.textColor = TextColour;
			toggleStyle.onActive.textColor = TextColour;

			primaryButtonStyle = ButtonStyle(GreenColour);
			dangerButtonStyle = ButtonStyle(RedColour);
			neutralButtonStyle = ButtonStyle(NeutralColour);
		}

		private void DrawHiddenPlaceholder(BlankerApp app, float width, float height)
		{
			GUILayout.BeginArea(new Rect(Padding, Padding, width - (2.0f * Padding), height - (2.0f * Padding)));
			GUILayout.Label("Settings window hidden", headingStyle);
			GUILayout.Label("In a build the window is gone and only the status item remains. This placeholder is editor-only.", mutedStyle);
			if (GUILayout.Button("Show settings", neutralButtonStyle))
				app.Host.ShowSettingsWindow();
			GUILayout.EndArea();
		}

		private void DrawHeader(BlankerApp app)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("ScreenBlanker", titleStyle);
			GUILayout.FlexibleSpace();
			int displays = app.Host.DisplayCount;
			GUILayout.Label(displays == 1 ? "1 display" : displays + " displays", unitStyle, GUILayout.ExpandWidth(false));
			GUILayout.EndHorizontal();
		}

		private void DrawCountdownCard(BlankerApp app)
		{
			BlankerSettings settings = app.Settings;
			CountdownTimer countdown = app.Countdown;

			GUILayout.BeginVertical(cardStyle);
			GUILayout.Label("Countdown", headingStyle);

			if (countdown.IsArmed)
			{
				GUILayout.Label(StatusFormatter.Duration(countdown.Remaining), bigTimeStyle, GUILayout.Height(80.0f));
				GUILayout.Label(countdown.State == CountdownTimer.TimerState.Paused ? "Paused" : "Blackout when this reaches zero", mutedStyle);
			}
			else
			{
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				int hours = Stepper(settings.CountdownHours, 99, "h");
				Separator();
				int minutes = Stepper(settings.CountdownMinutes, 59, "m");
				Separator();
				int seconds = Stepper(settings.CountdownSeconds, 59, "s");
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				if (hours != settings.CountdownHours || minutes != settings.CountdownMinutes || seconds != settings.CountdownSeconds)
				{
					settings.CountdownHours = hours;
					settings.CountdownMinutes = minutes;
					settings.CountdownSeconds = seconds;
					app.ApplySettings();
				}
			}

			GUILayout.Space(6.0f);
			GUILayout.BeginHorizontal();
			string toggleLabel;
			switch (countdown.State)
			{
				case CountdownTimer.TimerState.Running:
					toggleLabel = "Pause";
					break;
				case CountdownTimer.TimerState.Paused:
					toggleLabel = "Resume";
					break;
				default:
					toggleLabel = "Start";
					break;
			}

			GUI.enabled = countdown.IsArmed || settings.CountdownTotalSeconds > 0;
			if (GUILayout.Button(toggleLabel, primaryButtonStyle))
				app.ToggleTimer();

			GUI.enabled = countdown.IsArmed;
			if (GUILayout.Button("Stop", dangerButtonStyle))
				app.StopTimer();

			GUI.enabled = true;
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		private void DrawDailyCard(BlankerApp app)
		{
			BlankerSettings settings = app.Settings;

			GUILayout.BeginVertical(cardStyle);
			GUILayout.Label("Daily blackout", headingStyle);

			bool isEnabled = GUILayout.Toggle(settings.IsDailyEnabled, " Black out every day at this time", toggleStyle);

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			int hour = Stepper(settings.DailyHour, 23, "h");
			Separator();
			int minute = Stepper(settings.DailyMinute, 59, "m");
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			if (isEnabled != settings.IsDailyEnabled || hour != settings.DailyHour || minute != settings.DailyMinute)
			{
				settings.IsDailyEnabled = isEnabled;
				settings.DailyHour = hour;
				settings.DailyMinute = minute;
				app.ApplySettings();
			}

			string next = app.Daily.IsEnabled
				? "Next blackout: " + StatusFormatter.NextOccurrence(app.Daily.NextLocal) + " (" + StatusFormatter.TimeUntil(app.Daily.NextLocal) + ")"
				: "Off";
			GUILayout.Label(next, mutedStyle);
			GUILayout.EndVertical();
		}

		private void DrawGeneralCard(BlankerApp app)
		{
			BlankerSettings settings = app.Settings;

			GUILayout.BeginVertical(cardStyle);
			bool isStartAtLogin = GUILayout.Toggle(settings.IsStartAtLoginEnabled, " Start at login (keeps the daily blackout active)", toggleStyle);
			if (isStartAtLogin != settings.IsStartAtLoginEnabled)
			{
				settings.IsStartAtLoginEnabled = isStartAtLogin;
				app.ApplySettings();
			}

			bool isPromptEnabled = GUILayout.Toggle(settings.IsPromptEnabled, " Show moving \"Press Esc\" prompt on the blackout", toggleStyle);
			if (isPromptEnabled != settings.IsPromptEnabled)
			{
				settings.IsPromptEnabled = isPromptEnabled;
				app.ApplySettings();
			}

			GUILayout.Space(6.0f);
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Test blackout", neutralButtonStyle))
				app.TriggerBlackout();
			if (GUILayout.Button("Hide window", neutralButtonStyle))
				app.Host.HideSettingsWindow();
			if (GUILayout.Button("Quit", neutralButtonStyle))
				app.Quit();
			GUILayout.EndHorizontal();
			GUILayout.EndVertical();
		}

		private void Separator()
		{
			GUILayout.Label(":", separatorStyle, GUILayout.Width(18.0f), GUILayout.Height(96.0f));
		}

		/// <summary>
		/// Two-digit number field with up / down buttons. Wraps round at either end.
		/// </summary>
		private int Stepper(int value, int max, string unit)
		{
			GUILayout.BeginVertical(GUILayout.Width(72.0f));

			if (GUILayout.Button("+", stepperStyle))
				value = value >= max ? 0 : value + 1;

			string text = GUILayout.TextField(value.ToString("00", CultureInfo.InvariantCulture), 2, fieldStyle);
			if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int typed))
				value = Mathf.Clamp(typed, 0, max);
			else if (text.Length == 0)
				value = 0;

			if (GUILayout.Button("-", stepperStyle))
				value = value <= 0 ? max : value - 1;

			GUILayout.Label(unit, unitStyle);
			GUILayout.EndVertical();
			return value;
		}
	}
}
