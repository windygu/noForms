﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Forms;
using SharpDX.Direct2D1;

using NoForms.Controls.Templates;

// aliases
using SysRect = System.Drawing.Rectangle;

namespace NoForms
{
    /// <summary>
    /// You would create a noform, and to it add INoComponent, and it would handle stuff.
    /// </summary>
    public class NoForm : IContainer
    {
        public IContainer Parent 
        {
            get { return null; }
            set { return; }
        }

        Rectangle clipSet = Rectangle.Empty;
        public void UnClipAll<RenderType>(RenderType renderArgument)
        {
            Util.SetClip<RenderType>(renderArgument, true, Rectangle.Empty);
        }
        public void ReClipAll<RenderType>(RenderType renderArgument)
        {
            Util.SetClip<RenderType>(renderArgument, true, clipSet);
        }

        ComponentCollection _components;
        public ComponentCollection components
        {
            get { return _components; }
        }

        protected IRender renderMethod;
        internal Form theForm;

        public void SetFormCursor(Cursor cur)
        {
            theForm.Cursor = cur;
        }

        // Some Model Elements...
        Point _Location = new Point(0,0);
        public Point Location
        {
            get { return _Location; }
            set
            {
                _Location = value;
                if (LocationChanged == null) return;
                foreach (Act mi in LocationChanged.GetInvocationList())
                    mi();
            }
        }
        Size _Size = new Size(200,300);
        /// <summary>
        /// This mirrors the size propterty at 0,0
        /// </summary>
        private Rectangle _DisplayRectangle = new Rectangle();
        public Rectangle DisplayRectangle
        {
            get { return _DisplayRectangle; }
            set
            {
                _DisplayRectangle = value;
                _Size = DisplayRectangle.Size;
            }
        }
        public Size Size
        {
            get { return _Size; }
            set
            {
                _Size = new Size(
                    Math.Max(Math.Min(value.width,MaxSize.width),MinSize.width),
                    Math.Max(Math.Min(value.height, MaxSize.height), MinSize.height)
                    );

                _DisplayRectangle.Size = new Size(Size.width, Size.height);

                if (SizeChanged == null) return;
                foreach (Act mi in SizeChanged.GetInvocationList())
                    mi();
            }
        }
        public delegate void Act();
        public event Act SizeChanged;
        public event Act LocationChanged;
        public Size MinSize = new Size(50,50);
        public Size MaxSize = new Size(9000,9000);

        public System.Drawing.Icon icon = null;
        public String title = "";

        public NoForm(IRender renderMethod)
        {
            this.renderMethod = renderMethod;
            renderMethod.noForm = this;
            _components = new ComponentCollection(this);
        }
        public SharpDX.Color4 _backColor = new SharpDX.Color4(1f, 1f, 1f, 1f);
        public SharpDX.Color4 backColor
        {
            get { return _backColor; }
            set { _backColor = value; }
        }

        // Register controller events
        internal void RegisterControllers()
        {
            theForm.MouseDown += new MouseEventHandler(MouseDown);
            theForm.MouseUp += new MouseEventHandler(MouseUp);
            theForm.MouseMove +=new MouseEventHandler(MouseMove);
            theForm.KeyDown += new KeyEventHandler((Object o, KeyEventArgs e) => { KeyDown(e.KeyCode); });
            theForm.KeyUp += new KeyEventHandler((Object o, KeyEventArgs e) => { KeyUp(e.KeyCode); });
            theForm.KeyPress += new KeyPressEventHandler((Object o, KeyPressEventArgs e) => { KeyPress(e.KeyChar); });
        }

        public void KeyPress(char c)
        {
            foreach (IComponent inc in components)
                if (inc is Focusable)
                    (inc as Focusable).KeyPress(c);
        }

        
        // Key Events
        public void KeyDown(System.Windows.Forms.Keys key)
        {
            foreach (IComponent inc in components)
                if (inc is Focusable)
                    (inc as Focusable).KeyDown(key);
        }
        public void KeyUp(System.Windows.Forms.Keys key)
        {
            foreach (IComponent inc in components)
                if (inc is Focusable)
                    (inc as Focusable).KeyUp(key);
        }


        // Click Controller
        void MouseDown(Object o, MouseEventArgs mea)
        {
            MouseUpDown(mea, MouseButtonState.DOWN);
        }
        void MouseUp(Object o, MouseEventArgs mea)
        {
            MouseUpDown(mea, MouseButtonState.UP);
        }
        public void MouseUpDown(MouseEventArgs mea, MouseButtonState mbs)
        {
            MouseButton mb;
            if (mea.Button == MouseButtons.Left) mb = MouseButton.LEFT;
            else if (mea.Button == MouseButtons.Right) mb = MouseButton.RIGHT;
            else return;

            var ceventargs = new ClickedEventArgs()
            {
                button = mb,
                state = mbs,
                clickLocation = mea.Location
            };

            if (Clicked != null)
                foreach (ClickedEventHandler cevent in Clicked.GetInvocationList())
                    cevent(ceventargs);
            foreach (IComponent inc in components)
                if(inc.visible)
                    inc.MouseUpDown(mea, mbs, Util.CursorInRect(inc.DisplayRectangle, Location), !Util.CursorInRect(DisplayRectangle, Location));
        }
        public struct ClickedEventArgs
        {
            public MouseButtonState state;
            public System.Drawing.Point clickLocation;
            public MouseButton button;
        }
        public delegate void ClickedEventHandler(ClickedEventArgs cea);
        public event ClickedEventHandler Clicked;

        // move contoller
        public delegate void MouseMoveEventHandler(System.Drawing.Point location);
        public event MouseMoveEventHandler MouseMoved;
        void MouseMove(object sender, MouseEventArgs e)
        {
            MouseMove(Cursor.Position);
        }
        public void MouseMove(System.Drawing.Point location)
        {
            if (MouseMoved == null) return;

            foreach (MouseMoveEventHandler mevent in MouseMoved.GetInvocationList())
                mevent(location);
            foreach (IComponent inc in components)
                if (inc.visible)
                    inc.MouseMove(location, Util.CursorInRect(inc.DisplayRectangle, Location), !Util.CursorInRect(DisplayRectangle, Location));
        }
        

        public void DrawBase<RenderType>(RenderType renderArgument)
        {
            if (renderArgument is RenderTarget)
            {
                RenderTarget d2dtarget = renderArgument as RenderTarget;
                d2dtarget.Clear(null); // this lets alphas to desktop happen. same as clear(new color4(0,0,0,0)).
                Draw(d2dtarget);

                // Now we need to draw our childrens....
                Util.SetClip<RenderType>(renderArgument, true, DisplayRectangle);
                foreach (IComponent c in components)
                    if (c.visible)
                        c.DrawBase<RenderType>(renderArgument);
                Util.SetClip<RenderType>(renderArgument, false, Rectangle.Empty);
                // FIXME assuming this is always root. might be nice to allow embedding NoForm in a NoForm...?
            }
            else if (renderArgument is System.Drawing.Graphics)
            {
                System.Drawing.Graphics g = renderArgument as System.Drawing.Graphics;
                Draw(g);
            }
            else
            {
                throw new NotImplementedException("Unknown rendering method asked of NoForm");
            }
        }
        // Supporting Direct2D
        public virtual void Draw(RenderTarget d2dtarget)
        {
            d2dtarget.FillRectangle(DisplayRectangle,
                new SolidColorBrush(d2dtarget, backColor)); // FIXME many brushes!!
        }
        public virtual void Draw(System.Drawing.Graphics graphics)
        {
             // not done yet....
            throw new NotImplementedException("GDI rendering not supported for NoForm");
        }

        void SetWFormProps()
        {
            theForm.Text = title;
            theForm.ShowIcon = icon != null;
            if (icon != null) theForm.Icon = icon;
        }

        bool okClose = false;
        public void Create(bool rootForm, bool dialog)
        {

            renderMethod.Init(ref theForm);
            SetWFormProps();
            RegisterControllers();

            theForm.Load += new EventHandler((object o, EventArgs e) =>
            {
                renderMethod.BeginRender();
            });
            theForm.FormClosing += new FormClosingEventHandler((object o, FormClosingEventArgs e) =>
            {
                if(!okClose) e.Cancel = true;
                renderMethod.EndRender(new MethodInvoker(() => Close(true)));
            });

            if (rootForm)
            {
                Application.EnableVisualStyles();
                Application.Run(theForm);

            }
            else
            {
                if (dialog) theForm.ShowDialog();
                else theForm.Show();
            }
        }
        public void Close(bool done = false)
        {
            if (theForm.InvokeRequired)
            {
                theForm.Invoke(new MethodInvoker(() => Close(done)));
                return;
            }
            okClose = done;
            theForm.Close();
        }
    }



    

    
 
}
