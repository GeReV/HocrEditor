using System;
using SkiaSharp;

namespace HocrEditor.GlContexts.Wgl
{
	internal sealed class WglContext : GlContext
	{
		private static ushort gWC;

		private static IntPtr fWindow;
		private static IntPtr fDeviceContext;

		private IntPtr fPbuffer;
		private IntPtr fPbufferDC;
		private IntPtr fPbufferGlContext;

		private static WNDCLASS wc;

		static WglContext()
		{
			wc = new WNDCLASS
			{
				cbClsExtra = 0,
				cbWndExtra = 0,
				hbrBackground = IntPtr.Zero,
				hCursor = User32.LoadCursor(IntPtr.Zero, (int)User32.IDC_ARROW),
				hIcon = User32.LoadIcon(IntPtr.Zero, (IntPtr)User32.IDI_APPLICATION),
				hInstance = Kernel32.CurrentModuleHandle,
				lpfnWndProc = (WNDPROC)User32.DefWindowProc,
				lpszClassName = "Griffin",
				lpszMenuName = null,
				style = User32.CS_HREDRAW | User32.CS_VREDRAW | User32.CS_OWNDC,
			};

			gWC = User32.RegisterClass(ref wc);
			if (gWC == 0)
			{
				throw new Exception("Could not register window class.");
			}

			fWindow = User32.CreateWindow(
				"Griffin",
				"The Invisible Man",
				WindowStyles.WS_OVERLAPPEDWINDOW,
				0, 0,
				1, 1,
				IntPtr.Zero, IntPtr.Zero, Kernel32.CurrentModuleHandle, IntPtr.Zero);
			if (fWindow == IntPtr.Zero)
			{
				throw new Exception($"Could not create window.");
			}

			fDeviceContext = User32.GetDC(fWindow);
			if (fDeviceContext == IntPtr.Zero)
			{
				DestroyWindow();
				throw new Exception("Could not get device context.");
			}

			if (!WglFunctions.HasExtension(fDeviceContext, "WGL_ARB_pixel_format") ||
				!WglFunctions.HasExtension(fDeviceContext, "WGL_ARB_pbuffer"))
			{
				DestroyWindow();
				throw new Exception("DC does not have extensions.");
			}
		}

		public WglContext()
		{
			var iAttrs = new int[]
			{
				WglFunctions.WGL_ACCELERATION_ARB, WglFunctions.WGL_FULL_ACCELERATION_ARB,
				WglFunctions.WGL_DRAW_TO_WINDOW_ARB, WglFunctions.TRUE,
				//Wgl.WGL_DOUBLE_BUFFER_ARB, (doubleBuffered ? TRUE : FALSE),
				WglFunctions.WGL_SUPPORT_OPENGL_ARB, WglFunctions.TRUE,
				WglFunctions.WGL_RED_BITS_ARB, 8,
				WglFunctions.WGL_GREEN_BITS_ARB, 8,
				WglFunctions.WGL_BLUE_BITS_ARB, 8,
				WglFunctions.WGL_ALPHA_BITS_ARB, 8,
				WglFunctions.WGL_STENCIL_BITS_ARB, 8,
				WglFunctions.NONE, WglFunctions.NONE
			};
			var piFormats = new int[1];
			uint nFormats = 0;
			WglFunctions.WglChoosePixelFormatArb?.Invoke(fDeviceContext, iAttrs, attribFList: null, (uint)piFormats.Length, piFormats, out nFormats);
			if (nFormats == 0)
			{
				Destroy();
				throw new Exception("Could not get pixel formats.");
			}

			fPbuffer = WglFunctions.WglCreatePbufferArb?.Invoke(fDeviceContext, piFormats[0], 1, 1, attribList: null) ?? IntPtr.Zero;
			if (fPbuffer == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not create Pbuffer.");
			}

			fPbufferDC = WglFunctions.WglGetPbufferDcarb?.Invoke(fPbuffer) ?? IntPtr.Zero;
			if (fPbufferDC == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not get Pbuffer DC.");
			}

			var prevDC = WglFunctions.wglGetCurrentDC();
			var prevGLRC = WglFunctions.wglGetCurrentContext();

			fPbufferGlContext = WglFunctions.wglCreateContext(fPbufferDC);

			WglFunctions.wglMakeCurrent(prevDC, prevGLRC);

			if (fPbufferGlContext == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not ceeate Pbuffer GL context.");
			}
		}

		public override void MakeCurrent()
		{
			if (!WglFunctions.wglMakeCurrent(fPbufferDC, fPbufferGlContext))
			{
				Destroy();
				throw new Exception("Could not set the context.");
			}
		}

		public override void SwapBuffers()
		{
			if (!Gdi32.SwapBuffers(fPbufferDC))
			{
				Destroy();
				throw new Exception("Could not complete SwapBuffers.");
			}
		}

		public override void Destroy()
		{
			if (!WglFunctions.HasExtension(fPbufferDC, "WGL_ARB_pbuffer"))
			{
				// ASSERT
			}

			WglFunctions.wglDeleteContext(fPbufferGlContext);

			WglFunctions.WglReleasePbufferDcarb?.Invoke(fPbuffer, fPbufferDC);

			WglFunctions.WglDestroyPbufferArb?.Invoke(fPbuffer);
		}

		private static void DestroyWindow()
		{
			if (fWindow != IntPtr.Zero)
			{
				if (fDeviceContext != IntPtr.Zero)
				{
					User32.ReleaseDC(fWindow, fDeviceContext);
					fDeviceContext = IntPtr.Zero;
				}

				User32.DestroyWindow(fWindow);
				fWindow = IntPtr.Zero;
			}

			User32.UnregisterClass("Griffin", Kernel32.CurrentModuleHandle);
		}

		public override GRGlTextureInfo CreateTexture(SKSizeI textureSize)
		{
			var textures = new uint[1];
			WglFunctions.glGenTextures(textures.Length, textures);
			var textureId = textures[0];

			WglFunctions.glBindTexture(WglFunctions.GL_TEXTURE_2D, textureId);
			WglFunctions.glTexImage2D(WglFunctions.GL_TEXTURE_2D, 0, WglFunctions.GL_RGBA, textureSize.Width, textureSize.Height, 0, WglFunctions.GL_RGBA, WglFunctions.GL_UNSIGNED_BYTE, IntPtr.Zero);
			WglFunctions.glBindTexture(WglFunctions.GL_TEXTURE_2D, 0);

			return new GRGlTextureInfo
			{
				Id = textureId,
				Target = WglFunctions.GL_TEXTURE_2D,
				Format = WglFunctions.GL_RGBA8
			};
		}

		public override void DestroyTexture(uint texture)
		{
			WglFunctions.glDeleteTextures(1, new[] { texture });
		}
	}
}
