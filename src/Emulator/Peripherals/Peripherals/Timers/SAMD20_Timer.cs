using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Renode.Time;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Peripherals.Sensors;

namespace Antmicro.Renode.Peripherals.Timers
{
    [AllowedTranslations(AllowedTranslation.WordToDoubleWord | AllowedTranslation.ByteToDoubleWord)]
    public class SAMD20_Timer : IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral
    {


        public long Size => 0x100;

        public GPIO IRQ { get; private set; }



        public SAMD20_Timer(Machine machine, long frequency = DefaultPeripheralFrequency)
        {
            dwordregisters = new DoubleWordRegisterCollection(this);

            IRQ = new GPIO();

            Timer0 = new LimitTimer(machine.ClockSource, frequency, this, nameof(Timer0), 65535, direction: Direction.Ascending, workMode: WorkMode.OneShot);
            Timer1 = new LimitTimer(machine.ClockSource, frequency, this, nameof(Timer1), 65535, direction: Direction.Ascending, workMode: WorkMode.OneShot);
            Clock = new LimitTimer(machine.ClockSource, 1, this, nameof(Clock), 1, Direction.Ascending, false, WorkMode.Periodic, true, true, 1);

            DefineRegisters();
            Reset();

            Clock.LimitReached += TriggerTimer;
            Clock.EventEnabled = true;
            if (InputFrequency > 0)
                Clock.Enabled = true;
            else
                Clock.Enabled = false;

        }

        public void SetInputSignal (long frequency, int DutyCycleInPercent = DefaultInputDutyCycleInPercent)
        { 
            if (frequency > 0)
            {
                Clock.Enabled = false;
                InputFrequency = frequency;
                Clock.Frequency = InputFrequency;
                Clock.Enabled = true;
            }
            else
            {
                Clock.Enabled = false;
                InputFrequency = frequency;
            }

        }

        public long GetInputSignal()
        {
            return InputFrequency;
        }


        public void TriggerTimer ()
        {
            if ( (Timer1.Enabled == false) && (Enable.Value == true))
            {
                Timer1.Value = 0x0000;
                Timer1.Enabled = true;
            }
            else
            {
                Timer1.Enabled = false;
                CC1.Value = (uint) Timer1.Value;
                MC1.Value = true;
                UpdateInterrupts();
                if (Enable.Value == true)
                {
                    Timer1.Value = 0x0000;
                    Timer1.Enabled = true;
                }
            }
        }
        


        public  void Reset()
        {
            Timer0.Reset();
            Timer1.Reset();

            dwordregisters.Reset();

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
            return (byte)ReadDoubleWord(offset);
        }

        public void WriteByte(long offset, byte value)
        {
            WriteDoubleWord(offset, value);
        }

        public uint ReadDoubleWord(long offset)
        {
            return dwordregisters.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            dwordregisters.Write(offset, value);
        }
        private readonly DoubleWordRegisterCollection dwordregisters;

        private IFlagRegisterField Enable;
        private IFlagRegisterField OVF;
        private IFlagRegisterField OVF_Clear;
        private IFlagRegisterField OVF_Set;

        private IFlagRegisterField ERR;
        private IFlagRegisterField ERR_Clear;
        private IFlagRegisterField ERR_Set;

        private IFlagRegisterField SYNCRDY;
        private IFlagRegisterField SYNCRDY_Clear;
        private IFlagRegisterField SYNCRDY_Set;

        private IFlagRegisterField MC0;
        private IFlagRegisterField MC0_Clear;
        private IFlagRegisterField MC0_Set;

        private IFlagRegisterField MC1;
        private IFlagRegisterField MC1_Clear;
        private IFlagRegisterField MC1_Set;

        private IValueRegisterField CC0;
        private IValueRegisterField CC1;

        private IFlagRegisterField STOP;
        private const long DefaultPeripheralFrequency = 32000000;
        private const long DefaultInputFrequency = 0;
        private const int DefaultInputDutyCycleInPercent = 50;
        private long InputFrequency = DefaultInputFrequency;
        private LimitTimer Timer0;
        private LimitTimer Timer1;
        private LimitTimer Clock;

        private void DefineRegisters()
        {

            Registers.CTRLA.Define(dwordregisters, 0x00, "CTRLA")
            .WithFlag(0, FieldMode.WriteOneToClear | FieldMode.Read,
                        writeCallback: (_, value) =>
                        {
                            if (value == true)
                            {
                                Reset();
                            }
                        }, name: "SWRST")

            .WithFlag(1, out Enable, name: "Enable")

            .WithValueField(2, 14, name: "unused");

            Registers.READREQ.Define(dwordregisters, 0x00, "READREQ")
            .WithValueField(0, 16, name: "Reserved");

            Registers.CTRLBCLR.Define(dwordregisters, 0x00, "CTRLBCLR")
            .WithValueField(0, 8, name: "unused");

            Registers.CTRLBSET.Define(dwordregisters, 0x00, "CTRLBSET")
            .WithValueField(0, 8, name: "unused");

            //
            Registers.CTRLC.Define(dwordregisters, 0x00, "CTRLC")
            .WithValueField(0, 8, name: "unused");


            Registers.DBGCTRL.Define(dwordregisters, 0x00, "DBGCTRL")
            .WithValueField(0, 8, name: "unused");

            Registers.EVCTRL.Define(dwordregisters, 0x00, "EVCTRL")
            .WithValueField(0, 16, name: "unused");


            Registers.INTENCLR.Define(dwordregisters, 0x00, "INTENCLR")
            .WithFlag(0, out OVF_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    OVF_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "OVF Interrupt is disabled")
            .WithFlag(1, out ERR_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    ERR_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "ERR interrupt is disabled.")
            .WithTag("RESERVED", 2, 1)
            .WithFlag(3, out SYNCRDY_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    SYNCRDY_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "SYNCRDY Interrupt is disabled")
            .WithFlag(4, out MC0_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    MC0_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "MC0 Interrupt is disabled")
            .WithFlag(5, out MC1_Clear, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    MC1_Set.Value = false;
                    UpdateInterrupts();
                }
            }, name: "MC1 Interrupt is disabled")
            .WithTag("RESERVED", 6, 2);



            Registers.INTENSET.Define(dwordregisters, 0x00, "INTENSET")
            .WithFlag(0, out OVF_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    OVF_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "OVF Interrupt is enabled")
            .WithFlag(1, out ERR_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    ERR_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "ERR interrupt is enabled.")
            .WithTag("RESERVED", 2, 1)
            .WithFlag(3, out SYNCRDY_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    SYNCRDY_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "SYNCRDY Interrupt is enabled")
            .WithFlag(4, out MC0_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    MC0_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "MC0 Interrupt is enabled")
            .WithFlag(5, out MC1_Set, FieldMode.Read | FieldMode.Set,
            writeCallback: (_, value) =>
            {
                if (value == true)
                {
                    MC1_Clear.Value = false;
                    UpdateInterrupts();
                }
            }, name: "MC1 Interrupt is enabled")
            .WithTag("RESERVED", 6, 2);

            Registers.INTFLAG.Define(dwordregisters, 0x00, "INTFLAG")

            .WithFlag(0, out OVF, FieldMode.WriteOneToClear | FieldMode.Read, name: "OVF")
            .WithFlag(1, out ERR, FieldMode.WriteOneToClear | FieldMode.Read, name: "ERR")
            .WithTag("RESERVED", 2, 1)
            .WithFlag(3, out SYNCRDY, FieldMode.WriteOneToClear | FieldMode.Read, name: "SYNCRDY")
            .WithFlag(4, out MC0, FieldMode.WriteOneToClear | FieldMode.Read, name: "MC0")
            .WithFlag(5, out MC1, FieldMode.WriteOneToClear | FieldMode.Read, name: "MC1")

            .WithTag("RESERVED", 6, 2);


            Registers.STATUS.Define(dwordregisters, 0x08, "STATUS")
            .WithValueField(0, 3, name: "unused")
            .WithFlag(3, out STOP, FieldMode.Write | FieldMode.Read, name: "stop")
            .WithValueField(4, 4, name: "unused");


            Registers.COUNT.Define(dwordregisters, 0x00, "COUNT")
            .WithValueField(0, 16, name: "unused");

            Registers.CC0.Define(dwordregisters, 0x0000, "CC0")
            .WithValueField(0, 16, out CC0,
                            readCallback: (_, __) =>
                            {
                                MC0.Value = false;
                                UpdateInterrupts();
                            }, name: "CC0");

            Registers.CC1.Define(dwordregisters, 0x0000, "CC1")
            .WithValueField(0, 16, out CC1,
                            readCallback: (_, __) =>
                            {
                                MC1.Value = false;
                                UpdateInterrupts();
                            }, name: "CC1");


        }

        private void UpdateInterrupts()
        {
            bool OVF_IntActive = false;
            bool ERR_IntActive = false;
            bool SYNCRDY_IntActive = false;
            bool MC0_IntActive = false;
            bool MC1_IntActive = false;


            if (OVF_Set.Value & OVF.Value)
            {
                OVF_IntActive = true;
            }


            if (ERR_Set.Value & ERR.Value)
            {
                ERR_IntActive = true;
            }

            if (SYNCRDY_Set.Value & SYNCRDY.Value)
            {
                SYNCRDY_IntActive = true;
            }

            if (MC0_Set.Value & MC0.Value)
            {
                MC0_IntActive = true;
            }

            if (MC1_Set.Value & MC1.Value)
            {
                MC1_IntActive = true;
            }
            // Set or Clear Interrupt
            IRQ.Set(OVF_IntActive | ERR_IntActive | SYNCRDY_IntActive | MC0_IntActive | MC1_IntActive);



        }



        private enum Registers : long
        {
            CTRLA = 0x00,
            READREQ = 0x02,
            CTRLBCLR = 0x04,
            CTRLBSET = 0x05,
            CTRLC = 0x06,
            DBGCTRL = 0x08,
            EVCTRL = 0x0A,
            INTENCLR = 0x0C,
            INTENSET = 0x0D,
            INTFLAG = 0x0E,
            STATUS = 0x0F,
            COUNT = 0x10,
            CC0 = 0x18,
            CC1 = 0x1A
        }

    }
}