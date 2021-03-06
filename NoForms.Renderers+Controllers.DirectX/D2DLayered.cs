﻿using System;
using System.Threading;
using System.Diagnostics;
using STimer = System.Threading.Timer;
using SharpDX.Direct3D10;
using SharpDX.DXGI;
using SharpDX.Direct2D1;
using SharpDX;
using NoForms.Common;
using NoForms.Renderers;
using NoForms.Platforms.Win32;
using SharpDXLib = SharpDX;

namespace NoForms.Renderers.SharpDX
{
    public class D2DLayered : IRender<IW32Win>, IDraw
    {


        #region IRender - Bulk of class, providing rendering control (specialised to particular IWindow)
        SharpDXLib.Direct3D10.Device1 device;
        SharpDXLib.Direct2D1.Factory d2dFactory = new SharpDXLib.Direct2D1.Factory();
        SharpDXLib.DXGI.Factory dxgiFactory = new SharpDXLib.DXGI.Factory();

        Texture2D backBuffer;
        RenderTargetView renderView;
        Surface1 surface;
        RenderTarget d2dRenderTarget;
        IntPtr someDC;
        IW32Win w32;

        DirtyObserver dobs;
        public void Dirty(Common.Rectangle dr) { dobs.Dirty(dr); }

        public float FPSLimit { get; set; }
        public D2DLayered()
        {
            FPSLimit = 60;
            device = new SharpDXLib.Direct3D10.Device1(DriverType.Hardware, DeviceCreationFlags.BgraSupport, SharpDXLib.Direct3D10.FeatureLevel.Level_10_1);
            someDC = Win32Util.GetDC(IntPtr.Zero); // not sure what this exactly does... root dc perhaps...
        }
        public void Init(IW32Win w32, NoForm root)
        {
            this.w32 = w32;
            noForm = root;
            noForm.renderer = this;
            lock (noForm)
            {
                var sz = root.Size;
                // Initialise d2d things
                backBuffer = new Texture2D(device, new Texture2DDescription()
                {
                    ArraySize = 1,
                    MipLevels = 1,
                    SampleDescription = new SampleDescription(1, 0),
                    OptionFlags = ResourceOptionFlags.GdiCompatible,
                    Width = (int)sz.width,
                    Height = (int)sz.height,
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.RenderTarget,
                    Format = Format.B8G8R8A8_UNorm,
                });
                renderView = new RenderTargetView(device, backBuffer);
                surface = backBuffer.QueryInterface<Surface1>();
                d2dRenderTarget = new RenderTarget(d2dFactory, surface, new RenderTargetProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));
                scbTrans = new SolidColorBrush(d2dRenderTarget, new Color4(1f, 0f, 1f, 0f)); // set buffer area to transparent

                // Init uDraw and assign IRenderElement parts
                _backRenderer = new SharpDX_RenderElements(d2dRenderTarget);
                _uDraw = new D2DDraw(_backRenderer);

                // Create the observer
                dobs = new DirtyObserver(noForm, RenderPass, () => noForm.DirtyAnimated, () => noForm.ReqSize, () => FPSLimit);    
            }
        }

        IntPtr hWnd;
        SolidColorBrush scbTrans;
        public void BeginRender()
        {
            // Make sure it gets layered!
            hWnd = w32.handle;
            var wl = Win32Util.GetWindowLong(hWnd, Win32Util.GWL_EXSTYLE);
            var ret = Win32Util.SetWindowLong(hWnd, Win32Util.GWL_EXSTYLE, wl | Win32Util.WS_EX_LAYERED);

            // hook move!
            noForm.LocationChanged += noForm_LocationChanged;

            // Start the watcher
            dobs.Dirty(noForm.DisplayRectangle);
            dobs.running = true;
            dobs.StartObserving();
            
            noForm_LocationChanged(noForm.Location);
        }

        void noForm_LocationChanged(Point obj)
        {
            Win32Util.SetWindowLocation(new Win32Util.Point((int)noForm.Location.X, (int)noForm.Location.Y), hWnd);
        }
       
        public void EndRender()
        {
            lock (dobs.lock_render)
            {
                // Free unmanaged stuff
                noForm.LocationChanged -= noForm_LocationChanged;
                dobs.running = false;
                scbTrans.Dispose();
                d2dRenderTarget.Dispose();
                surface.Dispose();
                renderView.Dispose();
                backBuffer.Dispose();
            }
        }

        public event Action<Size> RenderSizeChanged = delegate { };

        // object because IRender could be anything, gdi, opengl etc...
        public NoForm noForm { get; set; }
        Stopwatch renderTime = new Stopwatch();
        public float lastFrameRenderDuration { get; private set; }
        void RenderPass(Common.Region dc, Common.Size ReqSize)
        {
            renderTime.Start();
            // FIXME so much object spam and disposal in this very high frequency function (also inside Resize called belw).  My poor megabytes!

            // Resize the form and backbuffer to noForm.Size, and fire the noForms sizechanged
            Resize(ReqSize);

            // make size...
            Win32Util.Size w32Size = new Win32Util.Size((int)ReqSize.width, (int)ReqSize.height); 
            Win32Util.SetWindowSize(w32Size, hWnd);

            // Allow noform size to change as requested..like a layout hook (truncating layout passes with the render passes for performance)
            RenderSizeChanged(ReqSize);

            lock (noForm)
            {
                // Do Drawing stuff
                DrawingSize rtSize = new DrawingSize((int)d2dRenderTarget.Size.Width, (int)d2dRenderTarget.Size.Height);
                using (Texture2D t2d = new Texture2D(backBuffer.Device, backBuffer.Description))
                {
                    using (Surface1 srf = t2d.QueryInterface<Surface1>())
                    {
                        using (RenderTarget trt = new RenderTarget(d2dFactory, srf, new RenderTargetProperties(d2dRenderTarget.PixelFormat)))
                        {
                            _backRenderer.renderTarget = trt;
                            trt.BeginDraw();
                            noForm.DrawBase(this, dc);
                            // Fill with transparency the edgeBuffer!
                            trt.FillRectangle(new RectangleF(0, noForm.Size.height, noForm.Size.width + edgeBufferSize, noForm.Size.height + edgeBufferSize), scbTrans);
                            trt.FillRectangle(new RectangleF(noForm.Size.width, 0, noForm.Size.width + edgeBufferSize, noForm.Size.height + edgeBufferSize), scbTrans);
                            trt.EndDraw();

                            foreach (var rc in dc.AsRectangles())
                                t2d.Device.CopySubresourceRegion(t2d, 0,
                                    new ResourceRegion() { Left = (int)rc.left, Right = (int)rc.right, Top = (int)rc.top, Bottom = (int)rc.bottom, Back = 1, Front = 0 }, backBuffer, 0,
                                    (int)rc.left, (int)rc.top, 0);
                        }
                    }
                }

                // Present DC to windows (ugh layered windows sad times)
                IntPtr dxHdc = surface.GetDC(false);
                System.Drawing.Graphics dxdc = System.Drawing.Graphics.FromHdc(dxHdc);
                Win32Util.Point dstPoint = new Win32Util.Point((int)(noForm.Location.X), (int)(noForm.Location.Y ));
                Win32Util.Point srcPoint = new Win32Util.Point(0,0);
                Win32Util.Size pSize = new Win32Util.Size(rtSize.Width,rtSize.Height);
                Win32Util.BLENDFUNCTION bf = new Win32Util.BLENDFUNCTION() { SourceConstantAlpha = 255, AlphaFormat = Win32Util.AC_SRC_ALPHA, BlendFlags = 0, BlendOp = 0 };

                bool suc = Win32Util.UpdateLayeredWindow(hWnd, someDC, ref dstPoint, ref pSize, dxHdc, ref srcPoint, 1, ref bf, 2);

                surface.ReleaseDC();
                dxdc.Dispose();
            }
            lastFrameRenderDuration = 1f / (float)renderTime.Elapsed.TotalSeconds;
            renderTime.Reset();

        }
        public int edgeBufferSize = 128;
        void Resize(Size ReqSize)
        {
            // Initialise d2d things
            var nbb = new Texture2D(device, new Texture2DDescription()
            {
                ArraySize = 1,
                MipLevels = 1,
                SampleDescription = new SampleDescription(1, 0),
                OptionFlags = ResourceOptionFlags.GdiCompatible,
                Width = (int)ReqSize.width + edgeBufferSize,
                Height = (int)ReqSize.height + edgeBufferSize,
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget,
                Format = Format.B8G8R8A8_UNorm
            });

            ResourceRegion rrgn = new ResourceRegion()
            {
                Front = 0,
                Back = 1,
                Top = 0,
                Left = 0,
                Right = (int)ReqSize.width,
                Bottom = (int)ReqSize.height
            };

            //int bef = HashResource(nbb);
            device.CopySubresourceRegion(backBuffer, 0, rrgn, nbb, 0, 0, 0, 0);
            //int aft = HashResource(nbb);

            backBuffer.Dispose();
            backBuffer = nbb;

            renderView.Dispose();
            surface.Dispose();
            d2dRenderTarget.Dispose();

            renderView = new RenderTargetView(device, backBuffer);
            surface = backBuffer.QueryInterface<Surface1>();
            d2dRenderTarget = new RenderTarget(d2dFactory, surface, new RenderTargetProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied)));
        }
        public void Dispose()
        {
            d2dFactory.Dispose();
            dxgiFactory.Dispose();
            device.Dispose();
        }
        #endregion

        #region IDraw - Client facing interface to drawing commands and so on
        IUnifiedDraw _uDraw;
        public IUnifiedDraw uDraw
        {
            get { return _uDraw; }
        }
        SharpDX_RenderElements _backRenderer;
        public IRenderElements backRenderer
        {
            get { return _backRenderer; }
        }
        public UnifiedEffects uAdvanced
        {
            get { throw new NotImplementedException(); }
        }
        #endregion

        
      }    
}
