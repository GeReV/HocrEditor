using System;
using System.Runtime.InteropServices;

namespace HocrEditor.GlContexts.Wgl
{
	internal class Kernel32
	{
		private const string KERNEL32 = "kernel32.dll";

		static Kernel32()
		{
			CurrentModuleHandle = GetModuleHandle(lpModuleName: null);
			if (CurrentModuleHandle == IntPtr.Zero)
			{
				throw new Exception("Could not get module handle.");
			}
		}

		public static IntPtr CurrentModuleHandle { get; }

		[DllImport(KERNEL32, CallingConvention = CallingConvention.Winapi, BestFitMapping = false, ThrowOnUnmappableChar = true)]
		public static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPTStr)] string? lpModuleName);
	}
}
