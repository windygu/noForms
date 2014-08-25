﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Forms;
using NoForms.ComponentBase;
using NoForms.Renderers;
using NoForms.Common;

namespace NoForms
{
    
    /// <summary>
    /// You would create a noform, and to it add INoComponent, and it would handle stuff.
    /// </summary>
    public class NoForm : IComponent
    {
        FocusManager fm = new FocusManager();
        public FocusManager focusManager { get { return fm; } }
        public IComponent Parent { get { return null; } set { } }
        IComponent_Collection _components;
        public IComponent_Collection components
        {
            get { return _components; }
        }
        public int ZIndex { get { return 0; } }
        public event Action<IComponent> ZIndexChanged = delegate { };

        public NoForms.Common.Cursors Cursor { get; set; } // FIXME does nothing.
        public bool Scrollable { get; set; } // Begins to be irellivant FIXME?

        // Some Model Elements...
        Point _Location = new Point(0, 0);
        public Point Location
        {
            get { return _Location; }
            set
            {
                _Location = value;
                LocationChanged(_Location);
            }
        }
        Size _Size = new Size(200, 300);
        /// <summary>
        /// This mirrors the size propterty at 0,0
        /// </summary>
        private Rectangle _DisplayRectangle = new Rectangle();
        public Rectangle DisplayRectangle
        {
            get { return _DisplayRectangle; }
            set
            {
                _DisplayRectangle.Size = value.Size;
                _Size = DisplayRectangle.Size;
            }
        }
        public Size Size
        {
            get { return _Size; }
            set
            {
                lock (this)
                {
                    _Size = new Size(
                        Math.Max(Math.Min(value.width, MaxSize.width), MinSize.width),
                        Math.Max(Math.Min(value.height, MaxSize.height), MinSize.height)
                        );
                    bool isDirty = !_Size.Equals(_DisplayRectangle.Size);
                    _DisplayRectangle.Size = new Size(Size.width, Size.height);
                    SizeChanged(_Size);
                }
            }
        }
        public event Action<Size> SizeChanged = delegate { };
        public event Action<Point> LocationChanged = delegate { };
        public Size MinSize = new Size(50, 50);
        public Size MaxSize = new Size(9000, 9000);

        /// <summary>
        /// 
        /// 
        /// </summary>
        /// <param name="renderMethod">
        /// rednerMethod encapsulates the windowing and rendering system.
        /// variants use the same drawing implimnentations (although you will only see the udraw interface)
        /// so D2DLayered does the same drawing commands as D2DSwapChain, except it pushes the output of
        /// NoForm.DrawBase(and ofc children) to different places, uses different buffers and so on.
        /// </param>
        /// <param name="createOptions">
        /// encapsulates options that for whatever reason can only be set
        /// once.
        /// </param>
        public NoForm(IPlatform plat, CreateOptions createOptions)
        {
            _components = new IComponent_Collection(this);
            plat.Init(this, createOptions);
        }
        /// <summary>
        /// called on by children, to let us know drawing needs doing...
        /// </summary>
        /// <param name="rect"></param>
        public void Dirty(Rectangle rect)
        {
            //...just tell the renderer!
            renderer.Dirty(rect);
        }

        // Keyboard Hooks
        public void KeyUpDown(NoForms.Common.Keys key, NoForms.Common.ButtonState bs)
        {
            foreach (IComponent inc in components)
                inc.KeyUpDown(key, bs);
        }
        public void KeyPress(char c)
        {
            foreach (IComponent inc in components)
                inc.KeyPress(c);
        }

        // Mouse Hooks
        public void MouseUpDown(Point location, MouseButton mb, NoForms.Common.ButtonState bs, bool inComponent, bool amClipped)
        {
            foreach (IComponent c in components)
            {
                if (c.visible)
                    c.MouseUpDown(location, mb, bs,  c.DisplayRectangle.Contains(location),
                        amClipped ? true : ! DisplayRectangle.Contains(location));
            }
        }
        public void MouseMove(Point location, bool inComponent, bool amClipped)
        {
            foreach (IComponent c in components)
            {
                if (c.visible)
                {
                    bool child_inComponent =  c.DisplayRectangle.Contains(location);
                    bool child_amClipped = amClipped ? true : ! DisplayRectangle.Contains(location);
                    c.MouseMove(location, child_inComponent, child_amClipped);
                }
            }

            // FIXME should derrive from component?
            if (IComponent_Util.AmITopZOrder(this, location))
                window.Cursor = Cursor;
        }

        USolidBrush trans = new USolidBrush() { color = new Color(0, 0, 0, 0) };
        public void DrawBase(IDraw rt, Region dirty)
        {
            foreach (var rect in dirty.AsRectangles())
                rt.uDraw.FillRectangle(rect, trans); // this lets alphas to desktop happen.
            Draw(rt, dirty);

            // Now we need to draw our childrens....
            rt.uDraw.PushAxisAlignedClip(DisplayRectangle,false);
            foreach (IComponent c in components)
                if (c.visible && dirty.Intersects(c.DisplayRectangle)) 
                    c.DrawBase(rt, dirty);
            rt.uDraw.PopAxisAlignedClip();
        }
        public virtual void Draw(IDraw rt, Region dirty)
        {
        }

        public IWindow window;
        public IController controller;
        public IRender renderer;

        // FIXME some isp could avoid this and keep the hierachy intact..
        public bool visible
        {
            get { return true; }
            set { throw new NotImplementedException(); }
        }
        public void RecalculateDisplayRectangle() { throw new NotImplementedException(); }
        public void RecalculateLocation() { throw new NotImplementedException(); }
        
    }
}
