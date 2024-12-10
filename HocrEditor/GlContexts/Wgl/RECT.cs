using System.Runtime.InteropServices;

namespace HocrEditor.GlContexts.Wgl
{
	[StructLayout(LayoutKind.Sequential)]
    // ReSharper disable once InconsistentNaming
    internal struct RECT
	{
		public int left;
		public int top;
		public int right;
		public int bottom;
	}
}
