﻿using System;
using System.Windows.Forms;
using SysRect = System.Drawing.Rectangle;
using Common;

namespace NoForms.ComponentBase
{
    public abstract class Component : IComponent
    {
        IComponent parent;
        public IComponent Parent
        {
            get { return parent; }
            set
            {
                parent = value;
                if(fm == null)
                    focusManager = Util.GetTopLevelComponent(parent).focusManager;
                Util.OnAllChildren(this, c =>
                {
                    var cc = c as Component; // we can maintain our own and subtypes.
                    if (cc != null && cc.fm == null) cc.focusManager = focusManager;
                });
            }
        }
        ComponentCollection _components = new AlwaysEmptyComponentCollection(null);
        public virtual ComponentCollection components
        {
            get
            {
                return _components;
            }
        }
        private int _zindex = 0;
        public int ZIndex { get { return _zindex; } set { _zindex = value; ZIndexChanged(this); } }
        public event Action<IComponent> ZIndexChanged = delegate { };

        protected Rectangle _DisplayRectangle = Rectangle.Empty;
        /// <summary>
        /// Display rectangle is relative to the RenderType, i.e. the top level component
        /// In contrast to Size and Location (which are modified when this is changed)
        /// </summary>
        public Rectangle DisplayRectangle
        {
            get { return _DisplayRectangle; }
            set
            {
                _DisplayRectangle = value;
                RecalculateLocation();
                Size = value.Size;
            }
        }
        public event Action<Size> SizeChanged;
        protected virtual void OnSizeChanged()
        {
            if (SizeChanged != null)
                foreach (Action<Size> se in SizeChanged.GetInvocationList())
                    se(_DisplayRectangle.Size);
        }
        /// <summary>
        /// Size Of Control. Modifies DisplayRectangle Size.
        /// </summary>
        public Size Size
        {
            get { return _DisplayRectangle.Size; }
            set
            {
                _DisplayRectangle.Size = value;
                OnSizeChanged();
            }
        }
        public event Action<Point> LocationChanged;
        protected virtual void OnLocationChanged()
        {
            if (LocationChanged != null)
                foreach (Action<Point> pe in LocationChanged.GetInvocationList())
                    pe(_Location);
        }
        protected Point _Location = Point.Zero;
        /// <summary>
        /// Location of control relative to parent control.
        /// This also modifies DisplayRectangle.
        /// </summary>
        public Point Location
        {
            get
            {
                return _Location;
            }
            set
            {
                _Location = value;
                OnLocationChanged();
                RecalculateDisplayRectangle();
            }
        }

        public void RecalculateDisplayRectangle()
        {
            Point ploc = new Point(0, 0);
            if (Parent != null && !(Parent is NoForm))
                ploc = Parent.DisplayRectangle.Location;

            this._DisplayRectangle.Location = new Point(
                    ploc.X + _Location.X,
                    ploc.Y + _Location.Y
                    );
            foreach (IComponent c in components)
                c.RecalculateDisplayRectangle();
        }
        public void RecalculateLocation()
        {
            Point ploc = new Point(0, 0);
            if (Parent != null && !(Parent is NoForm))
                ploc = Parent.DisplayRectangle.Location;

            // Location is the difference between displayrectangle positions
            _Location = new Point((int)DisplayRectangle.left - ploc.X, (int)DisplayRectangle.top - ploc.Y);

            foreach (IComponent c in components)
                c.RecalculateLocation();
        }

        public abstract void DrawBase(IDraw renderArgument);

        bool _visible = true;
        public bool visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        Common.Cursors _Cursor = Common.Cursors.Default;
        public Common.Cursors Cursor { get { return _Cursor; } set { _Cursor = value; } }
        public bool _Scrollable = true;
        public bool Scrollable { get { return _Scrollable; } set { _Scrollable = value; } }

        public virtual void MouseMove(Point location, bool inComponent, bool amClipped) {
            if (inComponent && !amClipped)
            {
                var tzo = Util.AmITopZOrder(this, location);
                if (tzo)
                {
                    var tlc = Util.GetTopLevelComponent(this);
                    if (tlc is NoForm)
                    {
                        var nf = (tlc as NoForm);
                        if (nf.window.Cursor != Cursor)
                            nf.window.Cursor = Cursor;
                    }
                }
            }
            
        }
        public virtual void MouseUpDown(Point location, MouseButton mb, Common.ButtonState mbs, bool inComponent, bool amClipped) { }
        public virtual void KeyUpDown(Common.Keys key, Common.ButtonState bs) { }
        public virtual void KeyPress(char c) { }

        FocusManager fm;
        public FocusManager focusManager
        {
            get { return fm ?? FocusManager.Empty; }
            set { fm = value; }
        }
    }
}