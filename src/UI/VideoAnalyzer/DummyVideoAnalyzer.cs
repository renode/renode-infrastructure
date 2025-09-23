//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Backends.Video;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Extensions.Analyzers.Video
{
    public class DummyVideoAnalyzer : BasicPeripheralBackendAnalyzer<VideoBackend>
    {
        public override void Hide()
        {
        }

        public override void Show()
        {
        }
    }
}