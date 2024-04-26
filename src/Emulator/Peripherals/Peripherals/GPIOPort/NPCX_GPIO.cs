//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.GPIOPort
{
    public class NPCX_GPIO : BaseGPIOPort, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>, IKnownSize
    {
        public NPCX_GPIO(IMachine machine) : base(machine, NumberOfPinsPerPort)
        {
            RegistersCollection = new ByteRegisterCollection(this);
            DefineRegisters();
        }

        public byte ReadByte(long offset)
        {
            return RegistersCollection.Read(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            RegistersCollection.Write(offset, value);
        }

        public override void Reset()
        {
            base.Reset();
            RegistersCollection.Reset();
        }

        public long Size => 0x1000;

        public ByteRegisterCollection RegistersCollection { get; }

        private void DefineRegisters()
        {
            Registers.DataOut.Define(this)
                .WithFlags(0, 8, name: "DataOut",
                    valueProviderCallback: (index, _) => Connections[index].IsSet,
                    writeCallback: (index, _, value) =>
                    {
                        if(!pinDirection[index].Value)
                        {
                            return;
                        }

                        if(pinLockControl[index].Value)
                        {
                            this.Log(LogLevel.Warning, "Trying to set pin#{0} while it's locked", index);
                            return;
                        }

                        Connections[index].Set(value);
                    })
            ;

            Registers.DataIn.Define(this)
                .WithFlags(0, 8, FieldMode.Read, name: "DataIn",
                    valueProviderCallback: (index, _) => State[index]);
            ;

            Registers.Direction.Define(this)
                .WithFlags(0, 8, out pinDirection, name: "Direction")
            ;

            Registers.PullUpPullDownEnable.Define(this)
                .WithTaggedFlags("PullUpPullDownEnable", 0, 8)
            ;

            Registers.PullUpPullDownSelection.Define(this)
                .WithTaggedFlags("PullUpPullDownSelection", 0, 8)
            ;

            Registers.DriveEnable.Define(this)
                .WithTaggedFlags("DriveEnable", 0, 8)
            ;

            Registers.OutputType.Define(this)
                .WithTaggedFlags("OutputType", 0, 8)
            ;

            Registers.LockControl.Define(this)
                .WithFlags(0, 8, out pinLockControl, name: "LockControl")
            ;
        }

        private const int NumberOfPinsPerPort = 8;

        private IFlagRegisterField[] pinDirection;
        private IFlagRegisterField[] pinLockControl;

        private enum Registers
        {
            DataOut = 0x0,
            DataIn = 0x1,
            Direction = 0x2,
            PullUpPullDownEnable = 0x3,
            PullUpPullDownSelection = 0x4,
            DriveEnable = 0x5,
            OutputType = 0x6,
            LockControl = 0x7
        }
    }
}
