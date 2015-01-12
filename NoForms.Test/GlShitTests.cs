﻿using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using OpenTK.Graphics.OpenGL;
using NoForms.Renderers.OpenTK;

namespace NoForms.Test
{
    
    [TestFixture]
    public class OTKRenderTests
    {
        RenderData rd;
        MockGLBuffer buf;
        RenderProcessor rp;
        [SetUp]
        public void SetUp()
        {
            rd = new Renderers.OpenTK.RenderData();
            buf = new MockGLBuffer();
            rp = new RenderProcessor(buf);
        }

        [Test] public void RenderQuadsV() { IncrementalIndexTest(PrimitiveType.Quads, true, false); }
        [Test] public void RenderQuadsVC() { IncrementalIndexTest(PrimitiveType.Quads, true, true); }
        [Test] public void RenderLinesV() { IncrementalIndexTest(PrimitiveType.Lines, true, false); }
        [Test] public void RenderLinesVC() { IncrementalIndexTest(PrimitiveType.Lines, true, true); }
        void IncrementalIndexTest(PrimitiveType pt,bool ver, bool col)
        {
            // test one, just put vertex data in only, same stride, same lenghts.  Check the correct data ends up back in.
            int nd = 5 * buf.primsizes[pt] * (2 + 4); // 5 quads
            for (int i = 0; i < 3; i++) // 3 times
            {
                int st = i * nd;
                ArrayData ad = 0 | (ver ? ArrayData.Vertex : 0) | (col ? ArrayData.Color : 0);
                var r = new RenderInfo(st, nd, ad, pt);
                for (int j = st; j < st + nd; j++)
                    rd.sofwareBuffer.Add(j);
                rd.bufferInfo.Add(r);
            }
            rp.ProcessRenderBuffer(rd);

            // check we haaave got ...erm...vertexes
            int chk = 0;
            foreach (var p in buf.renderlist)
            {
                Assert.AreEqual(p.pt, pt);
                foreach (var d in p.data)
                {
                    Assert.IsNull(d.t);
                    if (ver)
                    {
                        Assert.AreEqual(d.v.x, chk++);
                        Assert.AreEqual(d.v.y, chk++);
                    }
                    else Assert.IsNull(d.v);
                    if (col)
                    {
                        Assert.AreEqual(d.c.a, chk++);
                        Assert.AreEqual(d.c.r, chk++);
                        Assert.AreEqual(d.c.g, chk++);
                        Assert.AreEqual(d.c.b, chk++);
                    }
                    else Assert.IsNull(d.c);
                }
            }
            buf.ResetBuffer();
        }

        [Test]
        public void RenderQuadsAndLinesV()
        {
            List<float[]> dt = new List<float[]> { new float[0], new float[0], new float[0], new float[0] };
            List<PrimitiveType> dtp = new List<PrimitiveType> { PrimitiveType.Quads, PrimitiveType.Lines, PrimitiveType.Quads, PrimitiveType.Lines };

            // few quads
            rd.bufferInfo.Add(new RenderInfo(0, 4 * 3 * 2, ArrayData.Vertex, PrimitiveType.Quads));
            rd.sofwareBuffer.AddRange(dt[0] = new float[] 
            {
                1,5, 4,5, 4,8, 1,8,     
                4,5, 7,12, 13,2, 14,19, 
                1,2, 3,4, 1,10, 10,3,   
            });

            // few lines
            rd.bufferInfo.Add(new RenderInfo(rd.sofwareBuffer.Count, 3 * 2 * 2, ArrayData.Vertex, PrimitiveType.Lines));
            rd.sofwareBuffer.AddRange(dt[1] = new float[]
            {
                1,2,  5,6,  
                9,2,  4,7,  
                10,2,  10,2
            });

            // few more quads
            rd.bufferInfo.Add(new RenderInfo(rd.sofwareBuffer.Count, 4 * 3 * 2, ArrayData.Vertex, PrimitiveType.Quads));
            rd.sofwareBuffer.AddRange(dt[2] = new float[]
            {
                6,7, 100,405, 47,124, 123,32,
                100,200, 300,400, 102,506, 1023,9921,
                1,2, 3,4, 6,7, 7,2
            });

            // few more lines
            rd.bufferInfo.Add(new RenderInfo(rd.sofwareBuffer.Count, 3 * 2 * 2, ArrayData.Vertex, PrimitiveType.Lines));
            rd.sofwareBuffer.AddRange(dt[3] = new float[] 
            {
                8,9,  5,6,  
                10,0,  50,12,  
                102,667,  9, 201
            });

            // process buffer!
            rp.ProcessRenderBuffer(rd);

            // assertion timeees
            for (int i = 0; i < 4; i++)
            {
                var p = buf.renderlist[i];
                var pd = dt[i];
                Assert.AreEqual(p.pt, dtp[i]);
                int pdi = 0;
                for (int j = 0; j < p.data.Length; j++)
                    foreach (float fl in lvr(p.data[j]))
                        Assert.AreEqual(fl, pd[pdi++]);
            }

            // reset
            buf.ResetBuffer();
        }
        IEnumerable<float> lvr(MockGLBuffer.rend rd)
        {
            return new float[] { rd.v.x, rd.v.y };
        }
        IEnumerable<float> lvtr(MockGLBuffer.rend rd)
        {
            return new float[] { rd.v.x, rd.v.y, rd.c.a, rd.c.r, rd.c.g, rd.c.b };
        }
    }

    class MockGLBuffer : IGLBuffer
    {
        public void ResetBuffer()
        {
            buffers = new Dictionary<int, float[]> { { 0, new float[0] } };
            renderlist.Clear();
            enables.Clear();
            pointers.Clear();
        }

        Dictionary<int, float[]> buffers = new Dictionary<int, float[]> { { 0, new float[0] } };
        public int GenBuffer()
        {
            int i = 1;
            while (buffers.ContainsKey(i)) i++;
            buffers[i] = new float[0];
            return i;
        }
        public void DeleteBuffer(int buf)
        {
            buffers.Remove(buf);
        }

        int activeBuffer = 0;
        public void BufferData(OpenTK.Graphics.OpenGL.BufferTarget targ, IntPtr length, float[] data, OpenTK.Graphics.OpenGL.BufferUsageHint hint)
        {
            if (!buffers.ContainsKey(activeBuffer)) throw new ArgumentException();
            buffers[activeBuffer] = data;
        }

        public void BindBuffer(OpenTK.Graphics.OpenGL.BufferTarget targ, int buf)
        {
            if (!buffers.ContainsKey(activeBuffer)) throw new ArgumentException();
            activeBuffer = buf;
        }

        public Dictionary<PrimitiveType, int> primsizes = new Dictionary<PrimitiveType, int> 
        { 
            { PrimitiveType.Quads, 4 }, 
            { PrimitiveType.Lines, 2 } 
        };

        public class ver { public float x, y;}
        public class col { public float r, g, b, a;}
        public class tex { public float u, v;}
        public class rend
        {
            public ver v; public col c; public tex t;
            public override string ToString()
            {
                return
                    (v == null ? "" : ("v:" + v.x + "," + v.y + " ")) +
                    (c == null ? "" : ("c:" + c.a + "," + c.r + "," + c.g + "," + c.b + " ")) +
                    (t == null ? "" : ("t:" + t.u + "," + t.v + " "));
            }
        }
        public class pol { public rend[] data; public PrimitiveType pt;}
        public List<pol> renderlist = new List<pol>();
        public void DrawArrays(OpenTK.Graphics.OpenGL.PrimitiveType pt, int st, int len)
        {
            int sf = sizeof(float);
            float[] ab = buffers[activeBuffer];

            bool ev = enables.ContainsKey(ArrayCap.VertexArray);
            bool ec = enables.ContainsKey(ArrayCap.ColorArray);
            bool et = enables.ContainsKey(ArrayCap.TextureCoordArray);
            int vi = ev ? pointers[ArrayCap.VertexArray].offset / sf : 0;
            int ci = ec ? pointers[ArrayCap.ColorArray].offset / sf : 0;
            int ti = et ? pointers[ArrayCap.TextureCoordArray].offset / sf : 0;
            int vs = ev ? pointers[ArrayCap.VertexArray].stride / sf : 0;
            int cs = ec ? pointers[ArrayCap.ColorArray].stride / sf : 0;
            int ts = et ? pointers[ArrayCap.TextureCoordArray].stride / sf : 0;
            vi += vs * st; ci += cs * st; ti += ts * st;
            List<rend> data = new List<rend>();
            for (int i = 0; i < len; i++) // index only for counting loop.
            {
                ver v = null; col c = null; tex t = null;
                if (ev)
                {
                    v = new ver() { x = ab[vi], y = ab[vi + 1] };
                    vi += vs;
                }
                if (ec)
                {
                    c = new col() { a = ab[ci], r = ab[ci + 1], g = ab[ci + 2], b = ab[ci + 3] };
                    ci += cs;
                }
                if (et)
                {
                    t = new tex() { u = ab[ti], v = ab[ti + 1] };
                    ti += ts;
                }
                data.Add(new rend() { v = v, c = c, t = t });
            }
            renderlist.Add(new pol() { pt = pt, data = data.ToArray() });
        }

        Dictionary<ArrayCap, Object> enables = new Dictionary<ArrayCap, Object>();
        public void ArrayEnabled(OpenTK.Graphics.OpenGL.ArrayCap type, bool enabled)
        {
            if (!enabled) enables.Remove(type);
            else if (enables.ContainsKey(type)) throw new ArgumentException();
            else enables.Add(type, new object());
        }

        public struct vpoint { public int size, offset, stride; }
        Dictionary<ArrayCap, vpoint> pointers = new Dictionary<ArrayCap, vpoint>();
        public void SetPointer(OpenTK.Graphics.OpenGL.ArrayCap type, int nel, int stride, int offset)
        {
            pointers[type] = new vpoint() { size = nel, stride = stride, offset = offset };
        }
    }

}