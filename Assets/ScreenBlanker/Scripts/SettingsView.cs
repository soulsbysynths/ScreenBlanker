using System;
using System.Globalization;
using System.IO;
using ScreenBlanker.Keys;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScreenBlanker
{
	/// <summary>
	/// Drives the settings window. The layout and look live in UI/SettingsWindow.uxml and
	/// UI/SettingsWindow.uss (edit them in Window > UI Toolkit > UI Builder); this only finds the named
	/// elements, wires their callbacks to <see cref="BlankerApp"/>, and refreshes the live text.
	/// </summary>
	[RequireComponent(typeof(UIDocument))]
	[RequireComponent(typeof(BlankerApp))]
	public sealed class SettingsView : MonoBehaviour
	{
		private BlankerApp app;
		private VisualElement root;
		private VisualElement window;
		private VisualElement settingsPanel;
		private VisualElement hiddenPlaceholder;
		private Label versionLabel;
		private Label displayCountLabel;
		private VisualElement countdownEdit;
		private VisualElement countdownRunning;
		private Label countdownTimeLabel;
		private Label countdownStatusLabel;
		private Button countdownToggleButton;
		private Button countdownStopButton;
		private Toggle dailyEnabledToggle;
		private Label dailyNextLabel;
		private Toggle showPromptToggle;
		private Toggle showGraphicToggle;
		private VisualElement graphicPreview;
		private Label graphicNameLabel;
		private Label graphicErrorLabel;
		private Button graphicChooseButton;
		private Button graphicDefaultButton;
		private Label exitKeysLabel;
		private Button listenButton;
		private Button listenCancelButton;
		private Button resetKeysButton;
		private Label keysHintLabel;
		private Toggle startAtLoginToggle;
		private Stepper countdownHours;
		private Stepper countdownMinutes;
		private Stepper countdownSeconds;
		private Stepper dailyHour;
		private Stepper dailyMinute;
		private Texture2D shownPreview;

		private static void SetShown(VisualElement element, bool isShown)
		{
			element.style.display = isShown ? DisplayStyle.Flex : DisplayStyle.None;
		}

		private void Start()
		{
			app = GetComponent<BlankerApp>();
			if (app.Settings == null)
				return;

			root = GetComponent<UIDocument>().rootVisualElement;
			Bind();
		}

		private void Update()
		{
			if (root == null || app.Settings == null)
				return;

			Refresh();
		}

		private void Bind()
		{
			BlankerSettings settings = app.Settings;

			window = Find<VisualElement>("window");
			settingsPanel = Find<VisualElement>("settings");
			hiddenPlaceholder = Find<VisualElement>("hidden-placeholder");
			versionLabel = Find<Label>("version");
			displayCountLabel = Find<Label>("display-count");

			countdownEdit = Find<VisualElement>("countdown-edit");
			countdownRunning = Find<VisualElement>("countdown-running");
			countdownTimeLabel = Find<Label>("countdown-time");
			countdownStatusLabel = Find<Label>("countdown-status");
			countdownToggleButton = Find<Button>("countdown-toggle");
			countdownStopButton = Find<Button>("countdown-stop");
			countdownHours = new Stepper(Find<VisualElement>("countdown-hours"), 99, () => settings.CountdownHours, value => Apply(() => settings.CountdownHours = value));
			countdownMinutes = new Stepper(Find<VisualElement>("countdown-minutes"), 59, () => settings.CountdownMinutes, value => Apply(() => settings.CountdownMinutes = value));
			countdownSeconds = new Stepper(Find<VisualElement>("countdown-seconds"), 59, () => settings.CountdownSeconds, value => Apply(() => settings.CountdownSeconds = value));
			countdownToggleButton.clicked += app.ToggleTimer;
			countdownStopButton.clicked += app.StopTimer;

			dailyEnabledToggle = Find<Toggle>("daily-enabled");
			dailyNextLabel = Find<Label>("daily-next");
			dailyHour = new Stepper(Find<VisualElement>("daily-hour"), 23, () => settings.DailyHour, value => Apply(() => settings.DailyHour = value));
			dailyMinute = new Stepper(Find<VisualElement>("daily-minute"), 59, () => settings.DailyMinute, value => Apply(() => settings.DailyMinute = value));
			dailyEnabledToggle.RegisterValueChangedCallback(evt => Apply(() => settings.IsDailyEnabled = evt.newValue));

			showPromptToggle = Find<Toggle>("show-prompt");
			showGraphicToggle = Find<Toggle>("show-graphic");
			graphicPreview = Find<VisualElement>("graphic-preview");
			graphicNameLabel = Find<Label>("graphic-name");
			graphicErrorLabel = Find<Label>("graphic-error");
			graphicChooseButton = Find<Button>("graphic-choose");
			graphicDefaultButton = Find<Button>("graphic-default");
			showPromptToggle.RegisterValueChangedCallback(evt => Apply(() => settings.IsPromptEnabled = evt.newValue));
			showGraphicToggle.RegisterValueChangedCallback(evt => Apply(() => settings.IsGraphicEnabled = evt.newValue));
			graphicChooseButton.clicked += app.RequestGraphicChoice;
			graphicDefaultButton.clicked += app.UseDefaultGraphic;

			exitKeysLabel = Find<Label>("exit-keys");
			listenButton = Find<Button>("listen");
			listenCancelButton = Find<Button>("listen-cancel");
			resetKeysButton = Find<Button>("reset-keys");
			keysHintLabel = Find<Label>("keys-hint");
			listenButton.clicked += OnListenClicked;
			listenCancelButton.clicked += app.CancelListening;
			resetKeysButton.clicked += app.ResetExitSequence;

			startAtLoginToggle = Find<Toggle>("start-at-login");
			startAtLoginToggle.RegisterValueChangedCallback(evt => Apply(() => settings.IsStartAtLoginEnabled = evt.newValue));
			Find<Button>("test-blackout").clicked += app.TriggerBlackout;
			Find<Button>("hide-window").clicked += () => app.Host.HideSettingsWindow();
			Find<Button>("quit").clicked += app.Quit;
			Find<Button>("show-settings").clicked += () => app.Host.ShowSettingsWindow();

			// While recording, key presses belong to the recorder, not to whichever field or button
			// has focus (Space or Enter would otherwise click a focused button).
			root.RegisterCallback<KeyDownEvent>(SwallowWhileListening, TrickleDown.TrickleDown);
			root.RegisterCallback<NavigationSubmitEvent>(SwallowWhileListening, TrickleDown.TrickleDown);
			root.RegisterCallback<NavigationMoveEvent>(SwallowWhileListening, TrickleDown.TrickleDown);
			root.RegisterCallback<NavigationCancelEvent>(SwallowWhileListening, TrickleDown.TrickleDown);

			versionLabel.text = "v" + Application.version;
		}

		private T Find<T>(string name)
			where T : VisualElement
		{
			T element = root.Q<T>(name);
			if (element == null)
				throw new InvalidOperationException("ScreenBlanker: SettingsWindow.uxml has no " + typeof(T).Name + " named '" + name + "'.");
			return element;
		}

		private void Apply(Action change)
		{
			change();
			app.ApplySettings();
		}

		private void OnListenClicked()
		{
			if (app.IsListening)
				app.StopListening();
			else
				app.StartListening();
		}

		private void SwallowWhileListening(EventBase evt)
		{
			if (!app.IsListening)
				return;

			evt.StopImmediatePropagation();
			evt.PreventDefault();
		}

		private void Refresh()
		{
			BlankerSettings settings = app.Settings;

			// The in-app fallback blackout is drawn over the window with IMGUI; keep the window out of it.
			bool isWindowShown = !(app.Host.IsBlackoutDrawnInApp && app.Host.IsBlackoutVisible);
			SetShown(window, isWindowShown);

			// The editor can't hide its Game view, so show a stand-in for the hidden window instead.
			bool isSettingsShown = app.Host.IsSettingsWindowVisible;
			SetShown(settingsPanel, isSettingsShown);
			SetShown(hiddenPlaceholder, !isSettingsShown);
			if (!isSettingsShown)
				return;

			int displays = app.Host.DisplayCount;
			displayCountLabel.text = displays == 1 ? "1 display" : displays + " displays";

			RefreshCountdown(settings);
			RefreshDaily(settings);
			RefreshBlackout(settings);
			startAtLoginToggle.SetValueWithoutNotify(settings.IsStartAtLoginEnabled);
		}

		private void RefreshCountdown(BlankerSettings settings)
		{
			CountdownTimer countdown = app.Countdown;
			SetShown(countdownEdit, !countdown.IsArmed);
			SetShown(countdownRunning, countdown.IsArmed);

			if (countdown.IsArmed)
			{
				countdownTimeLabel.text = StatusFormatter.Duration(countdown.Remaining);
				countdownStatusLabel.text = countdown.State == CountdownTimer.TimerState.Paused ? "Paused" : "Blackout when this reaches zero";
			}
			else
			{
				countdownHours.Refresh();
				countdownMinutes.Refresh();
				countdownSeconds.Refresh();
			}

			switch (countdown.State)
			{
				case CountdownTimer.TimerState.Running:
					countdownToggleButton.text = "Pause";
					break;
				case CountdownTimer.TimerState.Paused:
					countdownToggleButton.text = "Resume";
					break;
				default:
					countdownToggleButton.text = "Start";
					break;
			}

			countdownToggleButton.SetEnabled(countdown.IsArmed || settings.CountdownTotalSeconds > 0);
			countdownStopButton.SetEnabled(countdown.IsArmed);
		}

		private void RefreshDaily(BlankerSettings settings)
		{
			dailyEnabledToggle.SetValueWithoutNotify(settings.IsDailyEnabled);
			dailyHour.Refresh();
			dailyMinute.Refresh();
			dailyNextLabel.text = app.Daily.IsEnabled
				? "Next blackout: " + StatusFormatter.NextOccurrence(app.Daily.NextLocal) + " (" + StatusFormatter.TimeUntil(app.Daily.NextLocal) + ")"
				: "Off";
		}

		private void RefreshBlackout(BlankerSettings settings)
		{
			showPromptToggle.SetValueWithoutNotify(settings.IsPromptEnabled);
			showPromptToggle.text = "Show prompt: " + app.PromptText;
			showGraphicToggle.SetValueWithoutNotify(settings.IsGraphicEnabled);

			Texture2D preview = app.GraphicPreview;
			if (preview != shownPreview)
			{
				shownPreview = preview;
				graphicPreview.style.backgroundImage = preview != null ? new StyleBackground(preview) : new StyleBackground(StyleKeyword.None);
			}

			graphicNameLabel.text = string.IsNullOrEmpty(settings.GraphicPath) ? "Built-in logo" : Path.GetFileName(settings.GraphicPath);
			string error = app.GraphicPreviewError;
			graphicErrorLabel.text = error ?? string.Empty;
			SetShown(graphicErrorLabel, error != null);
			graphicPreview.SetEnabled(settings.IsGraphicEnabled);
			graphicChooseButton.SetEnabled(settings.IsGraphicEnabled);
			graphicDefaultButton.SetEnabled(settings.IsGraphicEnabled && !string.IsNullOrEmpty(settings.GraphicPath));

			if (app.IsListening)
			{
				// Nothing may hold focus while recording, or it would also act on the keys.
				root.focusController?.focusedElement?.Blur();

				string recorded = app.RecordedKeysDisplay;
				exitKeysLabel.text = recorded.Length > 0 ? recorded : "Listening…";
				listenButton.text = "Stop listening";
				listenButton.AddToClassList("danger");
				keysHintLabel.text = "Type up to " + KeySequence.MaxLength.ToString(CultureInfo.InvariantCulture) + " keys, then " + app.StopListeningKeysDisplay + " or Stop listening.";
			}
			else
			{
				exitKeysLabel.text = settings.ExitSequence.ToDisplayString();
				listenButton.text = "Listen";
				listenButton.RemoveFromClassList("danger");
				keysHintLabel.text = "[Bracketed] keys are control keys.";
			}

			SetShown(listenCancelButton, app.IsListening);
			SetShown(resetKeysButton, !app.IsListening);
			resetKeysButton.SetEnabled(settings.ExitSequence.Length != 1 || settings.ExitSequence.Keys[0] != KeyCode.Escape);
		}

		/// <summary>
		/// One instance of the Stepper.uxml template: a two-digit field with + / - buttons that wrap
		/// round at either end.
		/// </summary>
		private sealed class Stepper
		{
			private readonly TextField field;
			private readonly int max;
			private readonly Func<int> getValue;
			private readonly Action<int> setValue;
			private bool isEditing;

			public Stepper(VisualElement instance, int max, Func<int> getValue, Action<int> setValue)
			{
				this.max = max;
				this.getValue = getValue;
				this.setValue = setValue;

				field = instance.Q<TextField>("value");
				instance.Q<Button>("up").clicked += () => Set(getValue() >= max ? 0 : getValue() + 1);
				instance.Q<Button>("down").clicked += () => Set(getValue() <= 0 ? max : getValue() - 1);
				field.RegisterValueChangedCallback(OnTyped);
				field.RegisterCallback<FocusInEvent>(evt => isEditing = true);
				field.RegisterCallback<FocusOutEvent>(evt =>
				{
					isEditing = false;
					Show(getValue());
				});
			}

			/// <summary>
			/// Shows the current setting, unless the user is part-way through typing in the field.
			/// </summary>
			public void Refresh()
			{
				if (!isEditing)
					Show(getValue());
			}

			private void OnTyped(ChangeEvent<string> evt)
			{
				if (int.TryParse(evt.newValue, NumberStyles.None, CultureInfo.InvariantCulture, out int typed))
					Set(Mathf.Clamp(typed, 0, max), false);
				else if (evt.newValue.Length == 0)
					Set(0, false);
			}

			private void Set(int value, bool isShown = true)
			{
				if (value != getValue())
					setValue(value);
				if (isShown)
					Show(value);
			}

			private void Show(int value)
			{
				field.SetValueWithoutNotify(value.ToString("00", CultureInfo.InvariantCulture));
			}
		}
	}
}
