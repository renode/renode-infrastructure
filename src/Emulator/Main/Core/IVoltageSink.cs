using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Core
{
    public interface IVoltageSink : IPeripheral
    {
        void SetVoltage(double voltage);
    }
}
