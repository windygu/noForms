﻿using System;
using System.Collections.Generic;
using System.Text;
using NoForms.Renderers;

namespace NoForms.Controls.Abstract
{
    public abstract class ScrollContainer : BasicContainer
    {
        public ScrollContainer() : base()
        {
            VerticalScrollbarWidth = HorizontalScrollbarHeight = 10;
            upButton = new ScrollBarButton(Direction.u, this);
            downButton = new ScrollBarButton(Direction.d, this);
            leftButton = new ScrollBarButton(Direction.l, this);
            rightButton = new ScrollBarButton(Direction.r, this);
            verticalTracker = new ScrollBarTracker(Orientation.v, this);
            horizontalTracker = new ScrollBarTracker(Orientation.h, this);
            GenerateScrollbarElements();

            components.ComponentAdded += new Action<IComponent>(c =>
            {
                c.SizeChanged += (ChildSizeChange);
                c.LocationChanged += (ChildLocationChange);
            });
            components.ComponentRemoved += new Action<IComponent>(c =>
            {
                c.SizeChanged -= (ChildSizeChange);
                c.LocationChanged -= (ChildLocationChange);
            });
        }

        void ChildSizeChange(Size csz)
        {
            LayoutScrollbarElements();
        }
        void ChildLocationChange(Point cpt)
        {
            LayoutScrollbarElements();
        }

        float _xOffset = 0, _yOffset = 0;
        float xOffset
        {
            get { return _xOffset; }
            set 
            {
                var cw = ContentWidth;
                if (value < 0) _xOffset = 0;
                else if (value > cw - trimWidth) _xOffset = cw - trimWidth;
                else _xOffset = value;
            }
        }
        float yOffset
        {
            get { return _yOffset; }
            set
            {
                var ch = ContentHeight;
                if (value < 0) _yOffset = 0;
                else if (value > ch - trimHeight) _yOffset = ch - trimHeight;
                else _yOffset = value;
            }
        }
        protected float HorizontalScrollbarHeight, VerticalScrollbarWidth;
        public bool HorizontalScrollbarVisible { get; private set; }
        public bool VerticalScrollbarVisible { get; private set; }
        public float ContentWidth
        {
            get
            {
                float maxRight = 0,mr;
                foreach (var c in components)
                    if ((mr = c.Size.width + c.Location.X) > maxRight && c.Scrollable)
                        maxRight = mr;
                return maxRight;
            }
        }
        public float ContentHeight
        {
            get
            {
                float maxBottom = 0, mb;
                foreach (var c in components)
                    if ((mb = c.Size.height + c.Location.Y) > maxBottom && c.Scrollable)
                        maxBottom = mb;
                return maxBottom;
            }
        }

        float trimHeight = 0, trimWidth = 0;
        Size DetermineScrollBars() 
        {
            float cw = ContentWidth;
            float ch = ContentHeight;

            // FIXME this is stupid code
            HorizontalScrollbarVisible = cw > Size.width;
            trimHeight = (Size.height - (HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0));
            VerticalScrollbarVisible = verticalContainer.visible = ch > trimHeight;
            trimWidth = (Size.width - (VerticalScrollbarVisible ? VerticalScrollbarWidth : 0));
            HorizontalScrollbarVisible = horizontalContainer.visible = cw > trimWidth;
            trimHeight = (Size.height - (HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0));
            VerticalScrollbarVisible = verticalContainer.visible = ch > trimHeight;
            trimWidth = (Size.width - (VerticalScrollbarVisible ? VerticalScrollbarWidth : 0));

            if (cw - trimWidth < xOffset) xOffset = cw - trimWidth;
            if (ch - trimHeight < yOffset) yOffset = ch - trimHeight;

            hFrac = xOffset/(cw - DisplayRectangle.width + (VerticalScrollbarVisible ? VerticalScrollbarWidth : 0));
            vFrac = yOffset/(ch - DisplayRectangle.height + (HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0));

            hVis = (DisplayRectangle.width - (VerticalScrollbarVisible ? VerticalScrollbarWidth : 0)) / cw;
            vVis = (DisplayRectangle.height - (HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0)) / ch;

            return new Size(trimWidth, trimHeight);
        }
        protected float hFrac, vFrac;
        protected float hVis, vVis;

        uint _cycle = 20;
        public uint cycle
        {
            get
            {
                return _cycle;
            }
            set
            {
                _cycle = upButton.cycle = downButton.cycle = leftButton.cycle = rightButton.cycle = value;
            }
        }
        uint _step = 20;
        public uint step
        {
            get
            {
                return _step;
            }
            set
            {
                _step = upButton.step = downButton.step = leftButton.step = rightButton.step = value;
            }
        }

        ScrollBarContainer verticalContainer = new ScrollBarContainer();
        ScrollBarButton upButton;
        ScrollBarButton downButton;
        ScrollBarTracker verticalTracker;

        ScrollBarContainer horizontalContainer = new ScrollBarContainer();
        ScrollBarButton leftButton;
        ScrollBarButton rightButton;
        ScrollBarTracker horizontalTracker;
        void GenerateScrollbarElements()
        {
            // vertical scrolly
            components.Add(verticalContainer);
            verticalContainer.components.Add(upButton);
            verticalContainer.components.Add(downButton);
            verticalContainer.components.Add(verticalTracker);

            // horizontal scrolly
            components.Add(horizontalContainer);
            horizontalContainer.components.Add(leftButton);
            horizontalContainer.components.Add(rightButton);
            horizontalContainer.components.Add(horizontalTracker);

            horizontalContainer.Scrollable = verticalContainer.Scrollable = false;

        }

        protected override void OnSizeChanged()
        {
            LayoutScrollbarElements();
            base.OnSizeChanged();
        }

        void LayoutScrollbarElements()
        {
            DetermineScrollBars();

            verticalContainer.Location = new Point(Size.width - VerticalScrollbarWidth, 0);
            verticalContainer.Size = new Size(VerticalScrollbarWidth, Size.height);
            upButton.Size = new Size(VerticalScrollbarWidth - 1, VerticalScrollbarWidth - 1);
            upButton.Location = new Point(.5f, .5f);
            downButton.Size = new Size(VerticalScrollbarWidth - 1, VerticalScrollbarWidth - 1);
            downButton.Location = new Point(.5f, verticalContainer.Size.height - (VerticalScrollbarWidth - 0.5f));
            float remH = verticalContainer.Size.height - (upButton.Size.height) * 2f;
            float tOfs = upButton.Size.height;
            verticalTracker.Size = new Size(VerticalScrollbarWidth, remH * vVis);
            verticalTracker.Location = new Point(0, tOfs + (verticalContainer.Size.height - tOfs*2 - verticalTracker.Size.height)*vFrac);
            // b1 + (tot - b1 -b2 - trac)*frac = pos

            horizontalContainer.Location = new Point(0, Size.height - HorizontalScrollbarHeight);
            horizontalContainer.Size = new Size(Size.width, HorizontalScrollbarHeight);
            leftButton.Location = new Point(.5f, .5f);
            leftButton.Size = new Size(VerticalScrollbarWidth - 1, VerticalScrollbarWidth - 1);
            rightButton.Size = new Size(VerticalScrollbarWidth - 1, VerticalScrollbarWidth - 1);
            rightButton.Location = new Point(horizontalContainer.Size.width - (HorizontalScrollbarHeight-.5f),.5f);
            float lOfs = leftButton.Size.width;
            float remW = horizontalContainer.Size.width - (leftButton.Size.width) * 2f;
            horizontalTracker.Size = new Size(remW * hVis,HorizontalScrollbarHeight);
            horizontalTracker.Location = new Point(lOfs + (horizontalContainer.Size.width-lOfs*2-horizontalTracker.Size.width)*hFrac, 0);
        }

        class ScrollBarTracker : Abstract.Component
        {
            ScrollContainer controlled;
            Orientation orientation;
            public ScrollBarTracker(Orientation orientation, ScrollContainer controlled)
            {
                this.controlled = controlled;
                this.orientation = orientation;
            }
            UBrush trackBrush = new USolidBrush() { color = new Color(.6f) };
            public override void DrawBase(IRenderType renderArgument)
            {
                renderArgument.uDraw.FillRoundedRectangle(DisplayRectangle.Deflated(new Thickness(2f)), 5, 3, trackBrush);
            }
            bool downed = false;
            float downedOrigin, topOriginal;
            public override void MouseUpDown(System.Windows.Forms.MouseEventArgs mea, MouseButtonState mbs, bool inComponent, bool amClipped)
            {
                base.MouseUpDown(mea, mbs, inComponent, amClipped);
                if (inComponent && !amClipped && Util.AmITopZOrder(this, mea.Location) && mbs == MouseButtonState.DOWN)
                {
                    downed = true;
                    var sloc = mea.Location + Util.GetTopLevelLocation(this);
                    downedOrigin = orientation == Orientation.v ? sloc.Y : sloc.X;
                    topOriginal = orientation == Orientation.v ? Location.Y : Location.X;
                    (trackBrush as USolidBrush).color = new Color(0.8f);
                }
                else
                {
                    downed = false;
                    (trackBrush as USolidBrush).color = new Color(0.6f);
                }
            }
            public override void MouseMove(System.Drawing.Point location, bool inComponent, bool amClipped)
            {
                base.MouseMove(location, inComponent, amClipped);
                if (downed)
                {
                    // in order to move this trackbar, we need to change the offsetY of the controlled ScrollContainer appropriately
                    float nowPos = orientation == Orientation.v ? location.Y : location.X;
                    float want = topOriginal + (nowPos - downedOrigin);
                    // |b1|-----| tack |-------|b2|
                    // so we need to invert: top = b + frac*(total-2*b-tack)
                    // that is (top-b1)/(total-b1-b2-tack) = frac
                    float b1 = orientation == Orientation.v ? controlled.upButton.Size.height : controlled.leftButton.Size.width;
                    float b2 = orientation == Orientation.v ? controlled.downButton.Size.height : controlled.rightButton.Size.width;
                    float tot = orientation == Orientation.v ? controlled.verticalContainer.Size.height : controlled.horizontalContainer.Size.width;
                    float tack = orientation == Orientation.v ? Size.height : Size.width;
                    float fracWant = (want - b1) / (tot - b1 - b2 - tack);
                    if (orientation == Orientation.v)
                        controlled.yOffset = fracWant * (controlled.ContentHeight - controlled.trimHeight);
                    if (orientation == Orientation.h)
                        controlled.xOffset = fracWant * (controlled.ContentWidth - controlled.trimWidth);
                    controlled.OnSizeChanged();
                }
            }
        }

        enum Orientation {none,  v, h };
        enum Direction { none, u, d, l, r };
        class ScrollBarButton : Abstract.Component
        {
            Direction type;
            ScrollContainer controlled;
            public ScrollBarButton(Direction dir, ScrollContainer parent)
            {
                type = dir;
                this.controlled = parent;
                scrollTimer =  new System.Threading.Timer(new System.Threading.TimerCallback(TimeCB), null, System.Threading.Timeout.Infinite, 200);
            }
            UBrush butBrsh = new USolidBrush() { color = new Color(.8f, .4f, .6f, .4f) };
            UBrush butArrF = new USolidBrush() { color = new Color(0) };
            bool downed = false;
            public override void MouseUpDown(System.Windows.Forms.MouseEventArgs mea, MouseButtonState mbs, bool inComponent, bool amClipped)
            {
                base.MouseUpDown(mea, mbs, inComponent, amClipped);
                bool tzo = Util.AmITopZOrder(this, mea.Location);
                if (!downed && (!inComponent || amClipped || !tzo) ) return;
                if (mbs == MouseButtonState.DOWN)
                {
                    scrollTimer.Change(0, cycle);
                    (butBrsh as USolidBrush).color = new Color(.6f, .5f, .7f, .5f);
                    downed = true;
                }
                else if(downed)
                {
                    scrollTimer.Change(System.Threading.Timeout.Infinite, cycle);
                    (butBrsh as USolidBrush).color = new Color(.8f, .4f, .6f, .4f);
                    downed = false;
                }
            }
            public uint step = 1;
            public uint cycle = 20;
            System.Threading.Timer scrollTimer;
            void TimeCB(Object o)
            {
                switch (type)
                {
                    case Direction.u: controlled.yOffset -= step; break;
                    case Direction.d: controlled.yOffset += step; break;
                    case Direction.l: controlled.xOffset -= step; break;
                    case Direction.r: controlled.xOffset += step; break;
                }
                controlled.OnSizeChanged();
            }
            public override void DrawBase(IRenderType renderArgument)
            {
                Rectangle dr = DisplayRectangle;
                float gx = dr.width/4;
                float gy = dr.height/4;
                Point a1, a2, a3;
                switch (type)
                {
                    case Direction.u:
                        a1 = new Point(dr.left + gx, dr.top + 3 * gy);
                        a2 = new Point(dr.left + 3 * gx, dr.top + 3 * gy);
                        a3 = new Point(dr.left + 2 * gx, dr.top + gy);
                        break;
                    case Direction.d:
                        a1 = new Point(dr.left + gx, dr.top +  gy);
                        a2 = new Point(dr.left + 3 * gx, dr.top +  gy);
                        a3 = new Point(dr.left + 2 * gx, dr.top + 3*gy);
                        break;
                    case Direction.l:
                        a1 = new Point(dr.left + gx, dr.top + 3 * gy);
                        a2 = new Point(dr.left + 3 * gx, dr.top + 3 * gy);
                        a3 = new Point(dr.left + 2 * gx, dr.top + gy);
                        break;
                    case Direction.r:
                        a1 = new Point(dr.left + gx, dr.top + 3 * gy);
                        a2 = new Point(dr.left + 3 * gx, dr.top + 3 * gy);
                        a3 = new Point(dr.left + 2 * gx, dr.top + gy);
                        break;
                    default: throw new NotImplementedException("what direction?");
                }
                renderArgument.uDraw.FillRoundedRectangle(DisplayRectangle, 3, 3, butBrsh);
                UPath arr = new UPath();
                UFigure fig = new UFigure(a1,true,false);
                fig.geoElements.Add(new ULine(a2));
                fig.geoElements.Add(new ULine(a3));
                fig.geoElements.Add(new ULine(a1));
                arr.figures.Add(fig);
                renderArgument.uDraw.FillPath(arr, butArrF);
            }
        }

        class ScrollBarContainer : Abstract.BasicContainer
        {
            public UBrush background = new USolidBrush() { color = new Color(.7f) };
            public override void MouseUpDown(System.Windows.Forms.MouseEventArgs mea, MouseButtonState mbs, bool inComponent, bool amClipped)
            {

                base.MouseUpDown(mea, mbs, inComponent, amClipped);
                bool topz = Util.AmITopZOrder(this, mea.Location);
                if (inComponent && !amClipped && topz && mbs == MouseButtonState.DOWN)
                    (background as USolidBrush).color = new Color(.8f);
                else (background as USolidBrush).color = new Color(.7f);
            }
            public override void Draw(IRenderType renderArgument)
            {
                renderArgument.uDraw.FillRectangle(DisplayRectangle, background);
            }
        }

        public sealed override void DrawBase(IRenderType renderArgument)
        {
            // trimSize is the size sans scrollbars.  but maybe we should just let the thing overdraw...
            Draw(renderArgument);
            if (doClip) renderArgument.uDraw.PushAxisAlignedClip(clipSet = DisplayRectangle,false);
            foreach (IComponent c in components)
                if (c.visible && !c.Scrollable) c.DrawBase(renderArgument);            
            renderArgument.uDraw.SetRenderOffset(new Point(-xOffset, -yOffset));
            foreach (IComponent c in components)
                if (c.visible && c.Scrollable) c.DrawBase(renderArgument);
            renderArgument.uDraw.SetRenderOffset(new Point(0, 0));
            if (doClip) renderArgument.uDraw.PopAxisAlignedClip();
        }
        public Thickness InnerPadding = new Thickness(0, 0, 0, 0);
        
        public override void MouseMove(System.Drawing.Point location, bool inComponent, bool amClipped)
        {
            Rectangle clipDr = DisplayRectangle;
            clipDr.height -= HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0;
            clipDr.width -= VerticalScrollbarVisible ? VerticalScrollbarWidth : 0;

            // We trick the children by modifying these positions using the x and y offsets
            var scrollOffset = new Point(xOffset,yOffset);
            var tll = Util.GetTopLevelLocation(this);

            foreach (IComponent c in components)
            {
                if (c.visible && c.Scrollable)
                    c.MouseMove(location + scrollOffset, 
                        Util.CursorInRect(c.DisplayRectangle,tll - scrollOffset),
                        amClipped ? true : !Util.CursorInRect(clipDr, tll));
                else if(!c.Scrollable) 
                    c.MouseMove(location,
                        Util.CursorInRect(c.DisplayRectangle, tll),
                        amClipped ? true : !Util.CursorInRect(DisplayRectangle, tll));
            }
        }
        public override void MouseUpDown(System.Windows.Forms.MouseEventArgs mea, MouseButtonState mbs, bool inComponent, bool amClipped)
        {
            Rectangle clipDr = DisplayRectangle;
            clipDr.height -= HorizontalScrollbarVisible ? HorizontalScrollbarHeight : 0;
            clipDr.width -= VerticalScrollbarVisible ? VerticalScrollbarWidth : 0;

            // trickery
            var scrollOffset = new Point(xOffset, yOffset);
            var tll = Util.GetTopLevelLocation(this);
            var mea2 = new System.Windows.Forms.MouseEventArgs(mea.Button, mea.Clicks, mea.X + (int)xOffset, mea.Y + (int)yOffset, mea.Delta);

            foreach (IComponent c in components)
            {
                if (c.visible && c.Scrollable)
                    c.MouseUpDown(mea2, mbs, Util.CursorInRect(c.DisplayRectangle, tll - scrollOffset),
                        amClipped ? true : !Util.CursorInRect(clipDr, tll));
                else if(!c.Scrollable)
                    c.MouseUpDown(mea, mbs, Util.CursorInRect(c.DisplayRectangle, tll),
                        amClipped ? true : !Util.CursorInRect(DisplayRectangle, tll));
            }
        }
    }
}