//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.UserInterface
{
    public interface IUserInterfaceProvider
    {
        void ShowAnalyser(IAnalyzableBackendAnalyzer analyzer, string name);
        void HideAnalyser(IAnalyzableBackendAnalyzer analyzer);
    }
}

