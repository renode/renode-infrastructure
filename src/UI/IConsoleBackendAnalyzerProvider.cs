//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using AntShell.Terminal;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.UI
{
    public interface IConsoleBackendAnalyzerProvider : IAutoLoadType
    {
        bool TryOpen(string consoleName, out IIOSource io, bool isMonitorWindow = false);
        void Close();
        event Action OnClose;
    }

    public class ConsoleBackendAnalyzerProviderAttribute : Attribute
    {
        public ConsoleBackendAnalyzerProviderAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; private set; }

    }
}
