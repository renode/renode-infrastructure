//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using System;
using Antmicro.Migrant;


namespace Antmicro.Renode.Peripherals.UART
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_UART : IDoubleWordPeripheral,  IUART, IKnownSize, IWordPeripheral, IBytePeripheral
    {

        public event Action<byte> CharReceived;

        public SAMD20_UART(Machine machine) 
        {
            IRQ = new GPIO();
            dwordregisters = new DoubleWordRegisterCollection(this);

            DefineRegisters();
        }




        public long Size => 0x100;

        public GPIO IRQ { get; private set; }

        public Bits StopBits
        {
            get
            {
                if (bStopBit.Value == true)
                    return Bits.Two;
                else
                    return Bits.One;
            }
        }

        public Parity ParityBit
        {
            get
            {
                if (bParity.Value==true)
                   return Parity.Odd;
                else
                    return Parity.Even;
            }
        }

        public uint BaudRate
        {
            get
            {
                return baudrate.Value;
            }
        }



        public void Reset()
        {
            dwordregisters.Reset();
            UpdateInterrupts();
        }

        public void WriteChar(byte value)
        {
            receiveFifo.Enqueue(value);
            bRXC.Value = true;
            UpdateInterrupts();
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
            return (byte) ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return (dwordregisters.Read(offset));
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
            UpdateInterrupts();
        }

        private readonly Queue<byte> receiveFifo = new Queue<byte>();


        private IFlagRegisterField bDRE;
        private IFlagRegisterField bDRE_Clear;
        private IFlagRegisterField bDRE_Set;
        private IFlagRegisterField bTXC;
        private IFlagRegisterField bTXC_Clear;
        private IFlagRegisterField bTXC_Set;
        private IFlagRegisterField bRXC;
        private IFlagRegisterField bRXC_Clear;
        private IFlagRegisterField bRXC_Set;
        private IFlagRegisterField bRXS;
        private IFlagRegisterField bRXS_Clear;
        private IFlagRegisterField bRXS_Set;
        private IFlagRegisterField bParity;
        private IFlagRegisterField bStopBit;
        private IValueRegisterField baudrate;
        private DoubleWordRegisterCollection dwordregisters;

        private void DefineRegisters()
        {

            Register.ControlA.Define(dwordregisters, 0x00, "ControlA")
            .WithFlag(0, name: "Software Reset")
            .WithFlag(1, name: "Enable")
            .WithValueField(2, 3, name: "Mode")
            .WithTag("Reserved", 5, 2)
            .WithFlag(7, name: "Run In Standby")
            .WithFlag(8, name: "Immediate Buffer Overflow Notification")
            .WithTag("Reserved", 9, 7)
            .WithFlag(16, name: "Transmit Data Pinout")
            .WithTag("Reserved", 17, 2)
            .WithValueField(20, 2, name: "Receive Data Pinout")
            .WithTag("Reserved", 22, 2)
            .WithValueField(24, 4, name: "Frame Format")
            .WithFlag(28, name: "Communication Mode")
            .WithFlag(29, name: "Clock Polarity")
            .WithFlag(30, name: "Data Order")
            .WithTag("Reserved", 31, 1);

            Register.ControlB.Define(dwordregisters, 0x00, "ControlB")
                .WithValueField(0, 3, name: "Character Size")
                .WithTag("Reserved", 3, 3)
                .WithFlag(6, out bStopBit, FieldMode.Read | FieldMode.Write, name: "Stop Bit Mode")
                .WithTag("Reserved", 7, 2)
                .WithFlag(9, name: "SFDE")
                .WithTag("Reserved", 10, 3)
                .WithFlag(13, out bParity, FieldMode.Read | FieldMode.Write, name: "PMODE")
                .WithTag("RESERVED", 14, 2)
                .WithFlag(16, name: "TXEN")
                .WithFlag(17, name: "RXEN")
                .WithTag("RESERVED", 18, 14);

            Register.DebugControl.Define(dwordregisters, 0x00, "DebugControl")
                .WithTag("RESERVED", 0, 1);


            Register.Baudrate.Define(dwordregisters, 0x00, "Baudrate")
                .WithValueField(0, 16, out baudrate, FieldMode.Read | FieldMode.Write, name: "Baudraute");

            Register.IntenClr.Define(dwordregisters, 0x00, "IntenClr")
            .WithFlag(0, out bDRE_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bDRE_Set.Value = false;
                }
            }, name: "Data Register Empty Interrupt Enable")
            .WithFlag(1, out bTXC_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bTXC_Set.Value = false;
                }
            }, name: "Transmit Complete interrupt is disabled.")
            .WithFlag(2, out bRXC_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bRXC_Set.Value = false;
                }
            }, name: "Receive Complete Interrupt Enable")
            .WithFlag(3, out bRXS_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bRXS_Set.Value = false;
                }
            }, name: "Receive Start Interrupt Enable")
            .WithTag("RESERVED", 4, 4);

            Register.IntenSet.Define(dwordregisters, 0x00, "IntenSet")
            .WithFlag(0, out bDRE_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    bDRE_Clear.Value = false;
                }
            }, name: "Data Register Empty Interrupt Enable")
            .WithFlag(1, out bTXC_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bTXC_Clear.Value = false;
                 }
             }, name: "Transmit Complete interrupt is disabled.")
            .WithFlag(2, out bRXC_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bRXC_Clear.Value = false;
                 }
             }, name: "Receive Complete Interrupt Enable")
            .WithFlag(3, out bRXS_Set, FieldMode.Read | FieldMode.Set,
             writeCallback: (_, value) =>
             {
                 if (value == true)
                 {
                     bRXS_Clear.Value = false;
                 }
             }, name: "Receive Start Interrupt Enable")
            .WithTag("RESERVED", 4, 4);


            Register.IntFlag.Define(dwordregisters, 0x01, "IntFlag")
            .WithFlag(0, out bDRE, FieldMode.Read, name: "Data Register Empty")
            .WithFlag(1, out bTXC, FieldMode.WriteOneToClear | FieldMode.Read, name: "Transmit Complete.")
            .WithFlag(2, out bRXC, FieldMode.Read, name: "Receive Complete")
            .WithFlag(3, out bRXS, FieldMode.WriteOneToClear | FieldMode.Read, name: "Receive Start")
            .WithTag("RESERVED", 4, 4);

            Register.Status.Define(dwordregisters, 0x00, "Status")
            .WithFlag(0, name: "Parity Error")
            .WithFlag(1, name: "Frame Error")
            .WithFlag(2, name: "Buffer Overflow")
            .WithTag("RESERVED", 3, 12)
            .WithFlag(15, name: "Synchronization Busy");

            Register.Data.Define(dwordregisters, 0x00, "Data")
            .WithValueField(0, 9,
                                writeCallback: (_, value) =>
                                {
                                    this.Log(LogLevel.Noisy, "SAMD2x_UART: Data Send");
                                    bDRE.Value = false;
                                    CharReceived?.Invoke((byte)value);
                                    bTXC.Value = true;
                                    bDRE.Value = true;
                                },
                                valueProviderCallback: _ =>
                                {
                                    uint value = 0;
                                    if (receiveFifo.Count > 0)
                                    {
                                        value = receiveFifo.Dequeue();
                                        this.Log(LogLevel.Noisy, "SAMD2x_UART: Data Receive");
                                    }
                                    if (receiveFifo.Count == 0)
                                    {
                                        bRXC.Value = false;
                                        this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC = false");
                                    }
                                    return value;
                                }, name: "data")
            .WithTag("RESERVED", 9, 7);


        }

        private void UpdateInterrupts()
        {
            bool bDRE_IntActive = false;
            bool bTXC_IntActive = false;
            bool bRXC_IntActive = false;
            bool bRXS_IntActive = false;

            if (bDRE_Set.Value & bDRE.Value)
            {
                bDRE_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: DRE Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: DRE Int Off");


            if (bTXC_Set.Value & bTXC.Value)
            {
                bTXC_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: TXC Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: TXC Int Off");

            if (bRXC_Set.Value & bRXC.Value)
            {
                bRXC_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXC Int Off");

            if (bRXS_Set.Value & bRXS.Value)
            {
                bRXS_IntActive = true;
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXS Int Active");
            }
            else
                this.Log(LogLevel.Noisy, "SAMD2x_UART: RXS Int Off");

            // Set or Clear Interrupt
            IRQ.Set(bRXS_IntActive | bRXC_IntActive | bTXC_IntActive | bDRE_IntActive);

        }


        private enum Register : long
        {
            ControlA= 0x00,
            ControlB= 0x04,
            DebugControl = 0x08,
            Baudrate =0x0A,
            IntenClr = 0x0C,
            IntenSet = 0x0D,
            IntFlag = 0x0E,
            Status = 0x10,
            Data = 0x18
        }

    }
}