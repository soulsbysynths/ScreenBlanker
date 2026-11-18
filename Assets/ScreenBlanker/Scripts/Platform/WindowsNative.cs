using System;
using System.Runtime.InteropServices;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// The handful of Win32 calls the Windows host needs. Names follow the Win32 API.
	/// </summary>
	internal static class WindowsNative
	{
		public const uint WmDestroy = 0x0002;
		public const uint WmPaint = 0x000F;
		public const uint WmClose = 0x0010;
		public const uint WmSetCursor = 0x0020;
		public const uint WmNull = 0x0000;
		public const uint WmSetText = 0x000C;
		public const uint WmKeyDown = 0x0100;
		public const uint WmSysKeyDown = 0x0104;
		public const uint WmSysChar = 0x0106;
		public const uint WmLButtonUp = 0x0202;
		public const uint WmRButtonUp = 0x0205;
		public const uint WmApp = 0x8000;
		public const int VkEscape = 0x1B;
		public const uint WsPopup = 0x80000000;
		public const uint WsExTopmost = 0x00000008;
		public const uint WsExToolWindow = 0x00000080;
		public const int SwHide = 0;
		public const int SwShow = 5;
		public const uint SwpNoSize = 0x0001;
		public const uint SwpNoMove = 0x0002;
		public const uint SwpNoActivate = 0x0010;
		public const uint SwpShowWindow = 0x0040;
		public const int SmCMonitors = 80;
		public const int SmCxSmIcon = 49;
		public const int SmCySmIcon = 50;
		public const uint ImageIcon = 1;
		public const uint LrLoadFromFile = 0x0010;
		public const int IdiApplication = 32512;
		public const int IdcArrow = 32512;
		public const int BlackBrush = 4;
		public const int Transparent = 1;
		public const uint NimAdd = 0;
		public const uint NimModify = 1;
		public const uint NimDelete = 2;
		public const uint NifMessage = 0x01;
		public const uint NifIcon = 0x02;
		public const uint NifTip = 0x04;
		public const uint MfString = 0x0000;
		public const uint MfGrayed = 0x0001;
		public const uint MfSeparator = 0x0800;
		public const uint MfByCommand = 0x0000;
		public const uint ScClose = 0xF060;
		public const uint TpmRightButton = 0x0002;
		public const uint TpmReturnCmd = 0x0100;
		public const uint RegSz = 1;
		public const uint KeySetValue = 0x0002;
		public const int ErrorAlreadyExists = 183;
		public const int LogPixelsY = 90;
		public const uint DibRgbColours = 0;
		public const uint BiRgb = 0;
		public const uint OfnHideReadOnly = 0x00000004;
		public const uint OfnNoChangeDir = 0x00000008;
		public const uint OfnPathMustExist = 0x00000800;
		public const uint OfnFileMustExist = 0x00001000;
		public const uint OfnExplorer = 0x00080000;

		/// <summary>
		/// BLENDFUNCTION packed into its 4 bytes: AC_SRC_OVER, no flags, constant alpha 255,
		/// AC_SRC_ALPHA (per-pixel premultiplied alpha). Passed by value, exactly as the struct would be.
		/// </summary>
		public const uint BlendPerPixelAlpha = 0x01FF0000;

		public static readonly IntPtr HwndTopmost = new IntPtr(-1);
		public static readonly IntPtr HkeyCurrentUser = new IntPtr(unchecked((int)0x80000001));

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref Win32Rect lprcMonitor, IntPtr dwData);

		[UnmanagedFunctionPointer(CallingConvention.Winapi)]
		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern ushort RegisterClassExW(ref WndClassEx lpwcx);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool UnregisterClassW(string lpClassName, IntPtr hInstance);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateWindowExW(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

		[DllImport("user32.dll")]
		public static extern bool DestroyWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

		[DllImport("user32.dll")]
		public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

		[DllImport("user32.dll")]
		public static extern bool InvalidateRect(IntPtr hWnd, ref Win32Rect lpRect, bool bErase);

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		public static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool SetWindowTextW(IntPtr hWnd, string lpString);

		[DllImport("user32.dll")]
		public static extern IntPtr BeginPaint(IntPtr hWnd, out PaintStruct lpPaint);

		[DllImport("user32.dll")]
		public static extern bool EndPaint(IntPtr hWnd, ref PaintStruct lpPaint);

		[DllImport("user32.dll")]
		public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

		[DllImport("user32.dll")]
		public static extern bool EnumThreadWindows(uint dwThreadId, EnumWindowsProc lpfn, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern int GetClassNameW(IntPtr hWnd, [Out] char[] lpClassName, int nMaxCount);

		[DllImport("user32.dll")]
		public static extern int GetSystemMetrics(int nIndex);

		[DllImport("user32.dll")]
		public static extern short GetAsyncKeyState(int vKey);

		[DllImport("user32.dll")]
		public static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		public static extern IntPtr SetCursor(IntPtr hCursor);

		[DllImport("user32.dll")]
		public static extern bool GetCursorPos(out Win32Point lpPoint);

		[DllImport("user32.dll")]
		public static extern IntPtr CreatePopupMenu();

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern bool AppendMenuW(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

		[DllImport("user32.dll")]
		public static extern bool DestroyMenu(IntPtr hMenu);

		[DllImport("user32.dll")]
		public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hWnd, IntPtr lptpm);

		[DllImport("user32.dll")]
		public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

		[DllImport("user32.dll")]
		public static extern int EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern uint RegisterWindowMessageW(string lpString);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr LoadImageW(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

		[DllImport("user32.dll")]
		public static extern IntPtr LoadIconW(IntPtr hInstance, IntPtr lpIconName);

		[DllImport("user32.dll")]
		public static extern IntPtr LoadCursorW(IntPtr hInstance, IntPtr lpCursorName);

		[DllImport("user32.dll")]
		public static extern bool DestroyIcon(IntPtr hIcon);

		[DllImport("gdi32.dll")]
		public static extern IntPtr GetStockObject(int i);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr CreateFontW(int cHeight, int cWidth, int cEscapement, int cOrientation, int cWeight, uint bItalic, uint bUnderline, uint bStrikeOut, uint iCharSet, uint iOutPrecision, uint iClipPrecision, uint iQuality, uint iPitchAndFamily, string pszFaceName);

		[DllImport("gdi32.dll")]
		public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

		[DllImport("gdi32.dll")]
		public static extern bool DeleteObject(IntPtr ho);

		[DllImport("gdi32.dll")]
		public static extern uint SetTextColor(IntPtr hdc, uint colour);

		[DllImport("gdi32.dll")]
		public static extern int SetBkMode(IntPtr hdc, int mode);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		public static extern bool TextOutW(IntPtr hdc, int x, int y, string lpString, int c);

		[DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
		public static extern bool GetTextExtentPoint32W(IntPtr hdc, string lpString, int c, out Win32Size psizl);

		[DllImport("gdi32.dll")]
		public static extern int GetDeviceCaps(IntPtr hdc, int index);

		[DllImport("gdi32.dll")]
		public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		public static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll")]
		public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfoHeader pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

		[DllImport("msimg32.dll")]
		public static extern bool AlphaBlend(IntPtr hdcDest, int xoriginDest, int yoriginDest, int wDest, int hDest, IntPtr hdcSrc, int xoriginSrc, int yoriginSrc, int wSrc, int hSrc, uint ftn);

		[DllImport("comdlg32.dll", CharSet = CharSet.Unicode)]
		public static extern bool GetOpenFileNameW(ref OpenFileName lpofn);

		[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
		public static extern bool Shell_NotifyIconW(uint dwMessage, ref NotifyIconData lpData);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern IntPtr GetModuleHandleW(string lpModuleName);

		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		public static extern IntPtr CreateMutexW(IntPtr lpMutexAttributes, bool bInitialOwner, string lpName);

		[DllImport("kernel32.dll")]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
		public static extern uint GetModuleFileNameW(IntPtr hModule, [Out] char[] lpFilename, uint nSize);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
		public static extern int RegOpenKeyExW(IntPtr hKey, string lpSubKey, uint ulOptions, uint samDesired, out IntPtr phkResult);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
		public static extern int RegSetValueExW(IntPtr hKey, string lpValueName, uint reserved, uint dwType, byte[] lpData, uint cbData);

		[DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
		public static extern int RegDeleteValueW(IntPtr hKey, string lpValueName);

		[DllImport("advapi32.dll")]
		public static extern int RegCloseKey(IntPtr hKey);

		/// <summary>
		/// Win32 RECT.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Win32Rect
		{
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		/// <summary>
		/// Win32 POINT.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Win32Point
		{
			public int X;
			public int Y;
		}

		/// <summary>
		/// Win32 SIZE.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct Win32Size
		{
			public int Cx;
			public int Cy;
		}

		/// <summary>
		/// Win32 BITMAPINFOHEADER. With 32-bit BI_RGB there is no colour table, so this alone serves as
		/// the BITMAPINFO for CreateDIBSection.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct BitmapInfoHeader
		{
			public uint Size;
			public int Width;
			public int Height;
			public ushort Planes;
			public ushort BitCount;
			public uint Compression;
			public uint SizeImage;
			public int XPelsPerMeter;
			public int YPelsPerMeter;
			public uint ClrUsed;
			public uint ClrImportant;
		}

		/// <summary>
		/// Win32 OPENFILENAMEW.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct OpenFileName
		{
			public uint StructSize;
			public IntPtr Owner;
			public IntPtr Instance;
			public IntPtr Filter;
			public IntPtr CustomFilter;
			public uint MaxCustomFilter;
			public uint FilterIndex;
			public IntPtr File;
			public uint MaxFile;
			public IntPtr FileTitle;
			public uint MaxFileTitle;
			public string InitialDir;
			public string Title;
			public uint Flags;
			public ushort FileOffset;
			public ushort FileExtension;
			public string DefExt;
			public IntPtr CustData;
			public IntPtr Hook;
			public IntPtr TemplateName;
			public IntPtr Reserved;
			public uint ReservedInt;
			public uint FlagsEx;
		}

		/// <summary>
		/// Win32 PAINTSTRUCT.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct PaintStruct
		{
			public IntPtr Hdc;
			public int Erase;
			public Win32Rect Paint;
			public int Restore;
			public int IncUpdate;

			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
			public byte[] Reserved;
		}

		/// <summary>
		/// Win32 WNDCLASSEXW.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct WndClassEx
		{
			public uint Size;
			public uint Style;
			public IntPtr WndProc;
			public int ClassExtra;
			public int WindowExtra;
			public IntPtr Instance;
			public IntPtr Icon;
			public IntPtr Cursor;
			public IntPtr Background;
			public string MenuName;
			public string ClassName;
			public IntPtr IconSmall;
		}

		/// <summary>
		/// Win32 NOTIFYICONDATAW.
		/// </summary>
		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
		public struct NotifyIconData
		{
			public uint Size;
			public IntPtr Window;
			public uint Id;
			public uint Flags;
			public uint CallbackMessage;
			public IntPtr Icon;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
			public string Tip;

			public uint State;
			public uint StateMask;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
			public string Info;

			public uint TimeoutOrVersion;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
			public string InfoTitle;

			public uint InfoFlags;
			public Guid Item;
			public IntPtr BalloonIcon;
		}
	}
}
