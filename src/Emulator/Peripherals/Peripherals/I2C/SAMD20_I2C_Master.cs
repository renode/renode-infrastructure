//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using System;

namespace Antmicro.Renode.Peripherals.I2C
{

    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_I2C_Master : SimpleContainer<II2CPeripheral>, IKnownSize, IWordPeripheral, IBytePeripheral, IDoubleWordPeripheral
    {

        public SAMD20_I2C_Master(Machine machine) : base(machine)
        {
            IRQ = new GPIO();
            dwordregisters = new DoubleWordRegisterCollection(this);
            DefineRegisters();


        }




        public ushort ReadWord(long offset)
        {
            return (ushort)ReadDoubleWord(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            WriteDoubleWord(offset, value);
        }

        public byte ReadByte(long offset)
        {
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public long Size => 0x100;

        public GPIO IRQ { get; private set; }


        public override void Reset()
        {
            dwordregisters.Reset();
            UpdateInterrupts();
        }
        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
            UpdateInterrupts();
        }

        public enum Registers : long
        {
            ControlA = 0x0,
            ControlB = 0x04,
            DebugControl = 0x08,
            Baudrate = 0x0A,
            IntenClr = 0x0C,
            IntenSet = 0x0D,
            IntFlag = 0x0E,
            Status = 0x10,
            Address = 0x14,
            Data = 0x18
        }

        private readonly DoubleWordRegisterCollection dwordregisters;
        private int SlaveAddress = 0x00;
        //        private Boolean MB_Interrupt_Enabled;
        private Boolean bReadActive;

        private IFlagRegisterField bMB;
        private IFlagRegisterField bMB_Set;
        private IFlagRegisterField bMB_Clear;
        private IFlagRegisterField bSB;
        private IFlagRegisterField bSB_Set;
        private IFlagRegisterField bSB_Clear;
        private IFlagRegisterField bBusError;
        private IFlagRegisterField bArbLost;
        private IFlagRegisterField bRxNack;
        private IFlagRegisterField bLowTOut;

        private List<byte> txpacket = new List<byte>();


        private void DefineRegisters()
        {

            Registers.ControlA.Define(dwordregisters, 0x00, "ControlA")
            .WithFlag(0, name: "Software Reset")
            .WithFlag(1, name: "Enable")
            .WithValueField(2, 3, name: "Mode")
            .WithTag("Reserved", 5, 2)
            .WithFlag(7, name: "Run In Standby")
            .WithTag("Reserved", 8, 8)
            .WithFlag(16, name: "Pinout")
            .WithTag("Reserved", 17, 3)
            .WithValueField(20, 2, name: "SDAHold")
            .WithTag("Reserved", 22, 6)
            .WithValueField(28, 2, name: "InActOut")
            .WithFlag(30, name: "LowTOut")
            .WithTag("Reserved", 31, 1);

            Registers.ControlB.Define(dwordregisters, 0x00, "ControlB")
            .WithTag("Reserved", 0, 8)
            .WithFlag(8, name: "SMEN")
            .WithFlag(9, name: "QCEN")
            .WithTag("Reserved", 10, 6)
            .WithValueField(16, 2,
                                        writeCallback: (_, value) =>
                                        {
                                            if (value == 0x03)
                                            {
                                                II2CPeripheral slave;
                                                if (TryGetByAddress(SlaveAddress & 0xFE, out slave))
                                                {
                                                    slave.FinishTransmission();
                                                }
                                            }
                                            value = 0x00;
                                            bSB.Value = false;
                                            bMB.Value = false;
                                            bReadActive = false;
                                        },
                                        name: "Cmd")
            .WithFlag(18, name: "AckAct")
            .WithTag("RESERVED", 19, 13);

            Registers.DebugControl.Define(dwordregisters, 0x00, "DebugControl")
            .WithTag("RESERVED", 0, 1);

            Registers.Baudrate.Define(dwordregisters, 0x00, "Baudrate")
            .WithValueField(0, 8, name: "Baud")
            .WithValueField(8, 8, name: "BaudLow");

            Registers.IntenClr.Define(dwordregisters, 0x00, "IntenClr")
            .WithFlag(0, out bMB_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bMB_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "MB Interrupt clear")
            .WithFlag(1, out bSB_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bSB_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "SP interrupt clear.")
            .WithTag("RESERVED", 2, 6);

            Registers.IntenSet.Define(dwordregisters, 0x00, "IntenSet")
            .WithFlag(0, out bMB_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bMB_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "Interrupt Enable MB")
            .WithFlag(1, out bSB_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bSB_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "interrupt enabled SB.")
            .WithTag("RESERVED", 2, 6);

            Registers.IntFlag.Define(dwordregisters, 0x00, "IntFlag")
            .WithFlag(0, out bMB, FieldMode.WriteOneToClear | FieldMode.Read,
                                       writeCallback: (_, value) =>
                                       {
                                           UpdateInterrupts();

                                       }, name: "MB")
            .WithFlag(1, out bSB, FieldMode.WriteOneToClear | FieldMode.Read,
                                        writeCallback: (_, value) =>
                                        {
                                            UpdateInterrupts();

                                        }, name: "SB")
            .WithTag("RESERVED", 2, 6);

            Registers.Status.Define(dwordregisters, 0x00, "Status")
            .WithFlag(0, out bBusError, writeCallback: (_, value) =>
            {
                if (value)
                {
                    bBusError.Value = false;
                }
            }, name: "Bus Error")
            .WithFlag(1, out bArbLost, writeCallback: (_, value) =>
            {
                if (value)
                {
                    bArbLost.Value = false;
                }
            }, name: "ArbLost")
            .WithFlag(2, out bRxNack, writeCallback: (_, value) =>
            {
                if (value)
                {
                    bRxNack.Value = false;
                }
            }, name: "RxNack")
            .WithTag("RESERVED", 3, 1)
            .WithValueField(4, 2, name: "BusState")
            .WithFlag(6, out bLowTOut, writeCallback: (_, value) =>
            {
                if (value)
                {
                    bLowTOut.Value = false;
                }
            }, name: "LowTOut")
            .WithFlag(7, name: "ClkHold")
            .WithTag("RESERVED", 8, 2)
            .WithFlag(10, name: "SyncBusy")
            .WithTag("RESERVED", 11, 5);

            Registers.Address.Define(dwordregisters, 0x00, "Adress")
            .WithValueField(0, 8,
                                       writeCallback: (_, value) =>
                                       {
                                           if (0x01 == (value & 0x01))   // ReadAddress
                                           {
                                               bSB.Value = true;
                                               bMB.Value = false;
                                               bReadActive = true;
                                           }
                                           else                          // WriteAddress
                                           {
                                               bSB.Value = false;
                                               bMB.Value = true;
                                               bReadActive = false;
                                           }
                                           SlaveAddress = (int)value; // >> 1;
                                           UpdateInterrupts();
                                       },
                                       name: "address")
              .WithTag("RESERVED", 8, 8);

            Registers.Data.Define(dwordregisters, 0x00, "Data")
            .WithValueField(0, 8,
                                writeCallback: (_, value) =>
                                {
                                    //                                    bSB.Value = false;
                                    bMB.Value = true;
                                    II2CPeripheral slave;
                                    this.Log(LogLevel.Noisy, "SAMD20 I2C Master send Data 0x{0:X}", value);

                                    if (TryGetByAddress(SlaveAddress, out slave))
                                    {
                                        txpacket.Add((byte)value);
                                        slave.Write(txpacket.ToArray());
                                        txpacket.Clear();
                                    }
                                    UpdateInterrupts();

                                },
                                valueProviderCallback: _ =>
                                {
                                    if (bReadActive)
                                        bSB.Value = true;
                                    //                                    bMB.Value = false;
                                    this.Log(LogLevel.Noisy, "SAMD20 I2C Master read Data");
                                    II2CPeripheral slave;
                                    if (TryGetByAddress(SlaveAddress & 0xFE, out slave))
                                    {
                                        var rxpacket = slave.Read(1);
                                        UpdateInterrupts();
                                        return (rxpacket[0]);
                                    }
                                    return 0x00; // character;
                                }, name: "data")
            .WithTag("RESERVED", 8, 8);

        }

        private void UpdateInterrupts()
        {
            bool bMB_IntActive = false;
            bool bSB_IntActive = false;

            if (bMB_Set.Value & bMB.Value)
            {
                bMB_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD20 Int Active bMB");
            }
            else
            this.Log(LogLevel.Noisy, "SAMD20 Int OFF bMB");


            if (bSB_Set.Value & bSB.Value)
            {
                bSB_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD20 Int Active bSB");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD20 Int OFF bSB");


            // Set or Clear Interrupt
            IRQ.Set(bMB_IntActive | bSB_IntActive);

        }






    }
}
