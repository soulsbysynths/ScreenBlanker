using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AOT;
using ScreenBlanker.Blackout;
using ScreenBlanker.Keys;
using UnityEngine;
using static ScreenBlanker.Platform.WindowsNative;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// Windows host: a notification-area (tray) icon, one topmost black popup per monitor for the
	/// blackout, and an HKCU Run-key entry for start-at-login. Mainly here so the app can be developed
	/// and exercised on a PC; macOS is the primary target.
	/// </summary>
	/// <remarks>
	/// All windows are created on the Unity main thread, whose message pump dispatches to them, so the
	/// window procedure always runs on the main thread too.
	/// </remarks>
	public sealed class WindowsPlatformHost : IPlatformHost
	{
		private const string TrayClassName = "ScreenBlankerTray";
		private const string BlackoutClassName = "ScreenBlankerBlackout";
		private const string MutexName = "Local\\SoulsbySynths.ScreenBlanker.SingleInstance";
		private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
		private const string RunValueName = "ScreenBlanker";
		private const string TrayIconFileName = "TrayIcon.ico";
		private const uint TrayCallbackMessage = WmApp + 1;
		private const uint PromptColour = 0x008C8C8C;
		private const int PromptFontHeight = -22;
		private const int GraphicDisplaySize = 180;
		private const int GroupGap = 12;
		private const int MaxPathChars = 1024;

		// Modifiers first, so a combination pressed within one poll is fed modifier-first.
		private static readonly int[] ModifierVirtualKeys = { 0x10, 0x11, 0x12, 0x5B, 0x5C };

		// Callbacks are static (required for IL2CPP) and reach the live host through this.
		private static WindowsPlatformHost current;
		private static WndProc wndProc;
		private static MonitorEnumProc monitorEnumProc;
		private static EnumWindowsProc enumWindowsProc;

		private readonly bool isEditor;
		private readonly Dictionary<IntPtr, BlackoutWindow> blackoutWindows = new Dictionary<IntPtr, BlackoutWindow>();
		private readonly List<IntPtr> windowsPendingDestroy = new List<IntPtr>();
		private readonly List<Win32Rect> monitorRects = new List<Win32Rect>();
		private readonly System.Random random = new System.Random();
		private readonly List<int> polledVirtualKeys = new List<int>();
		private readonly HashSet<int> virtualKeysDown = new HashSet<int>();

		private Func<StatusSnapshot> statusProvider;
		private IntPtr instance;
		private IntPtr trayWindow;
		private IntPtr trayIcon;
		private IntPtr promptFont;
		private IntPtr unityWindow;
		private IntPtr singleInstanceMutex;
		private IntPtr graphicDc;
		private IntPtr graphicBitmap;
		private IntPtr previousGraphicBitmap;
		private BlackoutContent content;
		private KeySequence.Matcher exitMatcher;
		private Win32Size textSize;
		private int graphicWidth;
		private int graphicHeight;
		private int groupWidth;
		private int groupHeight;
		private float dpiScale = 1.0f;
		private uint taskbarCreatedMessage;
		private string lastTip;
		private float lastPromptMoveTime;
		private float lastStatusRefreshTime;
		private bool isTrayIconAdded;
		private bool isInitialised;

		/// <summary>
		/// Creates the host. Nothing native is touched until <see cref="Initialise"/>.
		/// </summary>
		public WindowsPlatformHost()
		{
			isEditor = Application.isEditor;
		}

		/// <inheritdoc/>
		public event Action<MenuCommand> MenuCommandInvoked;

		/// <inheritdoc/>
		public int DisplayCount => Math.Max(1, GetSystemMetrics(SmCMonitors));

		/// <inheritdoc/>
		public bool IsBlackoutVisible { get; private set; }

		/// <inheritdoc/>
		public bool IsSettingsWindowVisible { get; private set; } = true;

		/// <inheritdoc/>
		public bool IsBlackoutDrawnInApp => false;

		/// <inheritdoc/>
		public int GraphicMaxPixels => Mathf.RoundToInt(GraphicDisplaySize * dpiScale);

		/// <inheritdoc/>
		public bool TryClaimSingleInstance()
		{
			if (isEditor)
				return true;

			singleInstanceMutex = CreateMutexW(IntPtr.Zero, true, MutexName);
			return Marshal.GetLastWin32Error() != ErrorAlreadyExists;
		}

		/// <inheritdoc/>
		public void Initialise(Func<StatusSnapshot> statusProvider, bool isStartHidden)
		{
			this.statusProvider = statusProvider;
			current = this;

			wndProc = WindowProcedure;
			monitorEnumProc = OnMonitor;
			enumWindowsProc = OnThreadWindow;

			instance = GetModuleHandleW(null);
			RegisterWindowClass(TrayClassName, IntPtr.Zero, LoadCursorW(IntPtr.Zero, new IntPtr(IdcArrow)));
			RegisterWindowClass(BlackoutClassName, GetStockObject(BlackBrush), IntPtr.Zero);

			// A hidden top-level window rather than a message-only one: the tray menu needs a window
			// that can become foreground, or it will not close when the user clicks elsewhere.
			trayWindow = CreateWindowExW(WsExToolWindow, TrayClassName, "ScreenBlanker", WsPopup, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
			taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
			IntPtr screenDc = GetDC(IntPtr.Zero);
			dpiScale = Math.Max(1.0f, GetDeviceCaps(screenDc, LogPixelsY) / 96.0f);
			ReleaseDC(IntPtr.Zero, screenDc);
			promptFont = CreateFontW(Mathf.RoundToInt(PromptFontHeight * dpiScale), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");

			foreach (int virtualKey in ModifierVirtualKeys)
				polledVirtualKeys.Add(virtualKey);
			foreach (int virtualKey in KeyCatalogue.WindowsVirtualKeys)
			{
				if (!polledVirtualKeys.Contains(virtualKey))
					polledVirtualKeys.Add(virtualKey);
			}

			trayIcon = LoadTrayIcon();
			AddTrayIcon();

			if (!isEditor)
			{
				unityWindow = FindUnityWindow();

				// Closing Unity's only window quits the player, so grey out Close (and with it Alt+F4).
				// The window is hidden from the UI or the tray menu instead.
				if (unityWindow != IntPtr.Zero)
					EnableMenuItem(GetSystemMenu(unityWindow, false), ScClose, MfByCommand | MfGrayed);
			}

			isInitialised = true;
			if (isStartHidden)
				HideSettingsWindow();

			RefreshStatus();
		}

		/// <inheritdoc/>
		public void Tick()
		{
			if (!isInitialised)
				return;

			DestroyPendingWindows();

			float now = Time.realtimeSinceStartup;
			if (now - lastStatusRefreshTime >= 1.0f)
			{
				lastStatusRefreshTime = now;
				RefreshStatus();
			}

			if (!IsBlackoutVisible)
				return;

			// Polled globally rather than read from WM_KEYDOWN: Windows' foreground lock may stop a
			// background app taking keyboard focus.
			if (PollExitSequence())
			{
				HideBlackout();
				return;
			}

			if (now - lastPromptMoveTime >= 1.0f)
			{
				lastPromptMoveTime = now;
				TickBlackout();
			}
		}

		/// <inheritdoc/>
		public void ShowBlackout(BlackoutContent content)
		{
			if (!isInitialised || IsBlackoutVisible)
				return;

			this.content = content;
			exitMatcher = content.ExitSequence.CreateMatcher();
			PrimeKeyStates();
			CreateGraphic(content.Graphic);
			MeasureGroup();

			CreateBlackoutWindows();
			IsBlackoutVisible = true;
			lastPromptMoveTime = Time.realtimeSinceStartup;
			HideCursorNow();
		}

		/// <inheritdoc/>
		public void HideBlackout()
		{
			if (!IsBlackoutVisible)
				return;

			// May run inside a blackout window's own WM_KEYDOWN, so hide now and destroy on next Tick.
			foreach (IntPtr window in blackoutWindows.Keys)
			{
				ShowWindow(window, SwHide);
				windowsPendingDestroy.Add(window);
			}

			blackoutWindows.Clear();
			IsBlackoutVisible = false;
		}

		/// <inheritdoc/>
		public string ChooseImageFile()
		{
#if UNITY_EDITOR
			string chosen = UnityEditor.EditorUtility.OpenFilePanel("Choose blackout graphic", string.Empty, "png,jpg,jpeg");
			return string.IsNullOrEmpty(chosen) ? null : chosen;
#else
			IntPtr filter = Marshal.StringToHGlobalUni("Images (*.png, *.jpg)\0*.png;*.jpg;*.jpeg\0\0");
			IntPtr file = Marshal.AllocHGlobal(MaxPathChars * 2);
			try
			{
				Marshal.WriteInt16(file, 0);
				OpenFileName dialog = new OpenFileName
				{
					StructSize = (uint)Marshal.SizeOf(typeof(OpenFileName)),
					Owner = unityWindow,
					Filter = filter,
					FilterIndex = 1,
					File = file,
					MaxFile = MaxPathChars,
					Title = "Choose blackout graphic",
					Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnHideReadOnly | OfnNoChangeDir,
				};
				return GetOpenFileNameW(ref dialog) ? Marshal.PtrToStringUni(file) : null;
			}
			finally
			{
				Marshal.FreeHGlobal(file);
				Marshal.FreeHGlobal(filter);
			}
#endif
		}

		/// <inheritdoc/>
		public void SetWindowTitle(string title)
		{
			if (!isEditor && unityWindow != IntPtr.Zero)
				SetWindowTextW(unityWindow, title);
		}

		/// <inheritdoc/>
		public void ShowSettingsWindow()
		{
			IsSettingsWindowVisible = true;
			if (unityWindow == IntPtr.Zero)
				return;

			ShowWindow(unityWindow, SwShow);
			SetForegroundWindow(unityWindow);
		}

		/// <inheritdoc/>
		public void HideSettingsWindow()
		{
			IsSettingsWindowVisible = false;
			if (unityWindow != IntPtr.Zero)
				ShowWindow(unityWindow, SwHide);
		}

		/// <inheritdoc/>
		public void ApplyStartAtLogin(bool isEnabled)
		{
			// In the editor the "app" is Unity.exe, which must never be registered.
			if (isEditor)
				return;

			if (RegOpenKeyExW(HkeyCurrentUser, RunKeyPath, 0, KeySetValue, out IntPtr key) != 0)
				return;

			try
			{
				if (!isEnabled)
				{
					RegDeleteValueW(key, RunValueName);
					return;
				}

				char[] buffer = new char[1024];
				uint length = GetModuleFileNameW(IntPtr.Zero, buffer, (uint)buffer.Length);
				string command = "\"" + new string(buffer, 0, (int)length) + "\" " + BlankerApp.HiddenArgument;
				byte[] data = Encoding.Unicode.GetBytes(command + "\0");
				RegSetValueExW(key, RunValueName, 0, RegSz, data, (uint)data.Length);
			}
			finally
			{
				RegCloseKey(key);
			}
		}

		/// <inheritdoc/>
		public void Shutdown()
		{
			if (!isInitialised)
				return;

			isInitialised = false;
			HideBlackout();
			DestroyPendingWindows();

			if (isTrayIconAdded)
			{
				NotifyIconData data = NewNotifyIconData();
				Shell_NotifyIconW(NimDelete, ref data);
				isTrayIconAdded = false;
			}

			if (trayWindow != IntPtr.Zero)
			{
				DestroyWindow(trayWindow);
				trayWindow = IntPtr.Zero;
			}

			if (trayIcon != IntPtr.Zero)
			{
				DestroyIcon(trayIcon);
				trayIcon = IntPtr.Zero;
			}

			if (promptFont != IntPtr.Zero)
			{
				DeleteObject(promptFont);
				promptFont = IntPtr.Zero;
			}

			DestroyGraphic();

			// Unregister so the editor never keeps a class pointing at a callback thunk that a
			// domain reload has freed.
			UnregisterClassW(TrayClassName, instance);
			UnregisterClassW(BlackoutClassName, instance);

			if (singleInstanceMutex != IntPtr.Zero)
			{
				CloseHandle(singleInstanceMutex);
				singleInstanceMutex = IntPtr.Zero;
			}

			if (current == this)
				current = null;
		}

		[MonoPInvokeCallback(typeof(WndProc))]
		private static IntPtr WindowProcedure(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
		{
			if (current != null)
			{
				IntPtr? handled = current.HandleMessage(hWnd, msg, wParam, lParam);
				if (handled.HasValue)
					return handled.Value;
			}

			return DefWindowProcW(hWnd, msg, wParam, lParam);
		}

		[MonoPInvokeCallback(typeof(MonitorEnumProc))]
		private static bool OnMonitor(IntPtr hMonitor, IntPtr hdc, ref Win32Rect monitorRect, IntPtr data)
		{
			current?.monitorRects.Add(monitorRect);
			return true;
		}

		[MonoPInvokeCallback(typeof(EnumWindowsProc))]
		private static bool OnThreadWindow(IntPtr hWnd, IntPtr lParam)
		{
			char[] name = new char[64];
			int length = GetClassNameW(hWnd, name, name.Length);
			if (current == null || new string(name, 0, length) != "UnityWndClass")
				return true;

			current.unityWindow = hWnd;
			return false;
		}

		/// <summary>
		/// The pointer only changes when the window under it gets WM_SETCURSOR, which normally waits
		/// for the mouse to move. Re-setting the position to where it already is sends one at once.
		/// </summary>
		private static void HideCursorNow()
		{
			SetCursor(IntPtr.Zero);
			if (GetCursorPos(out Win32Point position))
				SetCursorPos(position.X, position.Y);
		}

		private IntPtr? HandleMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
		{
			if (hWnd == trayWindow)
			{
				if (msg == TrayCallbackMessage)
				{
					uint mouseMessage = (uint)lParam.ToInt64() & 0xFFFF;
					if (mouseMessage == WmLButtonUp || mouseMessage == WmRButtonUp)
						ShowTrayMenu();
					return IntPtr.Zero;
				}

				// Explorer restarted: the notification area is new and our icon has gone.
				if (msg == taskbarCreatedMessage && taskbarCreatedMessage != 0)
				{
					isTrayIconAdded = false;
					AddTrayIcon();
					return IntPtr.Zero;
				}

				return null;
			}

			if (!blackoutWindows.TryGetValue(hWnd, out BlackoutWindow blackout))
				return null;

			switch (msg)
			{
				case WmPaint:
					PaintOverlay(blackout);
					return IntPtr.Zero;
				case WmKeyDown:
				case WmSysKeyDown:
				case WmSysChar:
					// Keys are polled in Tick. Swallowing them here stops Alt+F4, and Alt opening a menu.
					return IntPtr.Zero;
				case WmClose:
					// Only the exit sequence dismisses the blackout.
					return IntPtr.Zero;
				case WmSetCursor:
					// No pointer over the blackout. Returning TRUE stops DefWindowProc restoring one.
					SetCursor(IntPtr.Zero);
					return new IntPtr(1);
				default:
					return null;
			}
		}

		private void RegisterWindowClass(string className, IntPtr background, IntPtr cursor)
		{
			// A class left registered by an earlier editor play session still points at a freed
			// callback; replace it.
			UnregisterClassW(className, instance);

			WndClassEx windowClass = new WndClassEx
			{
				Size = (uint)Marshal.SizeOf(typeof(WndClassEx)),
				WndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
				Instance = instance,
				Cursor = cursor,
				Background = background,
				ClassName = className,
			};
			RegisterClassExW(ref windowClass);
		}

		private IntPtr LoadTrayIcon()
		{
			string path = Path.Combine(Application.streamingAssetsPath, TrayIconFileName);
			IntPtr icon = LoadImageW(IntPtr.Zero, path, ImageIcon, GetSystemMetrics(SmCxSmIcon), GetSystemMetrics(SmCySmIcon), LrLoadFromFile);
			return icon != IntPtr.Zero ? icon : LoadIconW(IntPtr.Zero, new IntPtr(IdiApplication));
		}

		private NotifyIconData NewNotifyIconData()
		{
			return new NotifyIconData
			{
				Size = (uint)Marshal.SizeOf(typeof(NotifyIconData)),
				Window = trayWindow,
				Id = 1,
				Tip = string.Empty,
				Info = string.Empty,
				InfoTitle = string.Empty,
			};
		}

		private void AddTrayIcon()
		{
			NotifyIconData data = NewNotifyIconData();
			data.Flags = NifMessage | NifIcon | NifTip;
			data.CallbackMessage = TrayCallbackMessage;
			data.Icon = trayIcon;
			data.Tip = lastTip ?? "ScreenBlanker";
			isTrayIconAdded = Shell_NotifyIconW(NimAdd, ref data);
		}

		private void RefreshStatus()
		{
			if (!isTrayIconAdded || statusProvider == null)
				return;

			StatusSnapshot status = statusProvider();
			string tip = "ScreenBlanker\n" + status.TimerLine + "\n" + status.DailyLine;
			if (tip.Length > 127)
				tip = tip.Substring(0, 127);
			if (tip == lastTip)
				return;

			lastTip = tip;
			NotifyIconData data = NewNotifyIconData();
			data.Flags = NifTip;
			data.Tip = tip;
			Shell_NotifyIconW(NimModify, ref data);
		}

		private void ShowTrayMenu()
		{
			StatusSnapshot status = statusProvider();
			IntPtr popup = CreatePopupMenu();
			AppendMenuW(popup, MfString | MfGrayed, UIntPtr.Zero, status.TimerLine);
			AppendMenuW(popup, MfString | MfGrayed, UIntPtr.Zero, status.DailyLine);
			AppendMenuW(popup, MfString | MfGrayed, UIntPtr.Zero, status.DisplaysLine);
			AppendMenuW(popup, MfSeparator, UIntPtr.Zero, null);
			AppendMenuW(popup, MfString, (UIntPtr)(uint)MenuCommand.ToggleTimer, status.ToggleTimerLabel);
			AppendMenuW(popup, MfString | (status.IsStopTimerEnabled ? 0 : MfGrayed), (UIntPtr)(uint)MenuCommand.StopTimer, "Stop timer");
			AppendMenuW(popup, MfString, (UIntPtr)(uint)MenuCommand.BlackoutNow, "Blackout now");
			AppendMenuW(popup, MfSeparator, UIntPtr.Zero, null);
			AppendMenuW(popup, MfString, (UIntPtr)(uint)MenuCommand.OpenSettings, "Settings…");
			AppendMenuW(popup, MfSeparator, UIntPtr.Zero, null);
			AppendMenuW(popup, MfString, (UIntPtr)(uint)MenuCommand.Quit, "Quit ScreenBlanker");

			GetCursorPos(out Win32Point cursor);

			// Required so the menu closes when the user clicks elsewhere (see TrackPopupMenu docs).
			SetForegroundWindow(trayWindow);
			int command = TrackPopupMenuEx(popup, TpmRightButton | TpmReturnCmd, cursor.X, cursor.Y, trayWindow, IntPtr.Zero);
			PostMessageW(trayWindow, WmNull, IntPtr.Zero, IntPtr.Zero);
			DestroyMenu(popup);

			if (command != 0)
			{
				MenuCommandInvoked?.Invoke((MenuCommand)command);
				lastTip = null;
				RefreshStatus();
			}
		}

		private IntPtr FindUnityWindow()
		{
			unityWindow = IntPtr.Zero;
			EnumThreadWindows(GetCurrentThreadId(), enumWindowsProc, IntPtr.Zero);
			return unityWindow;
		}

		private List<Win32Rect> GetMonitorRects()
		{
			monitorRects.Clear();
			EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, monitorEnumProc, IntPtr.Zero);
			return new List<Win32Rect>(monitorRects);
		}

		private void CreateBlackoutWindows()
		{
			IntPtr first = IntPtr.Zero;
			foreach (Win32Rect monitor in GetMonitorRects())
			{
				int width = monitor.Right - monitor.Left;
				int height = monitor.Bottom - monitor.Top;
				IntPtr window = CreateWindowExW(WsExTopmost | WsExToolWindow, BlackoutClassName, "ScreenBlanker", WsPopup, monitor.Left, monitor.Top, width, height, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero);
				if (window == IntPtr.Zero)
					continue;

				BlackoutWindow blackout = new BlackoutWindow(window, width, height);
				blackoutWindows[window] = blackout;
				MovePrompt(blackout);
				SetWindowPos(window, HwndTopmost, monitor.Left, monitor.Top, width, height, SwpShowWindow);
				if (first == IntPtr.Zero)
					first = window;
			}

			if (first != IntPtr.Zero)
				SetForegroundWindow(first);
		}

		private void TickBlackout()
		{
			// A monitor plugged in or removed mid-blackout: rebuild so every screen stays covered.
			if (DisplayCount != blackoutWindows.Count)
			{
				HideBlackout();
				CreateBlackoutWindows();
				IsBlackoutVisible = true;
				return;
			}

			foreach (BlackoutWindow blackout in blackoutWindows.Values)
			{
				MovePrompt(blackout);

				// Something else (the taskbar, say) may have been raised above us; push back on top.
				SetWindowPos(blackout.Window, HwndTopmost, 0, 0, 0, 0, SwpNoActivate | SwpNoSize | SwpNoMove);
			}
		}

		/// <summary>
		/// Polls every catalogue key and feeds new presses to the matcher. Returns true once the exit
		/// sequence is complete.
		/// </summary>
		private bool PollExitSequence()
		{
			bool isComplete = false;
			foreach (int virtualKey in polledVirtualKeys)
			{
				short state = GetAsyncKeyState(virtualKey);
				bool isDown = (state & 0x8000) != 0;
				bool wasDown = virtualKeysDown.Contains(virtualKey);

				// The low bit catches a tap that went down and back up between two polls.
				bool isPressed = (isDown && !wasDown) || (!isDown && (state & 0x0001) != 0);

				if (isDown)
					virtualKeysDown.Add(virtualKey);
				else
					virtualKeysDown.Remove(virtualKey);

				if (isPressed && !isComplete && exitMatcher.Feed(KeyCatalogue.FromWindowsVirtualKey(virtualKey)))
					isComplete = true;
			}

			return isComplete;
		}

		/// <summary>
		/// Notes which keys are already held and clears their "pressed since last call" bits, so a key
		/// held as the blackout starts does not count as a press.
		/// </summary>
		private void PrimeKeyStates()
		{
			virtualKeysDown.Clear();
			foreach (int virtualKey in polledVirtualKeys)
			{
				if ((GetAsyncKeyState(virtualKey) & 0x8000) != 0)
					virtualKeysDown.Add(virtualKey);
			}
		}

		private void CreateGraphic(BlackoutGraphic graphic)
		{
			DestroyGraphic();
			if (graphic == null)
				return;

			BitmapInfoHeader header = new BitmapInfoHeader
			{
				Size = (uint)Marshal.SizeOf(typeof(BitmapInfoHeader)),
				Width = graphic.PixelWidth,
				Height = -graphic.PixelHeight,
				Planes = 1,
				BitCount = 32,
				Compression = BiRgb,
			};

			IntPtr screenDc = GetDC(IntPtr.Zero);
			graphicDc = CreateCompatibleDC(screenDc);
			graphicBitmap = CreateDIBSection(screenDc, ref header, DibRgbColours, out IntPtr bits, IntPtr.Zero, 0);
			ReleaseDC(IntPtr.Zero, screenDc);

			if (graphicDc == IntPtr.Zero || graphicBitmap == IntPtr.Zero || bits == IntPtr.Zero)
			{
				DestroyGraphic();
				return;
			}

			Marshal.Copy(graphic.PremultipliedBgra, 0, bits, graphic.PremultipliedBgra.Length);
			previousGraphicBitmap = SelectObject(graphicDc, graphicBitmap);
			graphicWidth = graphic.PixelWidth;
			graphicHeight = graphic.PixelHeight;
		}

		private void DestroyGraphic()
		{
			if (graphicDc != IntPtr.Zero)
			{
				if (previousGraphicBitmap != IntPtr.Zero)
					SelectObject(graphicDc, previousGraphicBitmap);
				DeleteDC(graphicDc);
			}

			if (graphicBitmap != IntPtr.Zero)
				DeleteObject(graphicBitmap);

			graphicDc = IntPtr.Zero;
			graphicBitmap = IntPtr.Zero;
			previousGraphicBitmap = IntPtr.Zero;
			graphicWidth = 0;
			graphicHeight = 0;
		}

		/// <summary>
		/// Works out the size of the moving group: graphic on top, prompt text centred beneath it.
		/// </summary>
		private void MeasureGroup()
		{
			textSize = default;
			if (content.PromptText != null)
			{
				IntPtr screenDc = GetDC(IntPtr.Zero);
				IntPtr previousFont = SelectObject(screenDc, promptFont);
				GetTextExtentPoint32W(screenDc, content.PromptText, content.PromptText.Length, out textSize);
				SelectObject(screenDc, previousFont);
				ReleaseDC(IntPtr.Zero, screenDc);
			}

			int gap = graphicHeight > 0 && textSize.Cy > 0 ? Mathf.RoundToInt(GroupGap * dpiScale) : 0;
			groupWidth = Math.Max(graphicWidth, textSize.Cx);
			groupHeight = graphicHeight + gap + textSize.Cy;
		}

		private void MovePrompt(BlackoutWindow blackout)
		{
			if (groupWidth <= 0 || groupHeight <= 0)
				return;

			// Repaint only where the group was and where it goes, so the rest of the screen never flickers.
			InvalidateGroup(blackout);
			blackout.PromptX = random.Next(0, Math.Max(1, blackout.Width - groupWidth));
			blackout.PromptY = random.Next(0, Math.Max(1, blackout.Height - groupHeight));
			InvalidateGroup(blackout);
		}

		private void InvalidateGroup(BlackoutWindow blackout)
		{
			Win32Rect area = new Win32Rect
			{
				Left = blackout.PromptX,
				Top = blackout.PromptY,
				Right = blackout.PromptX + groupWidth,
				Bottom = blackout.PromptY + groupHeight,
			};
			InvalidateRect(blackout.Window, ref area, true);
		}

		private void PaintOverlay(BlackoutWindow blackout)
		{
			IntPtr hdc = BeginPaint(blackout.Window, out PaintStruct paint);
			if (content == null || !content.HasOverlay)
			{
				EndPaint(blackout.Window, ref paint);
				return;
			}

			if (graphicDc != IntPtr.Zero)
			{
				int graphicX = blackout.PromptX + ((groupWidth - graphicWidth) / 2);
				AlphaBlend(hdc, graphicX, blackout.PromptY, graphicWidth, graphicHeight, graphicDc, 0, 0, graphicWidth, graphicHeight, BlendPerPixelAlpha);
			}

			if (content.PromptText != null)
			{
				int textX = blackout.PromptX + ((groupWidth - textSize.Cx) / 2);
				int textY = blackout.PromptY + groupHeight - textSize.Cy;
				IntPtr previousFont = SelectObject(hdc, promptFont);
				SetTextColor(hdc, PromptColour);
				SetBkMode(hdc, Transparent);
				TextOutW(hdc, textX, textY, content.PromptText, content.PromptText.Length);
				SelectObject(hdc, previousFont);
			}

			EndPaint(blackout.Window, ref paint);
		}

		private void DestroyPendingWindows()
		{
			foreach (IntPtr window in windowsPendingDestroy)
				DestroyWindow(window);

			windowsPendingDestroy.Clear();
		}

		private sealed class BlackoutWindow
		{
			public BlackoutWindow(IntPtr window, int width, int height)
			{
				Window = window;
				Width = width;
				Height = height;
			}

			public IntPtr Window { get; }

			public int Width { get; }

			public int Height { get; }

			public int PromptX { get; set; }

			public int PromptY { get; set; }
		}
	}
}
