using Antmicro.Renode.Core;

namespace Antmicro.Renode.Peripherals.SPI
{
    public interface ISPICSPeripheral : ISPIPeripheral, IGPIOReceiver
    {
        bool ChipSelectActive { get; set; }

        void IGPIOReceiver.OnGPIO(int number, bool value)
        {
            ChipSelectActive = !value;
        }

        byte ISPIPeripheral.Transmit(byte data)
        {
            return ChipSelectActive ? TransmitSelected(data) : (byte)0xFF;
        }

        byte TransmitSelected(byte data);
    }
}
