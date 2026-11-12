using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace ScreenBlanker.Platform
{
	/// <summary>
	/// Minimal bindings to the Objective-C runtime and CoreGraphics, so AppKit can be driven from C#
	/// without a compiled native plug-in (which would need a Mac to build).
	/// </summary>
	/// <remarks>
	/// <para><c>objc_msgSend</c> is a trampoline that jumps to the method implementation, so it must be
	/// declared with the exact C signature of each method called through it. Each overload below is one
	/// such signature. BOOL arguments are passed as <see cref="byte"/>: BOOL is one byte on both x86_64
	/// (signed char) and arm64 (bool).</para>
	/// <para>No method here returns a struct through <c>objc_msgSend</c>: on x86_64 that would need
	/// <c>objc_msgSend_stret</c> while arm64 has none. Screen geometry comes from CoreGraphics C functions
	/// instead, whose struct returns are handled by the ordinary C ABI.</para>
	/// </remarks>
	internal static class MacNative
	{
		public const byte Yes = 1;
		public const byte No = 0;

		private const string LibObjc = "/usr/lib/libobjc.A.dylib";
		private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

		private static readonly Dictionary<string, IntPtr> SelectorCache = new Dictionary<string, IntPtr>();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void ObjectArgImp(IntPtr self, IntPtr cmd, IntPtr arg);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate byte BoolReturnImp(IntPtr self, IntPtr cmd);

		[DllImport(LibObjc, EntryPoint = "objc_getClass")]
		public static extern IntPtr GetClass(string name);

		[DllImport(LibObjc, EntryPoint = "objc_allocateClassPair")]
		public static extern IntPtr AllocateClassPair(IntPtr superclass, string name, IntPtr extraBytes);

		[DllImport(LibObjc, EntryPoint = "objc_registerClassPair")]
		public static extern void RegisterClassPair(IntPtr cls);

		[DllImport(LibObjc, EntryPoint = "class_replaceMethod")]
		public static extern IntPtr ReplaceMethod(IntPtr cls, IntPtr selector, IntPtr imp, string types);

		[DllImport(LibObjc, EntryPoint = "objc_autoreleasePoolPush")]
		public static extern IntPtr AutoreleasePoolPush();

		[DllImport(LibObjc, EntryPoint = "objc_autoreleasePoolPop")]
		public static extern void AutoreleasePoolPop(IntPtr pool);

		// id f(id, SEL)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector);

		// id f(id, SEL, id)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1);

		// id f(id, SEL, id, id)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

		// id f(id, SEL, id, SEL, id) - initWithTitle:action:keyEquivalent:
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

		// id f(id, SEL, NSInteger / NSUInteger)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, long arg1);

		// id f(id, SEL, NSUInteger, id) - beginActivityWithOptions:reason:
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, ulong arg1, IntPtr arg2);

		// id f(id, SEL, BOOL)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, byte arg1);

		// id f(id, SEL, CGFloat)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, double arg1);

		// id f(id, SEL, CGFloat, CGFloat)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, double arg1, double arg2);

		// void f(id, SEL, NSPoint)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern void Send(IntPtr receiver, IntPtr selector, CGPoint arg1);

		// void f(id, SEL, NSSize)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern void Send(IntPtr receiver, IntPtr selector, CGSize arg1);

		// void f(id, SEL, NSRect)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern void Send(IntPtr receiver, IntPtr selector, CGRect arg1);

		// void f(id, SEL, NSRect, BOOL) - setFrame:display:
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern void Send(IntPtr receiver, IntPtr selector, CGRect arg1, byte arg2);

		// id f(id, SEL, NSRect, NSUInteger, NSUInteger, BOOL) - initWithContentRect:styleMask:backing:defer:
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, CGRect arg1, ulong arg2, ulong arg3, byte arg4);

		// id f(id, SEL, const char*) - stringWithUTF8String:
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr Send(IntPtr receiver, IntPtr selector, byte[] utf8);

		// BOOL f(id, SEL)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern byte SendBool(IntPtr receiver, IntPtr selector);

		// NSUInteger f(id, SEL)
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern ulong SendULong(IntPtr receiver, IntPtr selector);

		// unsigned short f(id, SEL) - [NSEvent keyCode]
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern ushort SendUShort(IntPtr receiver, IntPtr selector);

		// const char* f(id, SEL) - [NSString UTF8String]
		[DllImport(LibObjc, EntryPoint = "objc_msgSend")]
		public static extern IntPtr SendCString(IntPtr receiver, IntPtr selector);

		[DllImport(CoreGraphics)]
		public static extern int CGGetActiveDisplayList(uint maxDisplays, [Out] uint[] activeDisplays, out uint displayCount);

		[DllImport(CoreGraphics)]
		public static extern CGRect CGDisplayBounds(uint display);

		[DllImport(CoreGraphics)]
		public static extern uint CGDisplayMirrorsDisplay(uint display);

		[DllImport(CoreGraphics)]
		public static extern uint CGMainDisplayID();

		/// <summary>
		/// Returns the (cached) selector for a method name.
		/// </summary>
		public static IntPtr Sel(string name)
		{
			if (SelectorCache.TryGetValue(name, out IntPtr selector))
				return selector;

			selector = RegisterSelector(name);
			SelectorCache[name] = selector;
			return selector;
		}

		/// <summary>
		/// Shorthand for <c>[ClassName selector]</c>.
		/// </summary>
		public static IntPtr SendClass(string className, string selector)
		{
			return Send(GetClass(className), Sel(selector));
		}

		/// <summary>
		/// Creates an autoreleased NSString. Call inside an autorelease pool.
		/// </summary>
		public static IntPtr NSString(string text)
		{
			byte[] bytes = Encoding.UTF8.GetBytes((text ?? string.Empty) + "\0");
			return Send(GetClass("NSString"), Sel("stringWithUTF8String:"), bytes);
		}

		/// <summary>
		/// Copies an NSString into a managed string.
		/// </summary>
		public static string FromNSString(IntPtr nsString)
		{
			if (nsString == IntPtr.Zero)
				return string.Empty;

			IntPtr utf8 = SendCString(nsString, Sel("UTF8String"));
			if (utf8 == IntPtr.Zero)
				return string.Empty;

			int length = 0;
			while (Marshal.ReadByte(utf8, length) != 0)
				++length;

			byte[] bytes = new byte[length];
			Marshal.Copy(utf8, bytes, 0, length);
			return Encoding.UTF8.GetString(bytes);
		}

		/// <summary>
		/// Gets an existing Objective-C class, or creates and registers a new subclass. Methods are
		/// added separately with <see cref="ReplaceMethod"/>, which also re-points them correctly
		/// after a Unity domain reload has invalidated the previous callback thunks.
		/// </summary>
		public static IntPtr GetOrCreateClass(string name, string superclassName)
		{
			IntPtr cls = GetClass(name);
			if (cls != IntPtr.Zero)
				return cls;

			cls = AllocateClassPair(GetClass(superclassName), name, IntPtr.Zero);
			RegisterClassPair(cls);
			return cls;
		}

		[DllImport(LibObjc, EntryPoint = "sel_registerName")]
		private static extern IntPtr RegisterSelector(string name);

		/// <summary>
		/// Point, matching CGPoint / NSPoint on 64-bit macOS.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CGPoint
		{
			public double X;
			public double Y;

			public CGPoint(double x, double y)
			{
				X = x;
				Y = y;
			}
		}

		/// <summary>
		/// Size, matching CGSize / NSSize on 64-bit macOS.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CGSize
		{
			public double Width;
			public double Height;

			public CGSize(double width, double height)
			{
				Width = width;
				Height = height;
			}
		}

		/// <summary>
		/// Rectangle, matching CGRect / NSRect on 64-bit macOS.
		/// </summary>
		[StructLayout(LayoutKind.Sequential)]
		public struct CGRect
		{
			public double X;
			public double Y;
			public double Width;
			public double Height;

			public CGRect(double x, double y, double width, double height)
			{
				X = x;
				Y = y;
				Width = width;
				Height = height;
			}
		}
	}
}
