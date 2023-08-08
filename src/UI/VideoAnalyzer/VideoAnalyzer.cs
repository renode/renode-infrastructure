//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Backends.Video;
using Antmicro.Renode.Peripherals.Video;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Input;
using Xwt;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using System.IO;
using Xwt.Drawing;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using System;
using Antmicro.Renode.UI;

namespace Antmicro.Renode.Extensions.Analyzers.Video
{
    [Transient]
    public class VideoAnalyzer : GUIPeripheralBackendAnalyzer<VideoBackend>, IExternal, IConnectable<IPointerInput>, IConnectable<IKeyboard>
    {
        public override Widget Widget { get { return analyserWidget; } }

        public void AttachTo(IKeyboard keyboardToAttach)
        {
            displayWidget.AttachTo(keyboardToAttach);
        }

        public void AttachTo(IPointerInput inputToAttach)
        {
            displayWidget.AttachTo(inputToAttach);
        }

        public void DetachFrom(IPointerInput inputToDetach)
        {
            displayWidget.DetachFrom(inputToDetach);
        }

        public void DetachFrom(IKeyboard keyboardToDetach)
        {
            displayWidget.DetachFrom(keyboardToDetach);
        }

        protected override void OnAttach(VideoBackend backend)
        {
            var videoPeripheral = (AutoRepaintingVideo)backend.Video;
            element = videoPeripheral;
            lastRewrite = CustomDateTime.Now;
            EnsureAnalyserWidget();

            videoPeripheral.ConfigurationChanged += (w, h, f, e) => ApplicationExtensions.InvokeInUIThread(() => displayWidget.SetDisplayParameters(w, h, f, e));
            videoPeripheral.FrameRendered += (f) => 
            {
                ApplicationExtensions.InvokeInUIThread(() => 
                {
                    displayWidget.DrawFrame(f);
                    snapshotButton.Sensitive = true; 
                });
            };

            displayWidget.InputAttached += i =>
            {
                if (i is IKeyboard)
                {
                    keyboardsComboBox.SelectedItem = i;
                }
                else if (i is IPointerInput)
                {
                    pointersComboBox.SelectedItem = i;
                }
            };

            if(backend.Frame != null)
            {
                // this must be called after setting `ConfigurationChanged` event;
                // otherwise the frame set here would be overrwritten by a new, empty, instance
                ApplicationExtensions.InvokeInUIThreadAndWait(() => 
                {
                    displayWidget.SetDisplayParameters(backend.Width, backend.Height, backend.Format, backend.Endianess);
                    displayWidget.DrawFrame(backend.Frame);
                });
            }
        }

        private void EnsureAnalyserWidget()
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;

            if(analyserWidget == null)
            {
                // create display widget and attach it to the emulation
                displayWidget = new FrameBufferDisplayWidget();

                var keyboards = FindKeyboards();
                var pointers = FindPointers();

                // create other widgets
                var displayModeComboBox = new ComboBox();
                displayModeComboBox.Items.Add(DisplayMode.Stretch);
                displayModeComboBox.Items.Add(DisplayMode.Fit);
                displayModeComboBox.Items.Add(DisplayMode.Center);

                displayModeComboBox.SelectionChanged += (sender, e) => displayWidget.Mode = (DisplayMode)displayModeComboBox.SelectedItem;
                ApplicationExtensions.InvokeInUIThread(() => 
                {
                    displayModeComboBox.SelectedIndex = 1;
                });

                keyboardsComboBox = new ComboBox();
                if(keyboards != null)
                {
                    foreach(var kbd in keyboards)
                    {
                        string name;
                        emulation.TryGetEmulationElementName(kbd, out name);
                        keyboardsComboBox.Items.Add(kbd, name);
                    }
                    keyboardsComboBox.SelectionChanged += (sender, e) =>
                        emulation.Connector.Connect((IKeyboard)keyboardsComboBox.SelectedItem, displayWidget);
                }
                keyboardsComboBox.SelectedIndex = 0;

                pointersComboBox = new ComboBox();
                if(pointers != null)
                {
                    foreach(var ptr in pointers)
                    {
                        string name;
                        emulation.TryGetEmulationElementName(ptr, out name);
                        pointersComboBox.Items.Add(ptr, name);
                    }
                    pointersComboBox.SelectionChanged += (sender, e) =>
                        emulation.Connector.Connect((IPointerInput)pointersComboBox.SelectedItem, displayWidget);
                }
                pointersComboBox.SelectedIndex = 0;

                snapshotButton = new Button("Take screenshot!") { Sensitive = false };
                snapshotButton.Clicked += (sender, e) =>
                {
                    var screenshotDir = Path.Combine(Emulator.UserDirectoryPath, "screenshots");
                    Directory.CreateDirectory(screenshotDir);
                    var filename = Path.Combine(screenshotDir, string.Format("screenshot-{0:yyyy_M_d_HHmmss}.png", CustomDateTime.Now));
                    displayWidget.SaveCurrentFrameToFile(filename);
                    MessageDialog.ShowMessage("Screenshot saved in {0}".FormatWith(filename));
                };

                var configurationPanel = new HBox();
                configurationPanel.PackStart(new Label("Display mode:"));
                configurationPanel.PackStart(displayModeComboBox);
                configurationPanel.PackStart(new Label(), true);
                configurationPanel.PackStart(new Label("Keyboard:"));
                configurationPanel.PackStart(keyboardsComboBox);
                configurationPanel.PackStart(new Label("Pointer:"));
                configurationPanel.PackStart(pointersComboBox);
                configurationPanel.PackStart(new Label(), true);
                configurationPanel.PackStart(snapshotButton);

                var svc = new VBox();
                svc.PackStart(configurationPanel);
                svc.PackStart(new Label());
                var sv = new ScrollView();
                sv.Content = svc;
                sv.HeightRequest = 50;
                sv.BorderVisible = false;
                sv.VerticalScrollPolicy = ScrollPolicy.Never;

                var summaryVB = new HBox();
                var resolutionL = new Label("unknown");
                displayWidget.DisplayParametersChanged += (w, h, f) => ApplicationExtensions.InvokeInUIThread(() => resolutionL.Text = string.Format("{0} x {1} ({2})", w, h, f));
                summaryVB.PackStart(new Label("Resolution: "));
                summaryVB.PackStart(resolutionL);
                summaryVB.PackStart(new Label(), true);
                var cursorPositionL = new Label("unknown");
                displayWidget.PointerMoved += (x, y) => ApplicationExtensions.InvokeInUIThread(() => cursorPositionL.Text = (x == -1 && y == -1) ? "unknown" : string.Format("{0} x {1}", x, y));
                summaryVB.PackStart(new Label("Cursor position: "));
                summaryVB.PackStart(cursorPositionL);
                summaryVB.PackStart(new Label(), true);
                summaryVB.PackStart(new Label("Framerate: "));
                framerateL = new Label("unknown");
                displayWidget.FrameDrawn += RefreshFramerate;
                summaryVB.PackStart(framerateL);

                var vbox = new VBox();
                vbox.PackStart(sv);
                vbox.PackStart(displayWidget, true, true);
                vbox.PackStart(summaryVB);
                analyserWidget = vbox;
            }
        }

        private IEnumerable<IKeyboard> FindKeyboards()
        {
            return EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(element, out var machine) ? machine.GetPeripheralsOfType<IKeyboard>() : null;
        }

        private IEnumerable<IPointerInput> FindPointers()
        {
            return EmulationManager.Instance.CurrentEmulation.TryGetMachineForPeripheral(element, out var machine) ? machine.GetPeripheralsOfType<IPointerInput>() : null;
        }

        private void RefreshFramerate()
        {
            var now = CustomDateTime.Now;
            if(prev == null)
            {
                prev = now;
                return;
            }

            if((now - lastRewrite).TotalSeconds > 1)
            {
                var framerate = (int)(1 / (now - prev).Value.TotalSeconds);
                var deviation = (lastOffendingFramerate > framerate ? (float)lastOffendingFramerate / framerate : (float)framerate / lastOffendingFramerate) - 1.0;
                if(framerate >= HighFramerateThreshold && deviation > OffendingFramerateDeviation)
                {
                    lastOffendingFramerate = framerate;
                    this.Log(LogLevel.Info, "The framebuffer fps is very high and can cause high CPU usage. Consider decreasing FramesPerVirtualSecond in video peripheral.");
                }
                if(framerate <= LowFramerateThreshold && Math.Abs(lastOffendingFramerate - framerate) > MinimalFramerateDelta && deviation > OffendingFramerateDeviation)
                {
                    lastOffendingFramerate = framerate;
                    this.Log(LogLevel.Info, "The framebuffer fps is very low and can cause video playback to be choppy. Consider increasing FramesPerVirtualSecond in video peripheral.");
                }

                framerateL.Text = string.Format("{0} fps", framerate);
                lastRewrite = now;
            }
            prev = now;
        }

        private const int HighFramerateThreshold = 100;
        private const int LowFramerateThreshold = 10;
        private const int MinimalFramerateDelta = 5;
        private const float OffendingFramerateDeviation = 0.4F;

        private FrameBufferDisplayWidget displayWidget;
        private Widget analyserWidget;
        private Button snapshotButton;
        private ComboBox keyboardsComboBox;
        private ComboBox pointersComboBox;
        private IPeripheral element;
        private Label framerateL;
        private DateTime? prev;
        private DateTime lastRewrite;
        private int lastOffendingFramerate;
    }
}

