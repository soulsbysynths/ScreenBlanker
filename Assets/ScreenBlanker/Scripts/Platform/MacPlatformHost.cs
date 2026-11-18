using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using AOT;
using ScreenBlanker.Blackout;
using ScreenBlanker.Keys;
using UnityEngine;
using static ScreenBlanker.Platform.MacNative;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// macOS host: an NSStatusItem in the menu bar, one borderless black NSWindow per display for the
	/// blackout, and a LaunchAgent for start-at-login. Everything is driven through the Objective-C
	/// runtime (see <see cref="MacNative"/>).
	/// </summary>
	public sealed class MacPlatformHost : IPlatformHost
	{
		private const string BlackoutWindowClassName = "SBBlackoutWindow";
		private const string MenuTargetClassName = "SBMenuTarget";
		private const string LaunchAgentLabel = "com.soulsbysynths.screenblanker";
		private const string StatusIconFileName = "StatusIcon.png";
		private const double PromptHeight = 24.0;
		private const double PromptFontSize = 16.0;
		private const double GroupGap = 12.0;
		private const double StatusIconPoints = 18.0;

		// The graphic is shown at up to 180 points, rendered from 2x pixels for Retina displays.
		private const int GraphicPixelsPerPoint = 2;
		private const int GraphicDisplayPoints = 180;
		private const ulong EventTypeKeyDown = 10;
		private const long ImageScaleProportionallyUpOrDown = 3;
		private const long ModalResponseOk = 1;
		private const ulong ModifierFlagShift = 1UL << 17;
		private const ulong ModifierFlagControl = 1UL << 18;
		private const ulong ModifierFlagOption = 1UL << 19;
		private const ulong ModifierFlagCommand = 1UL << 20;
		private const long ActivationPolicyAccessory = 1;
		private const ulong WindowStyleBorderless = 0;
		private const ulong WindowStyleClosable = 1UL << 1;
		private const ulong WindowStyleMiniaturisable = 1UL << 2;
		private const ulong BackingStoreBuffered = 2;
		private const long ImagePositionLeft = 2;

		// kCGScreenSaverWindowLevel: above the menu bar, Dock and full-screen apps.
		private const long BlackoutWindowLevel = 1000;

		// CanJoinAllSpaces | Stationary | IgnoresCycle | FullScreenAuxiliary
		private const long BlackoutCollectionBehaviour = (1 << 0) | (1 << 4) | (1 << 6) | (1 << 8);

		// NSActivityUserInitiatedAllowingIdleSystemSleep: stops App Nap throttling the frame loop
		// (which would delay the blackout) without keeping the machine awake.
		private const ulong ActivityUserInitiatedAllowingIdleSystemSleep = 0x00FFFFFFUL & ~(1UL << 20);

		// Callbacks are static (required for IL2CPP) and reach the live host through this.
		private static MacPlatformHost current;
		private static ObjectArgImp keyDownImp;
		private static ObjectArgImp flagsChangedImp;
		private static ObjectArgImp ignoreImp;
		private static BoolObjectArgImp keyEquivalentImp;
		private static ObjectArgImp menuActionImp;
		private static ObjectArgImp menuWillOpenImp;
		private static BoolReturnImp yesImp;

		private readonly bool isEditor;
		private readonly List<BlackoutWindow> blackoutWindows = new List<BlackoutWindow>();
		private readonly List<IntPtr> windowsPendingRelease = new List<IntPtr>();
		private readonly System.Random random = new System.Random();
		private readonly Dictionary<MenuCommand, IntPtr> commandItems = new Dictionary<MenuCommand, IntPtr>();

		private Func<StatusSnapshot> statusProvider;
		private IntPtr application;
		private IntPtr unityWindow;
		private IntPtr statusItem;
		private IntPtr statusButton;
		private IntPtr menu;
		private IntPtr menuTarget;
		private IntPtr activityToken;
		private IntPtr timerInfoItem;
		private IntPtr dailyInfoItem;
		private IntPtr displaysInfoItem;
		private IntPtr blackoutImage;
		private BlackoutContent content;
		private KeySequence.Matcher exitMatcher;
		private double groupWidth;
		private double groupHeight;
		private double graphicPointWidth;
		private double graphicPointHeight;
		private string lastBarTitle;
		private bool? wasArmed;
		private bool wasActive;
		private float lastSelfActivationTime = -100.0f;
		private float lastPromptMoveTime;
		private float lastStatusRefreshTime;
		private int blackoutDisplayCount;
		private bool isInitialised;
		private bool isCursorHidden;

		/// <summary>
		/// Creates the host. Nothing native is touched until <see cref="Initialise"/>.
		/// </summary>
		public MacPlatformHost()
		{
			isEditor = Application.isEditor;
		}

		/// <inheritdoc/>
		public event Action<MenuCommand> MenuCommandInvoked;

		/// <inheritdoc/>
		public int DisplayCount => GetDisplayBounds().Count;

		/// <inheritdoc/>
		public bool IsBlackoutVisible { get; private set; }

		/// <inheritdoc/>
		public bool IsSettingsWindowVisible { get; private set; } = true;

		/// <inheritdoc/>
		public bool IsBlackoutDrawnInApp => false;

		/// <inheritdoc/>
		public int GraphicMaxPixels => GraphicDisplayPoints * GraphicPixelsPerPoint;

		/// <inheritdoc/>
		public bool TryClaimSingleInstance()
		{
			if (isEditor)
				return true;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				IntPtr bundleId = Send(SendClass("NSBundle", "mainBundle"), Sel("bundleIdentifier"));
				if (bundleId == IntPtr.Zero)
					return true;

				IntPtr running = Send(GetClass("NSRunningApplication"), Sel("runningApplicationsWithBundleIdentifier:"), bundleId);
				return SendULong(running, Sel("count")) <= 1;
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void Initialise(Func<StatusSnapshot> statusProvider, bool isStartHidden)
		{
			this.statusProvider = statusProvider;
			current = this;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				application = SendClass("NSApplication", "sharedApplication");

				// Find Unity's window before anything of ours (status bar window, blackout windows)
				// is added to [NSApp windows].
				unityWindow = FindUnityWindow();

				if (!isEditor)
				{
					// No Dock icon, no app menu. LSUIElement in Info.plist does the same from launch;
					// this covers builds where the plist was not patched.
					Send(application, Sel("setActivationPolicy:"), ActivationPolicyAccessory);

					if (unityWindow != IntPtr.Zero)
					{
						// Closing Unity's only window quits the player, so take the close and minimise
						// buttons away. The window is hidden from the UI or the menu instead.
						ulong styleMask = SendULong(unityWindow, Sel("styleMask"));
						styleMask &= ~(WindowStyleClosable | WindowStyleMiniaturisable);
						Send(unityWindow, Sel("setStyleMask:"), (long)styleMask);
					}
				}

				IntPtr processInfo = SendClass("NSProcessInfo", "processInfo");
				activityToken = Send(processInfo, Sel("beginActivityWithOptions:reason:"), ActivityUserInitiatedAllowingIdleSystemSleep, NSString("Blackout timers must fire on time"));
				Send(activityToken, Sel("retain"));

				RegisterClasses();
				menuTarget = Send(Send(GetClass(MenuTargetClassName), Sel("alloc")), Sel("init"));
				CreateStatusItem();
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}

			isInitialised = true;
			wasActive = SendBool(application, Sel("isActive")) != No;

			if (isStartHidden)
				HideSettingsWindow();

			RefreshStatus();
		}

		/// <inheritdoc/>
		public void Tick()
		{
			if (!isInitialised)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				ReleasePendingWindows();

				float now = Time.realtimeSinceStartup;
				if (now - lastStatusRefreshTime >= 1.0f)
				{
					lastStatusRefreshTime = now;
					RefreshStatus();
				}

				if (IsBlackoutVisible)
					EnsureCursorHidden();

				if (IsBlackoutVisible && now - lastPromptMoveTime >= 1.0f)
				{
					lastPromptMoveTime = now;
					TickBlackout();
				}

				DetectReopen(now);
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void ShowBlackout(BlackoutContent content)
		{
			if (!isInitialised || IsBlackoutVisible)
				return;

			this.content = content;
			exitMatcher = content.ExitSequence.CreateMatcher();
			IntPtr pool = AutoreleasePoolPush();
			try
			{
				CreateBlackoutImage(content.Graphic);
				MeasureGroup();
				CreateBlackoutWindows();
				IsBlackoutVisible = true;
				lastPromptMoveTime = Time.realtimeSinceStartup;
				TakeFocus();
				EnsureCursorHidden();
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void HideBlackout()
		{
			if (!IsBlackoutVisible)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				// May run inside a blackout window's own keyDown:, so only order the windows out
				// here; they are released on the next Tick, once that call has returned.
				foreach (BlackoutWindow blackout in blackoutWindows)
				{
					Send(blackout.Window, Sel("orderOut:"), IntPtr.Zero);
					windowsPendingRelease.Add(blackout.Window);
				}

				blackoutWindows.Clear();
				IsBlackoutVisible = false;
				ReleaseBlackoutImage();

				// [NSCursor hide] calls are counted, so undo exactly the one we made.
				if (isCursorHidden)
				{
					SendClass("NSCursor", "unhide");
					isCursorHidden = false;
				}

				// Give focus back to whatever the user was working in.
				if (!IsSettingsWindowVisible && !isEditor)
					Send(application, Sel("hide:"), IntPtr.Zero);
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public string ChooseImageFile()
		{
#if UNITY_EDITOR
			string chosen = UnityEditor.EditorUtility.OpenFilePanel("Choose blackout graphic", string.Empty, "png,jpg,jpeg");
			return string.IsNullOrEmpty(chosen) ? null : chosen;
#else
			IntPtr pool = AutoreleasePoolPush();
			try
			{
				IntPtr types = SendClass("NSMutableArray", "array");
				foreach (string type in new[] { "png", "jpg", "jpeg" })
					Send(types, Sel("addObject:"), NSString(type));

				IntPtr panel = SendClass("NSOpenPanel", "openPanel");
				Send(panel, Sel("setCanChooseFiles:"), Yes);
				Send(panel, Sel("setCanChooseDirectories:"), No);
				Send(panel, Sel("setAllowsMultipleSelection:"), No);
				Send(panel, Sel("setAllowedFileTypes:"), types);
				Send(panel, Sel("setMessage:"), NSString("Choose a PNG or JPEG image for the blackout"));

				// An accessory app's panel opens behind other apps unless the app is active.
				ActivateApplication();
				if ((long)Send(panel, Sel("runModal")) != ModalResponseOk)
					return null;

				string path = FromNSString(Send(Send(panel, Sel("URL")), Sel("path")));
				return string.IsNullOrEmpty(path) ? null : path;
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
#endif
		}

		/// <inheritdoc/>
		public void SetWindowTitle(string title)
		{
			if (isEditor || unityWindow == IntPtr.Zero)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				Send(unityWindow, Sel("setTitle:"), NSString(title));
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void ShowSettingsWindow()
		{
			IsSettingsWindowVisible = true;
			if (isEditor || unityWindow == IntPtr.Zero)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				Send(application, Sel("unhide:"), IntPtr.Zero);
				ActivateApplication();
				Send(unityWindow, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void HideSettingsWindow()
		{
			IsSettingsWindowVisible = false;
			if (isEditor || unityWindow == IntPtr.Zero)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				Send(unityWindow, Sel("orderOut:"), IntPtr.Zero);
				if (!IsBlackoutVisible)
					Send(application, Sel("hide:"), IntPtr.Zero);
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <inheritdoc/>
		public void ApplyStartAtLogin(bool isEnabled)
		{
			// In the editor the "app" is the Unity editor itself, which must never be registered.
			if (isEditor)
				return;

			string agentsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "LaunchAgents");
			string plistPath = Path.Combine(agentsDirectory, LaunchAgentLabel + ".plist");

			try
			{
				if (!isEnabled)
				{
					if (File.Exists(plistPath))
						File.Delete(plistPath);
					return;
				}

				string executablePath = GetExecutablePath();
				if (string.IsNullOrEmpty(executablePath))
					return;

				Directory.CreateDirectory(agentsDirectory);
				File.WriteAllText(plistPath, BuildLaunchAgentPlist(executablePath));
			}
			catch (Exception exception)
			{
				Debug.LogWarning("ScreenBlanker: could not update login item: " + exception.Message);
			}
		}

		/// <inheritdoc/>
		public void Shutdown()
		{
			if (!isInitialised)
				return;

			isInitialised = false;
			IntPtr pool = AutoreleasePoolPush();
			try
			{
				HideBlackout();
				ReleasePendingWindows();

				if (statusItem != IntPtr.Zero)
				{
					Send(SendClass("NSStatusBar", "systemStatusBar"), Sel("removeStatusItem:"), statusItem);
					Send(statusItem, Sel("release"));
					statusItem = IntPtr.Zero;
				}

				if (menu != IntPtr.Zero)
				{
					Send(menu, Sel("release"));
					menu = IntPtr.Zero;
				}

				if (menuTarget != IntPtr.Zero)
				{
					Send(menuTarget, Sel("release"));
					menuTarget = IntPtr.Zero;
				}

				if (activityToken != IntPtr.Zero)
				{
					Send(SendClass("NSProcessInfo", "processInfo"), Sel("endActivity:"), activityToken);
					Send(activityToken, Sel("release"));
					activityToken = IntPtr.Zero;
				}
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}

			if (current == this)
				current = null;
		}

		private static string BuildLaunchAgentPlist(string executablePath)
		{
			return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
				+ "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n"
				+ "<plist version=\"1.0\">\n"
				+ "<dict>\n"
				+ "\t<key>Label</key>\n"
				+ "\t<string>" + LaunchAgentLabel + "</string>\n"
				+ "\t<key>ProgramArguments</key>\n"
				+ "\t<array>\n"
				+ "\t\t<string>" + SecurityElement.Escape(executablePath) + "</string>\n"
				+ "\t\t<string>" + BlankerApp.HiddenArgument + "</string>\n"
				+ "\t</array>\n"
				+ "\t<key>RunAtLoad</key>\n"
				+ "\t<true/>\n"
				+ "\t<key>LimitLoadToSessionType</key>\n"
				+ "\t<string>Aqua</string>\n"
				+ "\t<key>ProcessType</key>\n"
				+ "\t<string>Interactive</string>\n"
				+ "</dict>\n"
				+ "</plist>\n";
		}

		private static string GetExecutablePath()
		{
			IntPtr pool = AutoreleasePoolPush();
			try
			{
				return FromNSString(Send(SendClass("NSBundle", "mainBundle"), Sel("executablePath")));
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		/// <summary>
		/// Bounds of each physical display in Cocoa global coordinates (points, origin at the bottom
		/// left of the menu-bar display). Mirrored displays are reported once.
		/// </summary>
		private static List<CGRect> GetDisplayBounds()
		{
			List<CGRect> result = new List<CGRect>();
			uint[] displays = new uint[32];
			if (CGGetActiveDisplayList((uint)displays.Length, displays, out uint count) != 0)
				return result;

			// CoreGraphics uses a top-left origin with y pointing down; Cocoa flips that about the
			// height of the main display.
			double mainHeight = CGDisplayBounds(CGMainDisplayID()).Height;
			for (int i = 0; i < count; ++i)
			{
				if (CGDisplayMirrorsDisplay(displays[i]) != 0)
					continue;

				CGRect bounds = CGDisplayBounds(displays[i]);
				result.Add(new CGRect(bounds.X, mainHeight - (bounds.Y + bounds.Height), bounds.Width, bounds.Height));
			}

			return result;
		}

		private static void RegisterClasses()
		{
			// Delegates are kept in static fields so the GC can never collect a callback that AppKit
			// still holds a pointer to.
			keyDownImp = OnBlackoutKeyDown;
			flagsChangedImp = OnBlackoutFlagsChanged;
			ignoreImp = IgnoreAction;
			keyEquivalentImp = OnBlackoutKeyEquivalent;
			menuActionImp = OnMenuAction;
			menuWillOpenImp = OnMenuWillOpen;
			yesImp = ReturnYes;

			// A borderless NSWindow refuses key status, so it would never see key presses.
			// keyDown: gets ordinary keys, flagsChanged: modifier keys on their own, and
			// performKeyEquivalent: Command / Control combinations, which it swallows so shortcuts such
			// as Cmd+Q or Cmd+H cannot get round the blackout.
			IntPtr windowClass = GetOrCreateClass(BlackoutWindowClassName, "NSWindow");
			ReplaceMethod(windowClass, Sel("canBecomeKeyWindow"), Marshal.GetFunctionPointerForDelegate(yesImp), "c@:");
			ReplaceMethod(windowClass, Sel("canBecomeMainWindow"), Marshal.GetFunctionPointerForDelegate(yesImp), "c@:");
			ReplaceMethod(windowClass, Sel("keyDown:"), Marshal.GetFunctionPointerForDelegate(keyDownImp), "v@:@");
			ReplaceMethod(windowClass, Sel("flagsChanged:"), Marshal.GetFunctionPointerForDelegate(flagsChangedImp), "v@:@");
			ReplaceMethod(windowClass, Sel("performKeyEquivalent:"), Marshal.GetFunctionPointerForDelegate(keyEquivalentImp), "c@:@");
			ReplaceMethod(windowClass, Sel("cancelOperation:"), Marshal.GetFunctionPointerForDelegate(ignoreImp), "v@:@");

			IntPtr targetClass = GetOrCreateClass(MenuTargetClassName, "NSObject");
			ReplaceMethod(targetClass, Sel("menuAction:"), Marshal.GetFunctionPointerForDelegate(menuActionImp), "v@:@");
			ReplaceMethod(targetClass, Sel("menuWillOpen:"), Marshal.GetFunctionPointerForDelegate(menuWillOpenImp), "v@:@");
		}

		[MonoPInvokeCallback(typeof(BoolReturnImp))]
		private static byte ReturnYes(IntPtr self, IntPtr cmd)
		{
			return Yes;
		}

		[MonoPInvokeCallback(typeof(ObjectArgImp))]
		private static void OnBlackoutKeyDown(IntPtr self, IntPtr cmd, IntPtr nsEvent)
		{
			if (current == null || nsEvent == IntPtr.Zero)
				return;
			if (SendBool(nsEvent, Sel("isARepeat")) != No)
				return;

			current.FeedKey(KeyCatalogue.FromMacKeyCode(SendUShort(nsEvent, Sel("keyCode"))));
		}

		[MonoPInvokeCallback(typeof(BoolObjectArgImp))]
		private static byte OnBlackoutKeyEquivalent(IntPtr self, IntPtr cmd, IntPtr nsEvent)
		{
			if (current == null || nsEvent == IntPtr.Zero)
				return No;
			if (SendULong(nsEvent, Sel("type")) != EventTypeKeyDown)
				return No;

			// Returning YES consumes the event, so keyDown: is not called for it as well.
			OnBlackoutKeyDown(self, cmd, nsEvent);
			return Yes;
		}

		/// <summary>
		/// A modifier key went down or up. The event only says which key changed, so it counts as a
		/// press when that modifier's flag is now set.
		/// </summary>
		[MonoPInvokeCallback(typeof(ObjectArgImp))]
		private static void OnBlackoutFlagsChanged(IntPtr self, IntPtr cmd, IntPtr nsEvent)
		{
			if (current == null || nsEvent == IntPtr.Zero)
				return;

			KeyCode key = KeyCatalogue.FromMacKeyCode(SendUShort(nsEvent, Sel("keyCode")));
			ulong flags = SendULong(nsEvent, Sel("modifierFlags"));
			if ((flags & ModifierFlagFor(key)) != 0)
				current.FeedKey(key);
		}

		[MonoPInvokeCallback(typeof(ObjectArgImp))]
		private static void IgnoreAction(IntPtr self, IntPtr cmd, IntPtr sender)
		{
		}

		private static ulong ModifierFlagFor(KeyCode key)
		{
			switch (key)
			{
				case KeyCode.LeftShift:
					return ModifierFlagShift;
				case KeyCode.LeftControl:
					return ModifierFlagControl;
				case KeyCode.LeftAlt:
					return ModifierFlagOption;
				case KeyCode.LeftCommand:
					return ModifierFlagCommand;
				default:
					return 0;
			}
		}

		/// <summary>
		/// NSTextAlignmentCenter is 2 on Intel Macs but 1 on Apple silicon (AppKit's NSText.h switches
		/// on TARGET_ABI_USES_IOS_VALUES, which is true for every CPU other than x86_64).
		/// </summary>
		private static long TextAlignmentCentre()
		{
			return RuntimeInformation.ProcessArchitecture == Architecture.X64 ? 2 : 1;
		}

		[MonoPInvokeCallback(typeof(ObjectArgImp))]
		private static void OnMenuAction(IntPtr self, IntPtr cmd, IntPtr sender)
		{
			if (current == null)
				return;

			MenuCommand command = (MenuCommand)(long)Send(sender, Sel("tag"));
			current.MenuCommandInvoked?.Invoke(command);
			current.RefreshStatus();
		}

		[MonoPInvokeCallback(typeof(ObjectArgImp))]
		private static void OnMenuWillOpen(IntPtr self, IntPtr cmd, IntPtr openingMenu)
		{
			// The Unity frame loop is paused while a menu is tracking, so refresh now rather than
			// showing text up to a second stale.
			current?.RefreshStatus();
		}

		private IntPtr FindUnityWindow()
		{
			IntPtr mainWindow = Send(application, Sel("mainWindow"));
			if (mainWindow != IntPtr.Zero)
				return mainWindow;

			IntPtr windows = Send(application, Sel("windows"));
			ulong count = SendULong(windows, Sel("count"));
			for (ulong i = 0; i < count; ++i)
			{
				IntPtr window = Send(windows, Sel("objectAtIndex:"), (long)i);
				if (SendBool(window, Sel("canBecomeMainWindow")) != No)
					return window;
			}

			return IntPtr.Zero;
		}

		private void CreateStatusItem()
		{
			IntPtr statusBar = SendClass("NSStatusBar", "systemStatusBar");
			statusItem = Send(statusBar, Sel("statusItemWithLength:"), -1.0);
			Send(statusItem, Sel("retain"));
			statusButton = Send(statusItem, Sel("button"));

			string iconPath = Path.Combine(Application.streamingAssetsPath, StatusIconFileName);
			IntPtr image = Send(Send(GetClass("NSImage"), Sel("alloc")), Sel("initWithContentsOfFile:"), NSString(iconPath));
			if (image != IntPtr.Zero)
			{
				Send(image, Sel("setSize:"), new CGSize(StatusIconPoints, StatusIconPoints));

				// Template images are recoloured by macOS to suit light / dark menu bars.
				Send(image, Sel("setTemplate:"), Yes);
				Send(statusButton, Sel("setImage:"), image);
				Send(statusButton, Sel("setImagePosition:"), ImagePositionLeft);
				Send(image, Sel("release"));
			}
			else
			{
				Send(statusButton, Sel("setTitle:"), NSString("SB"));
			}

			IntPtr font = Send(GetClass("NSFont"), Sel("monospacedDigitSystemFontOfSize:weight:"), 0.0, 0.0);
			Send(statusButton, Sel("setFont:"), font);

			menu = Send(Send(GetClass("NSMenu"), Sel("alloc")), Sel("initWithTitle:"), NSString(string.Empty));
			Send(menu, Sel("setAutoenablesItems:"), No);
			Send(menu, Sel("setDelegate:"), menuTarget);

			timerInfoItem = AddInfoItem();
			dailyInfoItem = AddInfoItem();
			displaysInfoItem = AddInfoItem();
			AddSeparator();
			AddCommandItem(MenuCommand.ToggleTimer, "Start timer");
			AddCommandItem(MenuCommand.StopTimer, "Stop timer");
			AddCommandItem(MenuCommand.BlackoutNow, "Blackout now");
			AddSeparator();
			AddCommandItem(MenuCommand.OpenSettings, "Settings…");
			AddSeparator();
			AddCommandItem(MenuCommand.Quit, "Quit ScreenBlanker");

			Send(statusItem, Sel("setMenu:"), menu);
		}

		private IntPtr AddInfoItem()
		{
			IntPtr item = NewMenuItem(string.Empty, IntPtr.Zero);
			Send(item, Sel("setEnabled:"), No);
			Send(menu, Sel("addItem:"), item);
			Send(item, Sel("release"));
			return item;
		}

		private void AddCommandItem(MenuCommand command, string title)
		{
			IntPtr item = NewMenuItem(title, Sel("menuAction:"));
			Send(item, Sel("setTarget:"), menuTarget);
			Send(item, Sel("setTag:"), (long)command);
			Send(menu, Sel("addItem:"), item);
			Send(item, Sel("release"));
			commandItems[command] = item;
		}

		private void AddSeparator()
		{
			Send(menu, Sel("addItem:"), SendClass("NSMenuItem", "separatorItem"));
		}

		private IntPtr NewMenuItem(string title, IntPtr action)
		{
			IntPtr item = Send(GetClass("NSMenuItem"), Sel("alloc"));
			return Send(item, Sel("initWithTitle:action:keyEquivalent:"), NSString(title), action, NSString(string.Empty));
		}

		private void RefreshStatus()
		{
			if (statusItem == IntPtr.Zero || statusProvider == null)
				return;

			IntPtr pool = AutoreleasePoolPush();
			try
			{
				StatusSnapshot status = statusProvider();

				// Only touch the menu bar when something changed: setting the title relayouts the bar.
				if (status.BarTitle != lastBarTitle)
				{
					lastBarTitle = status.BarTitle;
					string title = string.IsNullOrEmpty(status.BarTitle) ? string.Empty : " " + status.BarTitle;
					Send(statusButton, Sel("setTitle:"), NSString(title));
				}

				if (status.IsArmed != wasArmed)
				{
					wasArmed = status.IsArmed;
					Send(statusButton, Sel("setAppearsDisabled:"), status.IsArmed ? No : Yes);
				}

				SetItemTitle(timerInfoItem, status.TimerLine);
				SetItemTitle(dailyInfoItem, status.DailyLine);
				SetItemTitle(displaysInfoItem, status.DisplaysLine);
				SetItemTitle(commandItems[MenuCommand.ToggleTimer], status.ToggleTimerLabel);
				Send(commandItems[MenuCommand.StopTimer], Sel("setEnabled:"), status.IsStopTimerEnabled ? Yes : No);
			}
			finally
			{
				AutoreleasePoolPop(pool);
			}
		}

		private void SetItemTitle(IntPtr item, string title)
		{
			Send(item, Sel("setTitle:"), NSString(title));
		}

		private void FeedKey(KeyCode key)
		{
			if (IsBlackoutVisible && exitMatcher != null && exitMatcher.Feed(key))
				HideBlackout();
		}

		private void CreateBlackoutImage(BlackoutGraphic graphic)
		{
			ReleaseBlackoutImage();
			graphicPointWidth = 0.0;
			graphicPointHeight = 0.0;
			if (graphic == null)
				return;

			IntPtr data = Send(GetClass("NSData"), Sel("dataWithBytes:length:"), graphic.Png, (ulong)graphic.Png.Length);
			blackoutImage = Send(Send(GetClass("NSImage"), Sel("alloc")), Sel("initWithData:"), data);
			if (blackoutImage == IntPtr.Zero)
				return;

			graphicPointWidth = graphic.PixelWidth / (double)GraphicPixelsPerPoint;
			graphicPointHeight = graphic.PixelHeight / (double)GraphicPixelsPerPoint;
			Send(blackoutImage, Sel("setSize:"), new CGSize(graphicPointWidth, graphicPointHeight));
		}

		private void ReleaseBlackoutImage()
		{
			if (blackoutImage == IntPtr.Zero)
				return;

			Send(blackoutImage, Sel("release"));
			blackoutImage = IntPtr.Zero;
		}

		/// <summary>
		/// Sizes the moving group: graphic on top, prompt text centred beneath it. Text cannot be
		/// measured without a struct return, so its width is a generous estimate; the label is centred
		/// in the group, so over-estimating only widens the invisible margin.
		/// </summary>
		private void MeasureGroup()
		{
			double textWidth = content.PromptText == null ? 0.0 : (content.PromptText.Length * PromptFontSize * 0.7) + 20.0;
			double textHeight = content.PromptText == null ? 0.0 : PromptHeight;
			double gap = graphicPointHeight > 0.0 && textHeight > 0.0 ? GroupGap : 0.0;
			groupWidth = Math.Max(graphicPointWidth, textWidth);
			groupHeight = graphicPointHeight + gap + textHeight;
		}

		/// <summary>
		/// Builds the view holding the graphic and the text. Cocoa's origin is bottom left, so the text
		/// sits at y = 0 and the graphic above it. Returns an owned (+1) view.
		/// </summary>
		private IntPtr CreateGroupView()
		{
			IntPtr group = SendObject(Send(GetClass("NSView"), Sel("alloc")), Sel("initWithFrame:"), new CGRect(0.0, 0.0, groupWidth, groupHeight));

			if (blackoutImage != IntPtr.Zero)
			{
				IntPtr imageView = Send(GetClass("NSImageView"), Sel("imageViewWithImage:"), blackoutImage);
				Send(imageView, Sel("setImageScaling:"), ImageScaleProportionallyUpOrDown);
				Send(imageView, Sel("setFrame:"), new CGRect((groupWidth - graphicPointWidth) / 2.0, groupHeight - graphicPointHeight, graphicPointWidth, graphicPointHeight));
				Send(group, Sel("addSubview:"), imageView);
			}

			if (content.PromptText != null)
			{
				IntPtr label = Send(GetClass("NSTextField"), Sel("labelWithString:"), NSString(content.PromptText));
				Send(label, Sel("setTextColor:"), Send(GetClass("NSColor"), Sel("colorWithWhite:alpha:"), 0.55, 1.0));
				Send(label, Sel("setFont:"), Send(GetClass("NSFont"), Sel("systemFontOfSize:"), PromptFontSize));
				Send(label, Sel("setAlignment:"), TextAlignmentCentre());
				Send(label, Sel("setFrame:"), new CGRect(0.0, 0.0, groupWidth, PromptHeight));
				Send(group, Sel("addSubview:"), label);
			}

			return group;
		}

		private void CreateBlackoutWindows()
		{
			List<CGRect> displays = GetDisplayBounds();
			blackoutDisplayCount = displays.Count;

			IntPtr black = SendClass("NSColor", "blackColor");
			IntPtr windowClass = GetClass(BlackoutWindowClassName);

			foreach (CGRect frame in displays)
			{
				IntPtr window = Send(windowClass, Sel("alloc"));
				window = Send(window, Sel("initWithContentRect:styleMask:backing:defer:"), frame, WindowStyleBorderless, BackingStoreBuffered, No);
				Send(window, Sel("setReleasedWhenClosed:"), No);
				Send(window, Sel("setBackgroundColor:"), black);
				Send(window, Sel("setOpaque:"), Yes);
				Send(window, Sel("setHasShadow:"), No);
				Send(window, Sel("setLevel:"), BlackoutWindowLevel);
				Send(window, Sel("setCollectionBehavior:"), BlackoutCollectionBehaviour);
				Send(window, Sel("setFrame:display:"), frame, Yes);

				IntPtr group = IntPtr.Zero;
				if (content.HasOverlay)
				{
					group = CreateGroupView();
					Send(Send(window, Sel("contentView")), Sel("addSubview:"), group);
					Send(group, Sel("release"));
				}

				BlackoutWindow blackout = new BlackoutWindow(window, group, frame.Width, frame.Height);
				blackoutWindows.Add(blackout);
				MovePrompt(blackout);
				Send(window, Sel("orderFrontRegardless"));
			}
		}

		private void TickBlackout()
		{
			// A display plugged in or removed mid-blackout: rebuild so every screen stays covered.
			if (DisplayCount != blackoutDisplayCount)
			{
				foreach (BlackoutWindow blackout in blackoutWindows)
				{
					Send(blackout.Window, Sel("orderOut:"), IntPtr.Zero);
					windowsPendingRelease.Add(blackout.Window);
				}

				blackoutWindows.Clear();
				CreateBlackoutWindows();
				TakeFocus();
				return;
			}

			foreach (BlackoutWindow blackout in blackoutWindows)
				MovePrompt(blackout);

			// Activation can be refused when another app is mid-interaction; keep asking so key presses
			// reach us. (Clicking the blackout also activates the app.)
			if (SendBool(application, Sel("isActive")) == No)
				TakeFocus();
		}

		private void MovePrompt(BlackoutWindow blackout)
		{
			if (blackout.Group == IntPtr.Zero)
				return;

			double x = Math.Floor(random.NextDouble() * Math.Max(0.0, blackout.Width - groupWidth));
			double y = Math.Floor(random.NextDouble() * Math.Max(0.0, blackout.Height - groupHeight));
			Send(blackout.Group, Sel("setFrameOrigin:"), new CGPoint(x, y));
		}

		private void TakeFocus()
		{
			ActivateApplication();
			if (blackoutWindows.Count > 0)
				Send(blackoutWindows[0].Window, Sel("makeKeyAndOrderFront:"), IntPtr.Zero);
		}

		/// <summary>
		/// macOS only honours [NSCursor hide] for the active app, and activation can arrive a moment
		/// after the blackout appears, so this is retried every frame until it takes.
		/// </summary>
		private void EnsureCursorHidden()
		{
			if (isCursorHidden)
				return;
			if (SendBool(application, Sel("isActive")) == No)
				return;

			SendClass("NSCursor", "hide");
			isCursorHidden = true;
		}

		private void ActivateApplication()
		{
			lastSelfActivationTime = Time.realtimeSinceStartup;
			Send(application, Sel("activateIgnoringOtherApps:"), Yes);
		}

		private void ReleasePendingWindows()
		{
			foreach (IntPtr window in windowsPendingRelease)
			{
				Send(window, Sel("close"));
				Send(window, Sel("release"));
			}

			windowsPendingRelease.Clear();
		}

		/// <summary>
		/// Launching the app again from Finder while it runs only activates the existing copy. With no
		/// Dock icon and a hidden window that would look like nothing happened, so treat an activation
		/// we did not cause as a request to show the settings window.
		/// </summary>
		private void DetectReopen(float now)
		{
			bool isActive = SendBool(application, Sel("isActive")) != No;
			bool isRisingEdge = isActive && !wasActive;
			wasActive = isActive;

			if (!isRisingEdge || isEditor || IsBlackoutVisible || IsSettingsWindowVisible)
				return;
			if (now - lastSelfActivationTime < 2.0f)
				return;

			ShowSettingsWindow();
		}

		private sealed class BlackoutWindow
		{
			public BlackoutWindow(IntPtr window, IntPtr group, double width, double height)
			{
				Window = window;
				Group = group;
				Width = width;
				Height = height;
			}

			public IntPtr Window { get; }

			public IntPtr Group { get; }

			public double Width { get; }

			public double Height { get; }
		}
	}
}
