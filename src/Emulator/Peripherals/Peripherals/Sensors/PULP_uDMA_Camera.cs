//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Sensor;

namespace Antmicro.Renode.Peripherals.Sensors
{
    public class PULP_uDMA_Camera: NullRegistrationPointPeripheralContainer<ICPIPeripheral>, IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public PULP_uDMA_Camera(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            sysbus = machine.GetSystemBus(this);

            RegistersCollection = new DoubleWordRegisterCollection(this);
            DefineRegisters();
        }

        public override void Reset()
        {
            RegistersCollection.Reset();
            // there is no need to clear IRQ
            // as we only blink with it
        }

        public uint ReadDoubleWord(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            RegistersCollection.Write(offset, value);
        }

        public DoubleWordRegisterCollection RegistersCollection { get; }

        public GPIO IRQ { get; }

        public long Size => 0x100;

        private void DefineRegisters()
        {
            Registers.RxBufferBaseAddress.Define(this)
                // this is not consistent with the documentation
                // that states that only 16 bits are used for the address,
                // but otherwise the sample fails
                .WithValueField(0, 32, out rxBufferAddress, name: "RX_SADDR")
            ;

            Registers.RxBufferSize.Define(this)
                .WithValueField(0, 17, out rxBufferSize, name: "RX_SIZE")
                .WithReservedBits(17, 15)
            ;

            Registers.RxStreamConfiguration.Define(this)
                .WithTag("CONTINOUS", 0, 1)
                .WithTag("DATASIZE", 1, 2)
                .WithReservedBits(3, 1)
                .WithFlag(4, out rxStreamEnabled, name: "EN")
                .WithTag("PENDING", 5, 1)
                .WithTag("CLR", 6, 1)
                .WithReservedBits(7, 25)
            ;

            Registers.GlobalConfiguration.Define(this)
                .WithTag("FRAMEDROP_EN", 0, 1)
                .WithTag("FRAMEDROP_VAL", 1, 6)
                .WithTag("FRAMESLICE_EN", 7, 1)
                .WithTag("FORMAT", 8, 3)
                .WithTag("SHIFT", 11, 4)
                .WithReservedBits(15, 16)
                .WithFlag(31, FieldMode.Read | FieldMode.WriteOneToClear, name: "EN", writeCallback: (_, val) =>
                {
                    // write-one-to-clear means that this bit is automatically
                    // cleared after writing
                    
                    if(!val)
                    {
                        return;
                    }

                    if(!rxStreamEnabled.Value)
                    {
                        this.Log(LogLevel.Warning, "Tried to enable the controller, but RX DMA stream is not enabled. Dropping it");
                        return;
                    }

                    if(RegisteredPeripheral == null)
                    {
                        this.Log(LogLevel.Warning, "Tried to enable the controller, but there is no device connected");
                        return;
                    }

                    var data = RegisteredPeripheral.ReadFrame();

                    if(data.Length != (int)rxBufferSize.Value)
                    {
                        this.Log(LogLevel.Warning, "Received {0} bytes from the device, but RX DMA stream is configured for {1} bytes. This might indicate problems in the driver", data.Length, rxBufferSize.Value);
                    }

                    sysbus.WriteBytes(data, rxBufferAddress.Value);
                    rxStreamEnabled.Value = false;
                    IRQ.Blink();
                })
            ;

            Registers.LowerLeftCornerConfiguration.Define(this)
                .WithTag("FRAMESLICE_LLX", 0, 16)
                .WithTag("FRAMESLICE_LLY", 16, 16)
            ;

            Registers.UpperRightCornderConfiguration.Define(this)
                .WithTag("FRAMESLICE_URX", 0, 16)
                .WithTag("FRAMESLICE_URY", 16, 16)
            ;

            Registers.HorizontalResolutionConfiguration.Define(this)
                .WithReservedBits(0, 16)
                .WithTag("ROWLEN", 16, 16)
            ;

            Registers.RGBCoefficientsConfiguration.Define(this)
                .WithTag("B_COEFF", 0, 8)
                .WithTag("G_COEFF", 8, 8)
                .WithTag("R_COEFF", 16, 8)
                .WithReservedBits(24, 8)
            ;

            Registers.VSYNCPolarity.Define(this)
                .WithTag("VSYNC_POLARITY", 0, 1)
                .WithReservedBits(1, 31)
            ;
        }

        private IValueRegisterField rxBufferAddress;
        private IValueRegisterField rxBufferSize;
        private IFlagRegisterField rxStreamEnabled;

        private readonly IBusController sysbus;

        private enum Registers
        {
            RxBufferBaseAddress = 0x0,
            RxBufferSize = 0x4,
            RxStreamConfiguration = 0x8,

            GlobalConfiguration = 0x20,
            LowerLeftCornerConfiguration = 0x24,
            UpperRightCornderConfiguration = 0x28,
            HorizontalResolutionConfiguration = 0x2C,
            RGBCoefficientsConfiguration = 0x30,
            VSYNCPolarity = 0x34
        }
    }
}
