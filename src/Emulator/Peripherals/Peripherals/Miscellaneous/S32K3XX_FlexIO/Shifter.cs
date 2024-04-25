//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using static Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIO;

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public class Shifter : ResourceBlock
    {
        public static IReadOnlyList<Shifter> BuildRegisters(IResourceBlockOwner owner, int count, ResourceBlocksManager<Timer> timersManager)
        {
            var statusFlags = Interrupt.BuildRegisters(owner, count, "ShifterStatus", Registers.ShifterStatus, Registers.ShifterStatusInterruptEnable);
            var errorFlags = Interrupt.BuildRegisters(owner, count, "ShifterError", Registers.ShifterError, Registers.ShifterErrorInterruptEnable);

            return Enumerable.Range(0, count)
                .Select(index => BuildShifter(owner, index, statusFlags[index], errorFlags[index], timersManager))
                .ToList().AsReadOnly();
        }

        public override void Reset()
        {
            base.Reset();
            receiveBuffer.Clear();
        }

        public void OnDataReceive(uint data)
        {
            if(mode.Value != ShifterMode.Receive)
            {
                owner.Log(LogLevel.Warning, "Trying to receive data (0x{0:X}) using the shifter with ID {1} that isn't in the receive mode", data, Identifier);
                return;
            }

            receiveBuffer.Enqueue(data);
            if(receiveBuffer.Count == 1)
            {
                // Trigger an interrupt when there was no data in the buffer before.
                buffer.Value = data;
                Status.SetFlag(true);
            }
        }

        public event Action<uint> DataTransmitted;
        public event Action ControlOrConfigurationChanged;

        public override IEnumerable<Interrupt> Interrupts => new[] { Status, Error };
        public override string Name => $"Shifter{Identifier}";

        public Interrupt Status { get; }
        public Interrupt Error { get; }
        public Timer Timer
        {
            get
            {
                timersManager.TryGet((uint)timerSelect.Value, out var timer);
                return timer;
            }
        }
        public ShifterPolarity TimerPolarity => timerPolarity.Value;
        public ShifterMode Mode => mode.Value;
        public uint StopBit => (uint)stopBit.Value;
        public uint StartBit => (uint)startBit.Value;

        private static Shifter BuildShifter(IResourceBlockOwner owner, int index, Interrupt status, Interrupt error,
            ResourceBlocksManager<Timer> timersManager)
        {
            Shifter shifter = null;
            var offset = index * 4;

            var bufferField = (Registers.ShifterBuffer0 + offset).Define(owner)
                .DefineValueField(0, 32, name: $"SHIFTBUF (Shifter Buffer)",
                    writeCallback: (prev, val) => shifter.OnBufferWrite((uint)val),
                    readCallback: (_, __) => shifter.OnBufferRead()
                );

            var controlRegister = (Registers.ShifterControl0 + offset).Define(owner)
                .WithReservedBits(27, 5)
                .WithReservedBits(18, 5)
                .WithTag("PINCFG (Shifter Pin Configuration)", 16, 2)
                .WithReservedBits(13, 3)
                .WithTag("PINSEL (Shifter Pin Select)", 8, 5)
                .WithTaggedFlag("PINPOL (Shifter Pin Polarity)", 7)
                .WithReservedBits(3, 4);

            var timerSelectField = controlRegister.DefineValueField(24, 3, name: "TIMSEL (Timer Select)");
            var timerPolarityField = controlRegister.DefineEnumField<ShifterPolarity>(23, 1, name: "TIMPOL (Timer Polarity)");
            var modeField = controlRegister.DefineEnumField<ShifterMode>(0, 3, name: $"SMOD (Shifter Mode)", changeCallback: (__, val) => shifter.OnModeChange(val));
            controlRegister.WithChangeCallback((_, __) => shifter.OnControlOrConfigurationChange());

            var configurationRegister = (Registers.ShifterConfiguration0 + offset).Define(owner)
                .WithReservedBits(21, 11)
                .WithTag("PWIDTH (Parallel Width)", 16, 5)
                .WithReservedBits(13, 3)
                .WithTaggedFlag("SSIZE (Shifter Size)", 12)
                .WithReservedBits(10, 2)
                .WithTaggedFlag("LATST (Late Store)", 9)
                .WithTaggedFlag("INSRC (Input Source)", 8)
                .WithReservedBits(6, 2)
                .WithReservedBits(2, 2);

            var stopBitField = configurationRegister.DefineValueField(4, 2, name: "SSTOP (Shifter Stop Bit)");
            var startBitField = configurationRegister.DefineValueField(0, 2, name: "SSTART (Shifter Start Bit)");
            configurationRegister.WithChangeCallback((_, __) => shifter.OnControlOrConfigurationChange());

            DefineStubRegisters(owner, offset);
            shifter = new Shifter(
                owner,
                (uint)index,
                timersManager,
                status,
                error,
                bufferField,
                timerSelectField,
                timerPolarityField,
                modeField,
                stopBitField,
                startBitField
            );
            return shifter;
        }

        private static void DefineStubRegisters(IProvidesRegisterCollection<DoubleWordRegisterCollection> owner, int offset)
        {
            (Registers.ShifterBuffer0BitSwapped + offset).Define(owner).WithTag("SHIFTBUFBIS (Shifter Buffer Bit Swapped)", 0, 32);
            (Registers.ShifterBuffer0ByteSwapped + offset).Define(owner).WithTag("SHIFTBUFBYS (Shifter Buffer Byte Swapped)", 0, 32);
            (Registers.ShifterBuffer0BitByteSwapped + offset).Define(owner).WithTag("SHIFTBUFBBS (Shifter Buffer Bit Byte Swapped)", 0, 32);
            (Registers.ShifterBuffer0NibbleByteSwapped + offset).Define(owner).WithTag("SHIFTBUFNBS (Shifter Buffer Nibble Byte Swapped)", 0, 32);
            (Registers.ShifterBuffer0HalfwordSwapped + offset).Define(owner).WithTag("SHIFTBUFHWS (Shifter Buffer Halfword Swapped)", 0, 32);
            (Registers.ShifterBuffer0NibbleSwapped + offset).Define(owner).WithTag("SHIFTBUFNIS (Shifter Buffer Nibble Swapped)", 0, 32);
            (Registers.ShifterBuffer0OddEvenSwapped + offset).Define(owner).WithTag("SHIFTBUFOES (Shifter Buffer Odd Even Swapped)", 0, 32);
            (Registers.ShifterBuffer0EvenOddSwapped + offset).Define(owner).WithTag("SHIFTBUFEOS (Shifter Buffer Even Odd Swapped)", 0, 32);
            (Registers.ShifterBuffer0HalfWordByteSwapped + offset).Define(owner).WithTag("SHIFTBUFHBS (Shifter Buffer Half Word Byte Swapped)", 0, 32);
        }

        private Shifter(IResourceBlockOwner owner, uint identifier, ResourceBlocksManager<Timer> timersManager,
            Interrupt status, Interrupt error,
            IValueRegisterField buffer,
            IValueRegisterField timerSelect, IEnumRegisterField<ShifterPolarity> timerPolarity, IEnumRegisterField<ShifterMode> mode,
            IValueRegisterField stopBit, IValueRegisterField startBit) : base(owner, identifier)
        {
            this.timersManager = timersManager;
            Status = status;
            Error = error;

            this.buffer = buffer;
            this.timerSelect = timerSelect;
            this.timerPolarity = timerPolarity;
            this.mode = mode;
            this.stopBit = stopBit;
            this.startBit = startBit;

            Status.MaskedFlagChanged += OnInterruptChange;
            Error.MaskedFlagChanged += OnInterruptChange;
        }

        private void OnControlOrConfigurationChange()
        {
            ControlOrConfigurationChanged?.Invoke();
        }

        private void OnModeChange(ShifterMode value)
        {
            if(value == ShifterMode.Transmit)
            {
                Status.SetFlag(true);
            }
        }

        private void OnBufferWrite(uint data)
        {
            if(mode.Value != ShifterMode.Transmit)
            {
                owner.Log(LogLevel.Warning, "Writing data (0x{0:X}) to the buffer of the shifter with ID {1} that isn't in the transmit mode", data, Identifier);
                return;
            }

            Status.SetFlag(false);
            DataTransmitted?.Invoke(data);
        }

        private void OnBufferRead()
        {
            if(mode.Value != ShifterMode.Receive)
            {
                owner.Log(LogLevel.Warning, "Reading data from the buffer of the shifter with ID {0} that isn't in the receive mode", Identifier);
                return;
            }

            // Discard read value from the buffer.
            if(receiveBuffer.Count > 0)
            {
                receiveBuffer.Dequeue();
            }
            RefreshBuffer();
        }

        private void RefreshBuffer()
        {
            var hasMoreData = receiveBuffer.Count > 0;
            if(hasMoreData)
            {
                buffer.Value = receiveBuffer.Peek();
            }
            Status.SetFlag(hasMoreData);
        }

        private readonly ResourceBlocksManager<Timer> timersManager;
        private readonly IValueRegisterField buffer;
        private readonly Queue<uint> receiveBuffer = new Queue<uint>();
        private readonly IValueRegisterField timerSelect;
        private readonly IEnumRegisterField<ShifterPolarity> timerPolarity;
        private readonly IEnumRegisterField<ShifterMode> mode;
        private readonly IValueRegisterField stopBit;
        private readonly IValueRegisterField startBit;
    }

    public enum ShifterPolarity
    {
        OnPosedge = 0,
        OnNegedge = 1
    }

    public enum ShifterMode
    {
        Disabled = 0b000,
        Receive = 0b001,
        Transmit = 0b010,
        Reserved = 0b011,
        MatchStore = 0b100,
        MatchContinuous = 0b101,
        State = 0b110,
        Logic = 0b111
    }
}
