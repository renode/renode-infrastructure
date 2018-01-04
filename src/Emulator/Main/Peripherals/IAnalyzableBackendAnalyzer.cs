//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals
{
    public interface IAnalyzableBackendAnalyzer<T> : IAnalyzableBackendAnalyzer where T : IAnalyzableBackend
    {
        void AttachTo(T backend);
    }

    public interface IAnalyzableBackendAnalyzer : IAutoLoadType
    {
        void Show();
        void Hide();

        IAnalyzableBackend Backend { get; }
    }
}

