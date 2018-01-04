//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Xwt;
using System.Collections.Generic;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.UI
{
    public class WindowedUserInterfaceProvider : IUserInterfaceProvider
    {
        public void ShowAnalyser(IAnalyzableBackendAnalyzer analyzer, string name)
        {
            var guiWidget = analyzer as IHasWidget;
            if(guiWidget == null)
            {
                throw new ArgumentException("Wrong analyzer provided, expected object of type 'IHasGUIWidget'");
            }
            
            var window = new Window();
            window.Title = name;
            window.Height = 600;
            window.Width = 800;
            
            window.Content = guiWidget.Widget;
            
            openedWindows.Add(analyzer, window);
            window.Closed += (sender, e) => openedWindows.Remove(analyzer);
            
            window.Show();
        }

        public void HideAnalyser(IAnalyzableBackendAnalyzer analyzer)
        {
            var guiAnalyzer = analyzer as IHasWidget;
            if(guiAnalyzer == null)
            {
                throw new ArgumentException("Wrong analyzer provided, expected object of type 'IHasGUIWidget'");
            }
            
            Window win;
            if(openedWindows.TryGetValue(analyzer, out win))
            {
                win.Close();
                openedWindows.Remove(analyzer);
            }
        }
        
        private readonly Dictionary<IAnalyzableBackendAnalyzer, Window> openedWindows = new Dictionary<IAnalyzableBackendAnalyzer, Window>();
    }
}

