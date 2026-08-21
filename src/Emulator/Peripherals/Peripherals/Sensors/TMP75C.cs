using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class TMP75C : II2CPeripheral, ITemperatureSensor, IProvidesRegisterCollection<WordRegisterCollection>
    {
        public TMP75C()
        {
            RegistersCollection = new WordRegisterCollection(this);
            Registers.Temperature.Define(this)
                .WithReservedBits(0, 4)
                .WithValueField(4, 12, mode: FieldMode.Read, name: "Temprature", valueProviderCallback: (_) => 
                {
                    return 0x10;
                });
            Registers.Configuration.Define(this)
                .WithReservedBits(0, 8)
                .WithTaggedFlag("Shutdown Control", 8)
                .WithTaggedFlag("Alert Theromstat mode", 9)
                .WithTaggedFlag("Alert polarity control", 10)
                .WithTag("Falt queue", 11, 2)
                .WithFlag(13, out oneShotMode, name: "One shot control")
                .WithReservedBits(14, 2);
        }

        public void Reset()
        {
            RegistersCollection.Reset();
            OneShotReading = 0;
        }

        public void FinishTransmission()
        {
            throw new System.NotImplementedException();
        }

        public byte[] Read(int count = 1)
        {
            throw new System.NotImplementedException();
        }

        public void Write(byte[] data)
        {
            if(data.Length < 1)
            {
                this.WarningLog("Write with length {0} unexpected. Should be at least 1", data.Length);
            }
            pointerRegister = (Registers)data[0];
            if(data.Length > 1)
            {
                //RegistersCollection.Write((byte)pointerRegister, )
            }
        }

        public decimal Temperature { get; set; }

        public WordRegisterCollection RegistersCollection { get; private set; }

        private decimal OneShotReading { get; set; }

        private Registers? pointerRegister;

        private readonly IFlagRegisterField oneShotMode;

        private enum Registers: byte
        {
            Temperature = 0x0,
            Configuration = 0x1,
            TemperatureLowLimit = 0x2,
            TempratureHighLimit = 0x3
        }
    }
}