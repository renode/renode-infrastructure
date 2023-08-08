//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.I2C;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class EOSS3_FlexibleFusionEngine : BasicDoubleWordPeripheral, IKnownSize, IPeripheralContainer<IBytePeripheral, NumberRegistrationPoint<int>>
    {
        public EOSS3_FlexibleFusionEngine(IMachine machine) : base(machine)
        {
            children = new Dictionary<int, IBytePeripheral>();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            foreach(var child in children.Values)
            {
                child.Reset();
            }
        }

        public IEnumerable<NumberRegistrationPoint<int>> GetRegistrationPoints(IBytePeripheral peripheral)
        {
            return children.Keys.Select(x => new NumberRegistrationPoint<int>(x));
        }

        public IEnumerable<IRegistered<IBytePeripheral, NumberRegistrationPoint<int>>> Children
        {
            get
            {
                return children.Select(x => Registered.Create(x.Value, new NumberRegistrationPoint<int>(x.Key)));
            }
        }

        public virtual void Register(IBytePeripheral peripheral, NumberRegistrationPoint<int> registrationPoint)
        {
            if(children.ContainsKey(registrationPoint.Address))
            {
                throw new RegistrationException("The specified registration point is already in use.");
            }
            if(!(peripheral is OpenCoresI2C))
            {
                throw new RegistrationException("The FFE Wishbone interface supports the OpenCoresI2C controller only");
            }
            children.Add(registrationPoint.Address, peripheral);
            machine.RegisterAsAChildOf(this, peripheral, registrationPoint);
        }

        public virtual void Unregister(IBytePeripheral peripheral)
        {
            var toRemove = children.Where(x => x.Value.Equals(peripheral)).Select(x => x.Key).ToList();
            if(toRemove.Count == 0)
            {
                throw new RegistrationException("The specified peripheral was never registered.");
            }
            foreach(var key in toRemove)
            {
                children.Remove(key);
            }
            machine.UnregisterAsAChildOf(this, peripheral);
        }

        public long Size => 0x2000;

        private void DefineRegisters()
        {
            Registers.WishboneAddress.Define(this)
                .WithValueField(0, 3, out slaveAddress, name: "Addr")
                .WithReservedBits(3, 3)
                .WithEnumField(6, 2, out selectedSlave, name: "slave_sel")
                .WithReservedBits(8, 24)
            ;

            Registers.WriteData.Define(this)
                .WithValueField(0, 8, out writeData, name: "WDATA")
                .WithReservedBits(8, 24)
            ;

            Registers.ControlAndStatus.Define(this)
                .WithFlag(0, out startTransfer, FieldMode.Set, name: "wb_ms_start")
                .WithFlag(1, out isWrite, name: "wb_ms_wen")
                .WithTaggedFlag("mux_wb_sn", 2)
                .WithFlag(3, FieldMode.Read, valueProviderCallback: _ => false, name: "BUSY")
                .WithFlag(4, FieldMode.Read, valueProviderCallback: _ => false, name: "OVFL")
                // These are three fields defining if the peripheral should be controlled from wishbone bus master or Sensor Manager.
                // We assume the selectedSlave is configured anyway.
                .WithValueField(5, 3, name: "mux_sel")
                .WithReservedBits(8, 24)
                .WithWriteCallback(ControlWrite)
            ;

            Registers.ReadData.Define(this)
                .WithValueField(0, 8, out readData, FieldMode.Read, name: "RDATA")
                .WithReservedBits(8, 24)
            ;
        }

        private void ControlWrite(uint _, uint value)
        {
            if(!children.TryGetValue((int)selectedSlave.Value, out var slave))
            {
                this.Log(LogLevel.Warning, "Trying to {1} slave {0}, but it is not registered", selectedSlave.Value,
                    isWrite.Value ? "write to" : "read from");
                return;
            }
            if(startTransfer.Value)
            {
                startTransfer.Value = false;
                if(isWrite.Value)
                {
                    this.Log(LogLevel.Noisy, "Writing to slave {0} at 0x{1:X}, value 0x{2:X}", selectedSlave.Value, slaveAddress.Value, writeData.Value);
                    slave.WriteByte((long)slaveAddress.Value << 2, (byte)writeData.Value);
                }
                else
                {
                    readData.Value = slave.ReadByte((long)slaveAddress.Value << 2);
                    this.Log(LogLevel.Noisy, "Read from slave {0} at 0x{1:X}, value 0x{2:X}", selectedSlave.Value, slaveAddress.Value, readData.Value);
                }
            }
        }

        private Dictionary<int, IBytePeripheral> children;
        private IFlagRegisterField isWrite;
        private IValueRegisterField slaveAddress;
        private IFlagRegisterField startTransfer;
        private IValueRegisterField writeData;
        private IValueRegisterField readData;
        private IEnumRegisterField<WishboneSlave> selectedSlave;

        private enum WishboneSlave
        {
            I2CMaster0 = 0,
            I2CMaster1 = 1,
            SPIMaster0 = 2
        }

        private enum Registers
        {
            WishboneAddress = 0x0,
            WriteData = 0x4,
            ControlAndStatus = 0x8,
            ReadData= 0xC,
            SRAMTest1 = 0x14,
            SRAMTest2 = 0x18,
            FFEControlAndStatus = 0x20,
            FFEDebugCombined = 0x38,
            Command = 0x100,
            Interrupt = 0x108,
            InterruptEnable = 0x10c,
            Status = 0x110,
            MailboxToFFE0 = 0x114,
            SMRuntimeAddress = 0x120,
            SM0RuntimeAddressControl = 0x124,
            SM1RuntimeAddressControl = 0x128,
            SM0RuntimeAddressCurrent = 0x12c,
            SM1RuntimeAddressCurrent = 0x130,
            SM0DebugSelection = 0x140,
            SM1DebugSelection = 0x144,
            FFEDebugSelection = 0x148,
            FFE0BreakpointConfig = 0x150,
            FFE0BreakPointContinue = 0x154,
            FFE0BreakPointStatus = 0x158,
            FFE0BreakpointProgramCounter0 = 0x160,
            FFE0BreakpointProgramCounter1 = 0x164,
            FFE0BreakpointProgramCounter2 = 0x168,
            FFE0BreakpointProgramCounter3 = 0x16c,
        }
    }
}
