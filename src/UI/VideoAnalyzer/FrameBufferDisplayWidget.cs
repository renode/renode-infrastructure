//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Migrant;
using Xwt;
using Antmicro.Renode.Backends.Display;
using Xwt.Drawing;
using Antmicro.Renode.Peripherals.Input;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using ELFSharp.ELF;
using Antmicro.Renode.Extensions.Analyzers.Video.Handlers;
using Antmicro.Renode.UI;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Extensions.Analyzers.Video
{
    [Transient]
    public class FrameBufferDisplayWidget : Canvas, IExternal, IConnectable<IKeyboard>, IConnectable<IPointerInput>
    {
        public FrameBufferDisplayWidget()
        {
            BoundsChanged += (sender, e) =>
            {
                drawMethod = CalculateDrawMethod();
                ActualImageArea = CalculateActualImageRectangle();
            };
            handler = new IOHandler(this);
            handler.GrabConfirm += ShowGrabConfirmationDialog;
            handler.PointerInputAttached += HandleNewPointerDevice;
        }

        public void SaveCurrentFrameToFile(string filename)
        {
            lock(imgLock) 
            {
                img.Save(filename, ImageFileType.Png);
            }
        }

        /// <summary>
        /// Draws the frame.
        /// </summary>
        /// <param name="frame">Frame represented as array of bytes. If this parameter is omitted previous frame is redrawn.</param>
        public void DrawFrame(byte[] frame = null)
        {
            if(!drawQueued)
            {
                lock(imgLock)
                {
                    if(img == null)
                    {
                        return;
                    }

                    if(frame != null)
                    {
                        converter.Convert(frame, ref outBuffer);
                        img.Copy(outBuffer);
                        cursorDrawn = false;
                    }

                    if(!anythingDrawnAfterLastReconfiguration && frame != null) 
                    {
                        anythingDrawnAfterLastReconfiguration = true;
                        handler.Init();
                    }

                    ApplicationExtensions.InvokeInUIThread(QueueDraw);
                    drawQueued = true;
                }
            }
        }

        public void OnDisplayParametersChanged(int width, int height, PixelFormat format)
        {
            var rc = DisplayParametersChanged;
            if(rc != null)
            {
                rc(width, height, format);
            }
        }

        public void SetDisplayParameters(int desiredWidth, int desiredHeight, PixelFormat colorFormat, Endianess endianess)
        {
            if(desiredWidth == 0 && desiredHeight == 0)
            {
                return;
            }

            DesiredDisplayWidth = desiredWidth;
            DesiredDisplayHeight = desiredHeight;

            lock(imgLock)
            {
                var pixelFormat = PixelFormat.RGBA8888;
#if PLATFORM_WINDOWS
                pixelFormat = PixelFormat.BGRA8888;
#endif
                converter = PixelManipulationTools.GetConverter(colorFormat, endianess, pixelFormat, Endianess.BigEndian);
                outBuffer = new byte[desiredWidth * desiredHeight * pixelFormat.GetColorDepth()];

                img = new ImageBuilder(DesiredDisplayWidth, DesiredDisplayHeight).ToBitmap();
                drawMethod = CalculateDrawMethod();
                ActualImageArea = CalculateActualImageRectangle();

                anythingDrawnAfterLastReconfiguration = false;
            }

            OnDisplayParametersChanged(DesiredDisplayWidth, DesiredDisplayHeight, colorFormat);
        }

        public void AttachTo(IKeyboard keyboardToAttach)
        {
            handler.Attach(keyboard: keyboardToAttach);
            var ia = InputAttached;
            if(ia != null)
            {
                ia(keyboardToAttach);
            }
        }

        public void AttachTo(IPointerInput inputToAttach)
        {
            handler.Attach(pointer: inputToAttach);
            var ia = InputAttached;
            if(ia != null)
            {
                ia(inputToAttach);
            }
        }

        public void DetachFrom(IPointerInput inputToDetach)
        {
            handler.Detach(pointer: true);
        }

        public void DetachFrom(IKeyboard keyboardToDetach)
        {
            handler.Detach(keyboard: true);
        }

        public event Action<int, int> PointerMoved;
        public Action<IPeripheral> InputAttached;
        public Action<int, int, PixelFormat> DisplayParametersChanged;
        public Action FrameDrawn;

        public int DesiredDisplayWidth { get; private set; }
        public int DesiredDisplayHeight { get; private set; }
        public Rectangle ActualImageArea { get; private set; }
        public Image Image
        {
            get
            {
                lock(imgLock)
                {
                    return img != null ? new Image(img) : null;
                }
            }
        }
        
        public DisplayMode Mode
        { 
            get { return mode; }
            set
            {
                mode = value;
                drawMethod = CalculateDrawMethod();
                ActualImageArea = CalculateActualImageRectangle();
                DrawFrame();
            }
        }

        protected override void OnDraw(Context ctx, Rectangle dirtyRect)
        {
            var dmc = drawMethod;
            if(img == null || dmc == null)
            {
                return;
            }

            IOHandler.Position current, previous;
            handler.GetPosition(out current, out previous);

            if(cursorDrawn && previous != null)
            {
                // drawing a cursor for the second time will effectively remove it
                img.DrawCursor(previous.X, previous.Y);
            }

            if(current != null)
            {
                img.DrawCursor(current.X, current.Y);
                cursorDrawn = true;
            }

            dmc(ctx);

            var fd = FrameDrawn;
            if(fd != null && drawQueued)
            {
                fd();
            }

            lock(imgLock)
            {
                drawQueued = false;
            }
        }

        private Action<Context> CalculateDrawMethod()
        {
            var bounds = Bounds;

            if(img == null)
            {
                return ctx =>
                {
                };
            }
            else if(Mode == DisplayMode.Stretch)
            {
                return ctx =>
                {
                    lock(imgLock)
                    {
                        ctx.DrawImage(img, bounds.Inflate(-1, -1));

                        // draw frame
                        ctx.Rectangle(bounds);
                        ctx.SetColor(new Color(0.643, 0.623, 0.616));
                        ctx.SetLineWidth(1);
                        ctx.Stroke();
                    }
                };
            }
            else if(Mode == DisplayMode.Fit)
            {
                Image fitImg;
                lock(imgLock)
                {
                    fitImg = img.WithBoxSize(bounds.Size);
                }
                var posx = (bounds.Size.Width - fitImg.Width) / 2;
                var posy = (bounds.Size.Height - fitImg.Height) / 2;
                var point = new Point(posx, posy);

                return ctx =>
                {
                    lock(imgLock)
                    {
                        fitImg = img.WithBoxSize(bounds.Size);
                    }

                    var rect = new Rectangle(point, fitImg.Size);
                    ctx.DrawImage(fitImg, rect.Inflate(-1, -1));

                    // draw frame
                    ctx.Rectangle(rect);
                    ctx.SetColor(new Color(0.643, 0.623, 0.616));
                    ctx.SetLineWidth(1);
                    ctx.Stroke();
                };
            }
            else
            {
                var posx = (bounds.Size.Width - img.Width) / 2;
                var posy = (bounds.Size.Height - img.Height) / 2;
                var point = new Point(posx, posy);

                return ctx =>
                {
                    lock(imgLock)
                    {
                        ctx.DrawImage(img, point); 

                        // draw frame
                        ctx.Rectangle(new Rectangle(point.X - 1, point.Y - 1, img.Width + 2, img.Height + 2));
                        ctx.SetColor(new Color(0.643, 0.623, 0.616));
                        ctx.SetLineWidth(1);
                        ctx.Stroke();
                    }
                };
            }
        }

        private bool ShowGrabConfirmationDialog()
        {
            if(dontShowGrabConfirmationDialog)
            {
                return true;
            }

            var dialog = new Dialog();
            dialog.Title = "Grabbing mouse&keyboard";

            var dialogContent = new VBox();
            CheckBox checkBox;
            dialogContent.PackStart(new Label { Markup = "Frame buffer analyser is about to grab your mouse and keyboard.\nTo ungrab it press <b>Left-Ctrl + Left-Alt + Left-Shift</b> combination." });
            dialogContent.PackStart((checkBox = new CheckBox("Don't show this message again")));
            dialog.Content = dialogContent;
            dialog.Buttons.Add(new DialogButton(Command.Ok));
            dialog.Buttons.Add(new DialogButton(Command.Cancel));

            var result = dialog.Run();
            dialog.Dispose();
            if(result == Command.Ok)
            {
                dontShowGrabConfirmationDialog = checkBox.Active;
                return true;
            }

            return false;
        }

        private void HandleNewPointerDevice(PointerHandler pointerHandler, PointerHandler previousPointerHandler)
        {
            var previousAbsolutePointerHandler = previousPointerHandler as AbsolutePointerHandler;
            if(previousAbsolutePointerHandler != null)
            {
                previousAbsolutePointerHandler.OnPointerMoved -= HandlePointerMoved;
            }

            var absolutePointerHandler = pointerHandler as AbsolutePointerHandler;
            if(absolutePointerHandler == null)
            {
                HandlePointerMoved(-1, -1);
                return;
            }

            absolutePointerHandler.OnPointerMoved += HandlePointerMoved;
        }

        private void HandlePointerMoved(int x, int y)
        {
            var pm = PointerMoved;
            if(pm != null)
            {
                pm(x, y);
            }
            DrawFrame();
        }

        /// <summary>
        /// Gets actual image rectangle relative to canvas
        /// </summary>
        /// <returns>The actual image rectangle.</returns>
        private Rectangle CalculateActualImageRectangle()
        {
            var canvasRect = ScreenBounds;
            var image = Image;
            var imgRect = new Rectangle();

            if(image == null)
            {
                return imgRect;
            } 

            switch(Mode)
            {
            case DisplayMode.Center:
                imgRect.Width = image.Width < canvasRect.Width ? image.Width : canvasRect.Width;
                imgRect.Height = image.Height < canvasRect.Height ? image.Height : canvasRect.Height;
                // if image is bigger than canvas, don't set margin
                imgRect.X = image.Width < canvasRect.Width ? (canvasRect.Width - image.Width) / 2 : 0;
                imgRect.Y = image.Height < canvasRect.Height ? (canvasRect.Height - image.Height) / 2 : 0;

                break;
            case DisplayMode.Fit:
                //check where the margin is
                var fitImg = image.WithBoxSize(canvasRect.Width, canvasRect.Height);
                imgRect.Width = fitImg.Width;
                imgRect.Height = fitImg.Height;
                //margin is on the left and right
                if(fitImg.Width < canvasRect.Width)
                {
                    imgRect.X = (canvasRect.Width - fitImg.Width) / 2;
                    imgRect.Y = 0;
                }
                else
                {
                    imgRect.X = 0;
                    imgRect.Y = (canvasRect.Height - fitImg.Height) / 2;
                }
                break;
            case DisplayMode.Stretch:
                imgRect = new Rectangle(0, 0, canvasRect.Width, canvasRect.Height);
                break;
            }
            return imgRect;
        }

        private IPixelConverter converter;
        [Transient]
        private bool dontShowGrabConfirmationDialog;
        private bool anythingDrawnAfterLastReconfiguration;
        private Action<Context> drawMethod;
        private bool drawQueued;
        private IOHandler handler;
        private BitmapImage img;
        private DisplayMode mode;
        private byte[] outBuffer;
        private bool cursorDrawn;

        private readonly object imgLock = new object();
    }
}

