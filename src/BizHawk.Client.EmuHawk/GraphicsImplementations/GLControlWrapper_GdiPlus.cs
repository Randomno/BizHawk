using System.Drawing.Drawing2D;
using System.Windows.Forms;

using BizHawk.Bizware.BizwareGL;

namespace BizHawk.Client.EmuHawk
{
	public class GLControlWrapper_GdiPlus : Control, IGraphicsControl
	{
		public GLControlWrapper_GdiPlus(IGL_GdiPlus gdi)
		{
			_gdi = gdi;
			SetStyle(ControlStyles.UserPaint, true);
			SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
			SetStyle(ControlStyles.Opaque, true);
			SetStyle(ControlStyles.UserMouse, true);
		}

		private readonly IGL_GdiPlus _gdi;

		/// <summary>
		/// the render target for rendering to this control
		/// </summary>
		public RenderTargetWrapper RenderTargetWrapper { get; set; }

		public void SetVsync(bool state)
		{
			// not really supported now...
		}

		public void Begin()
		{
			_gdi.BeginControl(this);
			RenderTargetWrapper.CreateGraphics();

#if false
			using (var g = CreateGraphics())
			{
				MyBufferedGraphics = _gdi.MyBufferedGraphicsContext.Allocate(g, ClientRectangle);
			}

			MyBufferedGraphics.Graphics.PixelOffsetMode = PixelOffsetMode.HighSpeed;

			// not sure about this stuff...
			// it will wreck alpha blending, for one thing
			MyBufferedGraphics.Graphics.CompositingMode = CompositingMode.SourceCopy;
			MyBufferedGraphics.Graphics.CompositingQuality = CompositingQuality.HighSpeed;
#endif
		}

		public void End()
		{
			_gdi.EndControl();
		}

		public void SwapBuffers()
		{
			if (RenderTargetWrapper.MyBufferedGraphics == null)
			{
				return;
			}

			using (var g = CreateGraphics())
			{
				// not sure we had proof we needed this but it cant hurt
				g.CompositingMode = CompositingMode.SourceCopy;
				g.CompositingQuality = CompositingQuality.HighSpeed;
				RenderTargetWrapper.MyBufferedGraphics.Render(g);
			}

			// not too sure about this.. i think we have to re-allocate it so we can support a changed window size. did we do this at the right time anyway?
			// maybe I should try caching harder, I hate to reallocate these constantly
			RenderTargetWrapper.CreateGraphics();
		}
	}
}