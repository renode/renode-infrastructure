using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class LSM6DSV : II2CPeripheral
    {
        public byte[] Read(int count = 1)
        {
            byte[] result = new byte[count];
            for (int i = 0; i < count; i ++)
            {
                result[i] = (byte) (0xFF - i);
            }

            return result;
        }

        public void Write(byte[] data)
        {
            
        }

        public void FinishTransmission()
        {
        }

        public void Reset()
        {
        }
    }
}
