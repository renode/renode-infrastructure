//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Logging;
using Xwt.Drawing;
using Xwt;

namespace Antmicro.Renode.Plugins.AdvancedLoggerViewer
{
    public class LogLevelsSelectionWidget : HBox
    {
        private event Action<LogLevel, bool> selectionChanged;
        public event Action<LogLevel, bool> SelectionChanged
        {
            add
            {
                selectionChanged += value;
                foreach(var button in buttons)
                {
                    var level = LogLevel.Parse(button.Label);
                    value(level, button.Active);
                }
            }

            remove 
            {
                selectionChanged -= value;
            }
        }

        public LogLevelsSelectionWidget()
        {
            buttons = new List<ToggleButton>();
            var buttonsDictionary = new Dictionary<LogLevel, Image>() {
                { LogLevel.Noisy,    null },
                { LogLevel.Debug,    null },
                { LogLevel.Info,     StockIcons.Information.WithSize(IconSize.Small) },
                { LogLevel.Warning,  StockIcons.Warning.WithSize(IconSize.Small) },
                { LogLevel.Error,    StockIcons.Error.WithSize(IconSize.Small) },
            };

            foreach(var button in buttonsDictionary)
            {
                var b = new ToggleButton(button.Key.ToStringCamelCase()) { WidthRequest = 85 };
                if(button.Value != null)
                {
                    b.ImagePosition = ContentPosition.Left;
                    b.Image = button.Value;
                    b.Active = true;
                }

                b.Clicked += (sender, e) =>
                {
                    var tb = sender as ToggleButton;
                    var level = LogLevel.Parse(tb.Label);

                    var sc = selectionChanged;
                    if (sc != null)
                    {
                        sc(level, tb.Active);
                    }
                };

                PackStart(b);
                buttons.Add(b);
            }
        }

        private readonly List<ToggleButton> buttons;
    }
}

