using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.SPI;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class ADIS16500 : ISPICSPeripheral
    {
        public GPIO IRQ { get; }
        public bool ChipSelectActive { get; set; }

        public ADIS16500()
        {
            IRQ = new GPIO();
            IRQ.Set(false);
        }
       
        public void FinishTransmission()
        {
            IRQ.Blink();
        }

        public void Reset()
        {
        }

        public byte TransmitSelected(byte data)
        {
            return data;
        }
    }
}
