using Antmicro.Renode.Backends.Video;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Extensions.Analyzers.Video
{
    public  class DummyVideoAnalyzer : BasicPeripheralBackendAnalyzer<VideoBackend>
    {
        public override void Hide()
        {
        }

        public override void Show()
        {
        }
    }
}