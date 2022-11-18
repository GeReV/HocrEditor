using System;
using SkiaSharp;

namespace HocrEditor.GlContexts.Wgl
{
	internal class WglContext : GlContext
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
				style = User32.CS_HREDRAW | User32.CS_VREDRAW | User32.CS_OWNDC
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

			if (!HocrEditor.GlContexts.Wgl.WglFunctions.HasExtension(fDeviceContext, "WGL_ARB_pixel_format") ||
				!HocrEditor.GlContexts.Wgl.WglFunctions.HasExtension(fDeviceContext, "WGL_ARB_pbuffer"))
			{
				DestroyWindow();
				throw new Exception("DC does not have extensions.");
			}
		}

		public WglContext()
		{
			var iAttrs = new int[]
			{
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_ACCELERATION_ARB, HocrEditor.GlContexts.Wgl.WglFunctions.WGL_FULL_ACCELERATION_ARB,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_DRAW_TO_WINDOW_ARB, HocrEditor.GlContexts.Wgl.WglFunctions.TRUE,
				//Wgl.WGL_DOUBLE_BUFFER_ARB, (doubleBuffered ? TRUE : FALSE),
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_SUPPORT_OPENGL_ARB, HocrEditor.GlContexts.Wgl.WglFunctions.TRUE,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_RED_BITS_ARB, 8,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_GREEN_BITS_ARB, 8,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_BLUE_BITS_ARB, 8,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_ALPHA_BITS_ARB, 8,
				HocrEditor.GlContexts.Wgl.WglFunctions.WGL_STENCIL_BITS_ARB, 8,
				HocrEditor.GlContexts.Wgl.WglFunctions.NONE, HocrEditor.GlContexts.Wgl.WglFunctions.NONE
			};
			var piFormats = new int[1];
			uint nFormats;
			HocrEditor.GlContexts.Wgl.WglFunctions.wglChoosePixelFormatARB(fDeviceContext, iAttrs, null, (uint)piFormats.Length, piFormats, out nFormats);
			if (nFormats == 0)
			{
				Destroy();
				throw new Exception("Could not get pixel formats.");
			}

			fPbuffer = HocrEditor.GlContexts.Wgl.WglFunctions.wglCreatePbufferARB(fDeviceContext, piFormats[0], 1, 1, null);
			if (fPbuffer == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not create Pbuffer.");
			}

			fPbufferDC = HocrEditor.GlContexts.Wgl.WglFunctions.wglGetPbufferDCARB(fPbuffer);
			if (fPbufferDC == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not get Pbuffer DC.");
			}

			var prevDC = HocrEditor.GlContexts.Wgl.WglFunctions.wglGetCurrentDC();
			var prevGLRC = HocrEditor.GlContexts.Wgl.WglFunctions.wglGetCurrentContext();

			fPbufferGlContext = HocrEditor.GlContexts.Wgl.WglFunctions.wglCreateContext(fPbufferDC);

			HocrEditor.GlContexts.Wgl.WglFunctions.wglMakeCurrent(prevDC, prevGLRC);

			if (fPbufferGlContext == IntPtr.Zero)
			{
				Destroy();
				throw new Exception("Could not creeate Pbuffer GL context.");
			}
		}

		public override void MakeCurrent()
		{
			if (!HocrEditor.GlContexts.Wgl.WglFunctions.wglMakeCurrent(fPbufferDC, fPbufferGlContext))
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
			if (!HocrEditor.GlContexts.Wgl.WglFunctions.HasExtension(fPbufferDC, "WGL_ARB_pbuffer"))
			{
				// ASSERT
			}

			HocrEditor.GlContexts.Wgl.WglFunctions.wglDeleteContext(fPbufferGlContext);

			HocrEditor.GlContexts.Wgl.WglFunctions.wglReleasePbufferDCARB?.Invoke(fPbuffer, fPbufferDC);

			HocrEditor.GlContexts.Wgl.WglFunctions.wglDestroyPbufferARB?.Invoke(fPbuffer);
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
			HocrEditor.GlContexts.Wgl.WglFunctions.glGenTextures(textures.Length, textures);
			var textureId = textures[0];

			HocrEditor.GlContexts.Wgl.WglFunctions.glBindTexture(HocrEditor.GlContexts.Wgl.WglFunctions.GL_TEXTURE_2D, textureId);
			HocrEditor.GlContexts.Wgl.WglFunctions.glTexImage2D(HocrEditor.GlContexts.Wgl.WglFunctions.GL_TEXTURE_2D, 0, HocrEditor.GlContexts.Wgl.WglFunctions.GL_RGBA, textureSize.Width, textureSize.Height, 0, HocrEditor.GlContexts.Wgl.WglFunctions.GL_RGBA, HocrEditor.GlContexts.Wgl.WglFunctions.GL_UNSIGNED_BYTE, IntPtr.Zero);
			HocrEditor.GlContexts.Wgl.WglFunctions.glBindTexture(HocrEditor.GlContexts.Wgl.WglFunctions.GL_TEXTURE_2D, 0);

			return new GRGlTextureInfo
			{
				Id = textureId,
				Target = HocrEditor.GlContexts.Wgl.WglFunctions.GL_TEXTURE_2D,
				Format = HocrEditor.GlContexts.Wgl.WglFunctions.GL_RGBA8
			};
		}

		public override void DestroyTexture(uint texture)
		{
			HocrEditor.GlContexts.Wgl.WglFunctions.glDeleteTextures(1, new[] { texture });
		}
	}
}
