using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class TMP75C : II2CPeripheral, ITemperatureSensor, IProvidesRegisterCollection<WordRegisterCollection>, IGPIOSender
    {
        public TMP75C()
        {
            RegistersCollection = new WordRegisterCollection(this);
            Registers.Temperature.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 12, mode: FieldMode.Read, name: "Temprature", valueProviderCallback: (_) =>
                {
                    if(shutdownControl.Value)
                    {
                        return 0;
                    }
                    if(oneShotMode.Value)
                    {
                        return (ushort)((double)OneShotReading / Resolution);
                    }
                    return (ushort)((double)Temperature / Resolution);
                });
            Registers.Configuration.Define(this)
                .WithReservedBits(0, 8)
                .WithFlag(8, out shutdownControl, name :"Shutdown Control")
                .WithTaggedFlag("Alert Theromstat mode", 9)
                .WithTaggedFlag("Alert polarity control", 10)
                .WithTag("Fault queue", 11, 2)
                .WithFlag(13, out oneShotMode, name: "One shot control")
                .WithReservedBits(14, 2);
            Registers.OneShot.Define(this)
                .WithValueField(0, 16, mode: FieldMode.Write, name: "Oneshot register", writeCallback: (_, _) =>
                {
                    if(!shutdownControl.Value && oneShotMode.Value)
                    {
                        OneShotReading = Temperature;
                    }
                });

            Alert = new GPIO();
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            OneShotReading = 0;
            pointerRegisterValue = null;
            Temperature = 0;
            Alert.Set(false);
        }

        public void FinishTransmission()
        {
            pointerRegisterValue = null;
        }

        public byte[] Read(int count = 1)
        {
            var result = new byte[count];
            this.DebugLog("Reading from register {0}", (byte)pointerRegisterValue);
            if(RegistersCollection.TryRead((byte)pointerRegisterValue, out var data))
            {
                result[0] = (byte)(data >> 8);
                if(count > 1)
                {
                    result[1] = (byte)(data & 0xFF);
                }
                if(count > 2)
                {
                    this.ErrorLog("Tried to read more than 2 bytes from a 16-bit register");
                }
            }
            else
            {
                this.ErrorLog("Pointer value of {0} is not a valid offset", pointerRegisterValue);
            }
            return result;
        }

        public void Write(byte[] data)
        {
            if(data.Length < 1)
            {
                this.WarningLog("Write with length {0} unexpected. Should be at least 1", data.Length);
            }
            pointerRegisterValue = (Registers)data[0];
            this.DebugLog("Setting pointer register to {0}", data[0]);
            if(data.Length == 2)
            {
                this.ErrorLog("Registers are 16-bits, only 8 written");
                return;
            }
            if(data.Length > 2)
            {
                var encodedData = (ushort)(((ushort)data[1] << 8) | ((ushort)data[2] & 0xFF));
                this.DebugLog("Writing 0x{0:X} to register {1}", encodedData, pointerRegisterValue);
                RegistersCollection.Write((byte)pointerRegisterValue, encodedData);
            }
        }

        public decimal Temperature { get; set; }

        public GPIO Alert { get; private set; }

        public WordRegisterCollection RegistersCollection { get; private set; }

        private decimal OneShotReading { get; set; }

        private Registers? pointerRegisterValue;
        private readonly IFlagRegisterField shutdownControl;
        private readonly IFlagRegisterField oneShotMode;

        private const double Resolution = 0.0625;

        private enum Registers: byte
        {
            Temperature = 0x0,
            Configuration = 0x1,
            TemperatureLowLimit = 0x2,
            TempratureHighLimit = 0x3,
            OneShot = 0x4,
        }
    }
}