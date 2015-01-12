﻿using System;
using sdg = System.Drawing;
using GlyphRunLib;
using System.Collections.Generic;
using System.Text;
using NoForms.Common;
using OTK = OpenTK;
using OpenTK.Graphics.OpenGL;

namespace NoForms.Renderers.OpenTK
{
    // Interleave in this order
    public enum ArrayData { Vertex = 1, Color = 2, Texture = 4 }; // FIXME normals etc, if we ever use em
    public struct RenderInfo
    {
        // SW buffer
        public RenderInfo(int st, int cnt, ArrayData format, PrimitiveType renderas, int tex=0)
        {
            offset = st;
            count = cnt;
            renderAs = renderas;
            dataFormat = format;
            vbo = -1;
            texture = tex;
        }
        // HW buffer
        public RenderInfo(int cnt, ArrayData format, PrimitiveType renderas, int vboID, int tex=0)
        {
            offset = 0;
            count = cnt;
            renderAs = renderas;
            dataFormat = format;
            vbo = vboID;
            texture = tex;
        }
        public readonly PrimitiveType renderAs;
        public readonly ArrayData dataFormat;
        public readonly int offset, count, vbo, texture;
    }
    public class RenderData
    {
        public List<float> sofwareBuffer { get; private set; }
        List<RenderInfo> _bufferInfo;
        public List<RenderInfo> bufferInfo { get { return _bufferInfo; } }
        public RenderData()
        {
            sofwareBuffer = new List<float>();
            _bufferInfo = new List<RenderInfo>();
        }
        public void Clear()
        {
            sofwareBuffer.Clear();
            _bufferInfo.Clear();
        }
    }

    public class OpenTK_RenderElements : IRenderElements
    {
        public OpenTK_RenderElements(OTK.Graphics.IGraphicsContext gc, int fd,int td,int fw,int tw)
        {
            graphicsContext = gc;
            FBO_Draw = fd;
            FBO_Window = fw;
            T2D_Draw = td;
            T2D_Window = tw;
            renderData = new RenderData();
        }
        public OTK.Graphics.IGraphicsContext graphicsContext { get; internal set; }
        public int FBO_Draw { get; internal set; }
        public int T2D_Draw { get; internal set; }
        public int FBO_Window { get; internal set; }
        public int T2D_Window { get; internal set; }
        public RenderData renderData { get; private set; }
    }
    public class OTKDraw : IUnifiedDraw
    {
        OpenTK_RenderElements r;
        public OTKDraw(OpenTK_RenderElements rel)
        {
            r = rel;
        }

        // FIXME the clipping dont work!!
        Stack<Rectangle> Clips = new Stack<Rectangle>();
        public void PushAxisAlignedClip(Rectangle clipRect, bool ignoreRenderOffset)
        {
            return;
            var cr = clipRect;
            OTK.Matrix4 cm = new OTK.Matrix4();
            GL.LoadMatrix(ref cm);
            if (ignoreRenderOffset) cr -= new Point(cm.M31, cm.M32);

            Clips.Push(cr);
            ClipStackViewport();
        }

        public void PopAxisAlignedClip()
        {
            return;
            Clips.Pop();
            if (Clips.Count > 0) ClipStackViewport();
        }

        void ClipStackViewport()
        {
            return;
            Rectangle fcr  = new Rectangle(); 
            bool started = false;
            foreach (var cr in Clips)
            {
                if (!started)
                {
                    started = true;
                    fcr = cr;
                }
                else
                {
                    var l = Math.Max(fcr.left, cr.left);
                    var t = Math.Max(fcr.top, cr.top);
                    var r = Math.Min(fcr.right, cr.right);
                    var b = Math.Min(fcr.bottom, cr.bottom);
                    fcr = new Rectangle(new Point(l, t), new Point(r, b), true);
                }
                if (fcr.width <= 0 || fcr.height <= 0)
                {
                    fcr = new Rectangle(0, 0, 0, 0);
                    break;
                }
            }
            // scissor it!
         //   GL.Scissor((int)fcr.left, (int)fcr.top, (int)fcr.width, (int)fcr.height);
        }

        public void SetRenderOffset(Point renderOffset)
        {
            GL.Translate(new OTK.Vector3(renderOffset.X, renderOffset.Y, 0));
        }

        public void FillPath(UPath path, UBrush brush)
        {
            foreach (var f in path.figures)
            {
                int st = r.renderData.sofwareBuffer.Count;
                BufferFigureVC(f, brush, false, false);
                int cnt = r.renderData.sofwareBuffer.Count - st;
                r.renderData.bufferInfo.Add(new RenderInfo(st, cnt, ArrayData.Vertex | ArrayData.Color, PrimitiveType.Polygon));
            }
        }

        public void DrawPath(UPath path, UBrush brush, UStroke stroke)
        {
            foreach (var f in path.figures)
            {
                int st = r.renderData.sofwareBuffer.Count;
                BufferFigureVC(f, brush, f.open, true);
                int cnt = r.renderData.sofwareBuffer.Count - st;
                r.renderData.bufferInfo.Add(new RenderInfo(st, cnt, ArrayData.Vertex | ArrayData.Color, PrimitiveType.Lines));
            }
        }

        void BufferFigureVC(UFigure f, UBrush brush, bool open, bool doublepoints)
        {
            Point stp = f.startPoint;
            foreach (var g in f.geoElements)
                PlotGeoElement(ref stp, g, brush, doublepoints);
            if (!open)
            {
                setVC(brush, stp.X, stp.Y);
                setVC(brush, f.startPoint.X, f.startPoint.Y);
            }
        }

        void PlotGeoElement(ref Point start, UGeometryBase g, UBrush b, bool doublepoints)
        {
            setVC(b, start.X, start.Y);
            System.Drawing.PointF[] pts = new System.Drawing.PointF[0];
            var lst = start;
            if (g is ULine)
            {
                var l = g as ULine;
                pts = new System.Drawing.PointF[] { new System.Drawing.PointF(l.endPoint.X, l.endPoint.Y) };
            }
            else if (g is UBeizer)
            {
                UBeizer bez = g as UBeizer;
                var p1 = new System.Drawing.PointF(start.X, start.Y);
                var p2 = new System.Drawing.PointF(bez.controlPoint1.X, bez.controlPoint1.Y);
                var p3 = new System.Drawing.PointF(bez.controlPoint2.X, bez.controlPoint2.Y);
                var p4 = new System.Drawing.PointF(bez.endPoint.X, bez.endPoint.Y);
                pts = (g.Retreive<OTKDraw>(() => new disParr(Common.Bezier.Get2D(p1, p2, p3, p4, bez.resolution))) as disParr).pts;
            }
            else if (g is UArc)
            {
                UArc arc = g as UArc;
                pts = (g.Retreive<OTKDraw>(() =>
                {
                    var elInput = new GeometryLib.Ellipse_Input(lst.X, lst.Y, arc.endPoint.X, arc.endPoint.Y, arc.arcSize.width, arc.arcSize.height, arc.rotation);
                    var elSolution = new List<GeometryLib.Ellipse_Output>(GeometryLib.Ellipse.Get_X0Y0(elInput)).ToArray();
                    GeometryLib.Ellipse.SampleArc(elInput, elSolution, arc.reflex, arc.sweepClockwise, arc.resolution, out pts);
                    return new disParr(pts);
                }) as disParr).pts;
            }
            //else if (g is UEasyArc)
            //{
            //    var arc = g as UEasyArc;
            //    pts = (g.Retreive<OTKDraw>(() =>
            //        {
            //            return new disParr(new List<System.Drawing.PointF>(EllipseLib.EasyEllipse.Generate(new EllipseLib.EasyEllipse.EasyEllipseInput()
            //                                {
            //                                    rotation = arc.rotation,
            //                                    start_x = lst.X,
            //                                    start_y = lst.Y,
            //                                    rx = arc.arcSize.width,
            //                                    ry = arc.arcSize.height,
            //                                    t1 = arc.startAngle,
            //                                    t2 = arc.endAngle,
            //                                    resolution = arc.resolution
            //                                })).ToArray());
            //        }) as disParr).pts;
            //}

            else throw new NotImplementedException();
            Point? sp = null;
            int i = 0;
            for (i = 0; i < pts.Length-1;i++ )
            {
                var p = pts[i];
                if (sp == null) sp = new Point(p.X, p.Y);
                setVC(b, p.X, p.Y); // end last line here
                if(doublepoints) setVC(b, p.X, p.Y); // start of new line
            }
            if (pts.Length > 0)
            {
                if (sp == null) sp = new Point(pts[i].X, pts[i].Y);
                setVC(b, pts[i].X, pts[i].Y); // end last line here
            }
            if (sp != null)
            {
                start.X = sp.Value.X;
                start.Y = sp.Value.Y;
            }
        }

        void LoadBitmapIntoTexture(int texture, sdg.Bitmap sdb)
        {
            // lock bitmap data
            var sdbd = sdb.LockBits(new System.Drawing.Rectangle(0, 0, sdb.Width, sdb.Height),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            // Bind texture
            GL.BindTexture(TextureTarget.Texture2D, texture);

            // copy
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, sdbd.Width, sdbd.Height, 0,
                PixelFormat.Bgra, PixelType.UnsignedByte, sdbd.Scan0);

            // unlock
            sdb.UnlockBits(sdbd);

            // do the mimmapthing FIXME for older cards? :/ mmmm 
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

            // Unbind Texture
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }
        void LoadBitmapIntoTexture(int texture, byte[] data)
        {
            // Load Data
            using (var ms = new System.IO.MemoryStream(data))
            {
                var sdb = new System.Drawing.Bitmap(ms);
                LoadBitmapIntoTexture(texture, sdb);
            }
        }

        class distex : IDisposable
        {
            public distex()
            {
                tex = GL.GenTexture();
            }
            public int width { get; private set; }
            public int height { get; private set; }
            public int tex { get; private set; }
            public void Dispose()
            {
                GL.DeleteTexture(tex);
            }
        }

        public void DrawBitmap(UBitmap bitmap, float opacity, UInterp interp, Rectangle source, Rectangle destination)
        {
            var rd = bitmap.Retreive<OTKDraw>(() =>
                {
                    var bd = bitmap.bitmapData ?? System.IO.File.ReadAllBytes(bitmap.bitmapFile);
                    var dt = new distex();
                    LoadBitmapIntoTexture(dt.tex, bd);
                    return dt;
                }) as distex;

            int st = r.renderData.sofwareBuffer.Count;
            bufferTexRect(destination,
                new Rectangle(
                    source.left / rd.width,
                    source.top / rd.height,
                    source.width / rd.width,
                    source.height / rd.height
                    )
                );
            int cnt = r.renderData.sofwareBuffer.Count - st;
            r.renderData.bufferInfo.Add(new RenderInfo(st, cnt, ArrayData.Vertex | ArrayData.Texture, PrimitiveType.Quads));
        }

        void bufferTexRect(Rectangle dest, Rectangle srcFracs)
        {
            r.renderData.sofwareBuffer.AddRange(
                new float[]
                {
                    dest.left, dest.top, srcFracs.left, srcFracs.top,
                    dest.right, dest.top, srcFracs.right, srcFracs.top,
                    dest.right, dest.bottom, srcFracs.right, srcFracs.bottom,
                    dest.left, dest.bottom, srcFracs.left, srcFracs.bottom
                }
                );
        }
        
        public void FillEllipse(Point center, float radiusX, float radiusY, UBrush brush)
        {
            GL.Begin(PrimitiveType.Polygon);
            foreach(var pt in GeometryLib.EllipseUtil.GetEllipse(radiusX, radiusX, .1f))
            {
                float x = pt.X + center.X;
                float y = pt.Y + center.Y;
                setCoordColor(brush,x,y);
                GL.Vertex2(x,y);
            }
            GL.End();
        }

        public void DrawEllipse(Point center, float radiusX, float radiusY, UBrush brush, UStroke stroke)
        {
            GL.Begin(PrimitiveType.LineLoop);
            foreach(var pt in GeometryLib.EllipseUtil.GetEllipse(radiusX, radiusX, .1f))
            {
                float x = pt.X + center.X;
                float y = pt.Y + center.Y;
                setCoordColor(brush, x, y);
                GL.Vertex2(x, y);
            }
            GL.End();
        }

        public void DrawLine(Point start, Point end, UBrush brush, UStroke stroke)
        {
            GL.Begin(PrimitiveType.Quads);
            setCoordColor(brush, start.X, start.Y);
            GL.Vertex2(start.X, start.Y);
            setCoordColor(brush, end.X, end.Y);
            GL.Vertex2(end.X, end.Y);
            GL.End();
        }

        public void DrawRectangle(Rectangle rect, UBrush brush, UStroke stroke)
        {
            // generate sw buffer { Vx,Vy,r,g,b,a } ...
            int st = r.renderData.sofwareBuffer.Count;
            bufferRectData(rect, brush);
            int len = r.renderData.sofwareBuffer.Count - st;
            var ri = new RenderInfo(st, len, ArrayData.Vertex | ArrayData.Color, PrimitiveType.LineLoop);
            r.renderData.bufferInfo.Add(ri);
        }

        public void FillRectangle(Rectangle rect, UBrush brush)
        {
            // generate sw buffer { Vx,Vy,r,g,b,a } ...
            int st = r.renderData.sofwareBuffer.Count;
            bufferRectData(rect, brush);
            int len = r.renderData.sofwareBuffer.Count - st;
            var ri = new RenderInfo(st, len, ArrayData.Vertex | ArrayData.Color, PrimitiveType.Quads);
            r.renderData.bufferInfo.Add(ri);
        }
        //void dr(UBrush b, float x, float y)
        //{
        //    GL.Vertex2(x, y);
        //    setCoordColor(b, x, y);
        //}
        void bufferRectData(Rectangle rect, UBrush brush)
        {
            setVC(brush, rect.left, rect.top);
            setVC(brush, rect.right, rect.top);
            setVC(brush, rect.right, rect.bottom);
            setVC(brush, rect.left, rect.bottom);
        }

        void setVC(UBrush b, float vx, float vy)
        {
            var ssb = r.renderData.sofwareBuffer;
            var cc = getCoordColor(b, vx, vy);
            ssb.Add(vx); 
            ssb.Add(vy);
            ssb.Add(cc.R); 
            ssb.Add(cc.G); 
            ssb.Add(cc.B); 
            ssb.Add(cc.A);
        }

        void setCoordColor(UBrush b, float x, float y)
        {
            GL.Color4(getCoordColor(b, x, y));
        }
        OTK.Graphics.Color4 getCoordColor(UBrush b, float x, float y)
        {
            var ret = new OTK.Graphics.Color4();
            if (b is USolidBrush)
            {
                var usb = b as USolidBrush;
                ret.R = usb.color.r; ret.G = usb.color.g; ret.B = usb.color.b; ret.A = usb.color.a;
            }
            else if (b is ULinearGradientBrush)
            {
                var ulgb = b as ULinearGradientBrush;
                float axisAngle = pointsanglex(ulgb.point1, ulgb.point2);

                float cp1, cp2;
                Color cv1, cv2;
                cp1 = angleaxisxinterp(axisAngle, ulgb.point1.X, ulgb.point1.Y);
                cp2 = angleaxisxinterp(axisAngle, ulgb.point2.X, ulgb.point2.Y);
                cv1 = ulgb.color1; cv2 = ulgb.color2;
                if (cp1 > cp2)
                {
                    cp1 = angleaxisxinterp(axisAngle, ulgb.point2.X, ulgb.point2.Y);
                    cp2 = angleaxisxinterp(axisAngle, ulgb.point1.X, ulgb.point1.Y);
                    cv1 = ulgb.color2; cv2 = ulgb.color1;
                }
                ret = hcolorfour(x, y, axisAngle, cp1, cp2, cv1, cv2);
            }
            return ret;
        }
        OTK.Graphics.Color4 hcolorfour(float x, float y, float a, float cp1, float cp2, Color cv1, Color cv2)
        {
            float iv = onedinterp(angleaxisxinterp(a,x, y), cp1, cp2);
            return new OTK.Graphics.Color4(
                    cv1.r * (1 - iv) + cv2.r * iv,
                    cv1.g * (1 - iv) + cv2.g * iv,
                    cv1.b * (1 - iv) + cv2.b * iv,
                    cv1.a * (1 - iv) + cv2.a * iv);
        }
        // The angle that p1 to p2 makes with the x axis
        float pointsanglex(Point p1, Point p2)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;
            float dh = (float)Math.Sqrt(dx * dx + dy * dy);
            return (float)Math.Acos(dx / dh);
        }
        // transforms x,y point into distance along axis at angle with xaxis
        float angleaxisxinterp(float a, float x, float y)
        {
            return (float)(Math.Cos(a) * x + Math.Sin(a) * y);
        }
        // retuns [0,1] for distance of x between x1 and x2, clamped.
        float onedinterp(float x, float x1, float x2)
        {
            return x < x1 ? 0 : x > x2 ? 1 : (x - x1) / (x2 - x1);
        }

        public void DrawRoundedRectangle(Rectangle rect, float radX, float radY, UBrush brush, UStroke stroke)
        {
            int st = r.renderData.sofwareBuffer.Count;
            RRPoints(rect, radX, radY, brush);
            int cnt = r.renderData.sofwareBuffer.Count - st;
            r.renderData.bufferInfo.Add(new RenderInfo(st,cnt,ArrayData.Vertex | ArrayData.Color, PrimitiveType.LineLoop));
        }
        public void FillRoundedRectangle(Rectangle rect, float radX, float radY, UBrush brush)
        {
            int st = r.renderData.sofwareBuffer.Count;
            RRPoints(rect, radX, radY, brush);
            int cnt = r.renderData.sofwareBuffer.Count - st;
            r.renderData.bufferInfo.Add(new RenderInfo(st,cnt,ArrayData.Vertex | ArrayData.Color, PrimitiveType.Polygon));
        }
        void RRPoints(Rectangle rect, float radX, float radY, UBrush b)
        {
            // top left arc
            ARCPoints(rect.left, rect.top + radY, 90f, 180f, radX, radY, b);
            // line to top right arc start
            setVC(b, rect.right - radX, rect.top);
            //top right arc
            ARCPoints(rect.right - radX, rect.top, 90f, 0f, radX, radY, b);
            // line top bottom right arc
            setVC(b, rect.right, rect.bottom - radY);
            //bottom right arc
            ARCPoints(rect.right, rect.bottom - radY, 0f, -90f, radX, radY, b);
            //line to bottom left arc
            setVC(b, rect.left + radX, rect.bottom);
            //bottom left arc
            ARCPoints(rect.left + radX, rect.bottom, -90f, -180f, radX, radY, b);
            // line back to top left arc
            setVC(b, rect.left, rect.top + radY);
        }
        void ARCPoints(float sx, float sy, float t1, float t2, float rx, float ry, UBrush b)
        {
            var e_tl = GeometryLib.EasyEllipse.Generate(new GeometryLib.EasyEllipse.EasyEllipseInput()
            {
                rx = rx,
                ry = ry,
                rotation = 0,
                resolution = .1f,
                start_x = sx,
                start_y = sy,
                t1 = t1,
                t2 = t2
            });
            foreach (var pt in e_tl)
                setVC(b, pt.X, pt.Y);
        }

        class GLTextStore<T> : IDisposable
        {
            // Cached from gettextinfo
            public GlyphRunGenerator<T>.UTextGlyphingInfo tinfo = null;

            // Cached when any hit,measure or draw method fires.
            public sdg.Graphics sbb_context = null;
            public sdg.Bitmap softbitbuf = null;

            // Cached when DrawText is called (the software bitmap also gets enlarged to correct size...FIXME keep that? :/)
            public int texture_for_blitting = -1; // i.e. not created yet

            public void Dispose()
            {
                throw new NotImplementedException();
            }
        }

        static sdg.Font FTR(UFont uf) { return new sdg.Font(uf.name, uf.size, (uf.italic ? sdg.FontStyle.Italic : 0) | (uf.bold ? sdg.FontStyle.Bold : 0)); }
        static GLTextStore<OTKDraw> EnsureStoredData(UText ut)
        {
            var GLts = ut.Retreive() as GLTextStore<OTKDraw>;
            if (GLts.softbitbuf == null)
            {
                GLts.softbitbuf = new sdg.Bitmap(0, 0); // FIXME will this cause problems @ 0x0? :/
                GLts.sbb_context = sdg.Graphics.FromImage(GLts.softbitbuf); // make context
            }
            return GLts;
        }
        static GLTextStore<OTKDraw> EnsureStoredData(UText ut, Size blitSize)
        {
            var GLts = ut.Retreive() as GLTextStore<OTKDraw>;
            if (GLts.softbitbuf == null)
            {
                // make bitmap right size FIXME is the dipose necessary? :/
                GLts.sbb_context.Dispose(); GLts.softbitbuf.Dispose();
                GLts.softbitbuf = new sdg.Bitmap((int)blitSize.width, (int)blitSize.height);
                GLts.sbb_context = sdg.Graphics.FromImage(GLts.softbitbuf);
            }
            return GLts; 
        }
        // Generator for glyphruns and related text info
        GlyphRunGenerator<System.Drawing.Font> glyphRunner = new GlyphRunGenerator<System.Drawing.Font>(
            // Measure Text
            (ut, s, f) =>
            {
                var GLts = EnsureStoredData(ut);
                var ms = GLts.sbb_context.MeasureString(s, f, sdg.PointF.Empty, sdg.StringFormat.GenericTypographic);
                return new Size(ms.Width, ms.Height);
            },
            // Create Implimentation Font
            uf => FTR(uf)
            );
        
        public UTextInfo GetTextInfo(UText text)
        {
            var datastore = GetTextData(text);
            // ...which we will fill as necessary.
            var ti = datastore.tinfo ?? (datastore.tinfo = glyphRunner.GetTextInfo(text));

            return new UTextInfo()
            {
                minSize = ti.minSize,
                lineLengths = ti.lineLengths.ToArray(),
                lineNewLineLength = ti.newLineLengths.ToArray(),
                numLines = ti.lineLengths.Count
            };
        }
        GLTextStore<sdg.Font> GetTextData(UText text)
        {
            // On NoCache, just return an uninitialised dude...
            return text.Retreive<OTKDraw>(() => new GLTextStore<sdg.Font>()) as GLTextStore<sdg.Font>;
        }

        public void DrawText(UText textObject, NoForms.Common.Point location, UBrush defBrush, UTextDrawOptions opt, bool clientRendering)
        {
            GLTextStore<sdg.Font> GLds  = GetTextData(textObject);
            if (GLds.texture_for_blitting == -1)
            {
                var tl = GLds.tinfo;
                float l = float.MaxValue, t = float.MaxValue, b = float.MinValue, r = float.MinValue;

                // Calculate the render rectangle
                foreach (var gr in tl.glyphRuns)
                {
                    var p1 = gr.location;
                    var p2 = new Point(p1.Y + gr.run.runSize.width, p1.Y + gr.run.runSize.height);
                    if (p1.X < l) l = p1.X;
                    if (p1.Y < t) t = p1.Y;
                    if (p2.X > r) r = p2.X;
                    if (p2.Y > b) b = p2.Y;
                }
                Rectangle rrect = new Rectangle(new Point(l, t), new Point(r, b), true);
                System.Diagnostics.Debug.Assert(rrect.width > 0 && rrect.height > 0);

                // ensure we have the software buffer
                EnsureStoredData(textObject, rrect.Size);
                // we will draw black onto transparent background, and handle brushes when rendering to blitting texture
                GLds.sbb_context.Clear(sdg.Color.Transparent);
                foreach (var glyphrun in tl.glyphRuns)
                {
                    var style = glyphrun.run.drawStyle;
                    UFont font = style != null ? (style.fontOverride ?? textObject.font) : textObject.font;
                    sdg.FontStyle fs = (font.bold ? sdg.FontStyle.Bold : 0) | (font.italic ? sdg.FontStyle.Italic : 0);
                    var sdgFont = FTR(font);
                    UBrush brsh = style != null ? (style.fgOverride ?? defBrush) : defBrush;
                    if (style != null && style.bgOverride != null)
                        FillRectangle(new NoForms.Common.Rectangle(glyphrun.location, glyphrun.run.runSize), style.bgOverride);
                    GLds.sbb_context.DrawString(glyphrun.run.content, sdgFont, sdg.Brushes.Black, PTR(location + glyphrun.location), sdg.StringFormat.GenericTypographic);
                }

                // now copy into the blit mask
                GLds.texture_for_blitting = GL.GenTexture();
                LoadBitmapIntoTexture(GLds.texture_for_blitting, GLds.softbitbuf);
            }


            // use blit mask to create a blitting texture, using the Util fbo (FIXME? but no multithreads...)
            throw new NotImplementedException();
            

            // blit the buffer (FIXME thats not blitting) FIXME needs seperate (what about masked tezture alpbas!)
            var gs = GLds.softbitbuf.Size;
            Rectangle rr = new Rectangle(location, new  Size(gs.Width,gs.Height));
            GL.BlendFunc(BlendingFactorSrc.Zero, BlendingFactorDest.SrcAlpha);
            GL.Begin(PrimitiveType.Quads);
            GL.TexCoord2(0, 0);
            GL.Vertex2(rr.left, rr.top);
            GL.TexCoord2(1, 0);
            GL.Vertex2(rr.right, rr.top);
            GL.TexCoord2(1, 1);
            GL.Vertex2(rr.right, rr.bottom);
            GL.TexCoord2(0, 1);
            GL.Vertex2(rr.left, rr.bottom);
            GL.End();
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
        }
        sdg.PointF PTR(Point pt)
        {
            return new sdg.PointF(pt.X, pt.Y);
        }

        public UTextHitInfo HitPoint(NoForms.Common.Point hitPoint, UText text)
        {
            var ti = GetTextData(text);
            return glyphRunner.HitPoint(ti.tinfo, hitPoint);
        }

        public NoForms.Common.Point HitText(int pos, bool trailing, UText text)
        {
            var ti = GetTextData(text);
            return glyphRunner.HitText(ti.tinfo, pos, trailing);
        }

        public IEnumerable<NoForms.Common.Rectangle> HitTextRange(int start, int length, NoForms.Common.Point offset, UText text)
        {
            var ti = GetTextData(text);
            return glyphRunner.HitTextRange(ti.tinfo, start, length, offset);
        }


    }
}