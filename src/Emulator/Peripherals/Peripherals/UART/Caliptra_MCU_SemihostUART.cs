//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.UART;

[AllowedTranslations(AllowedTranslation.DoubleWordToByte | AllowedTranslation.WordToByte)]
// This mirrors the implementation of UART in "caliptra-mcu-sw/emulator/periph/src/uart.rs"
public class Caliptra_MCU_SemihostUART : UARTBase, IUART, IKnownSize, IBytePeripheral, IProvidesRegisterCollection<ByteRegisterCollection>
{
    public Caliptra_MCU_SemihostUART(IMachine machine) : base(machine)
    {
        DefineRegisters();
        Reset();
    }

    public override void Reset()
    {
        RegistersCollection.Reset();
        base.Reset();
        UpdateInterrupts();
    }

    public void WriteByte(long offset, byte value)
    {
        RegistersCollection.Write(offset, value);
    }

    public byte ReadByte(long offset)
    {
        return RegistersCollection.Read(offset);
    }

    public override Bits StopBits => stopBits.Value == 0 ? Bits.None : Bits.One;

    public override Parity ParityBit => Parity.None;

    // Immutable, but this is a semihost
    public override uint BaudRate => (uint)baudRate.Value;

    public GPIO IRQ { get; private set; } = new();

    public long Size => 0x100;

    public ByteRegisterCollection RegistersCollection { get; private set; }

    protected override void CharWritten()
    {
        UpdateInterrupts();
    }

    protected override void QueueEmptied()
    {
        UpdateInterrupts();
    }

    private void UpdateInterrupts()
    {
        IRQ.Set(base.Count > 0);
    }

    private void DefineRegisters()
    {
        RegistersCollection = new ByteRegisterCollection(this);

        Registers.BitRate.Define(this, 0)
            .WithValueField(0, 8, out baudRate, name: "Bit Rate");

        Registers.DataBits.Define(this, 8)
            .WithTag("Data Bits", 0, 8);

        Registers.StopBits.Define(this, 1)
            .WithValueField(0, 8, out stopBits, name: "Stop Bits");

        Registers.TransmitStatus.Define(this)
            .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true, name: "Transmit Status")
            .WithReservedBits(1, 7);

        Registers.TransmitData.Define(this)
            .WithValueField(0, 8,
                valueProviderCallback: _ =>
                {
                    if(!TryGetCharacter(out var result))
                    {
                        // A valid path of program execution, where it uses to check if it has read everything
                        // Since it's a "semihost" UART, it might not need to differentiate between end of stream and data in the queue being 0x0
                        this.NoisyLog("Reading from empty data queue");
                        return 0;
                    }
                    this.NoisyLog("Reading 0x{0:X} from the receive register", result);

                    return result;
                },
                writeCallback: (_, val) =>
                {
                    TransmitCharacter((byte)val);
                },
            name: "Transmit Data");
    }

    private IValueRegisterField baudRate;
    private IValueRegisterField stopBits;

    private enum Registers
    {
        // The gaps in the address space are expected
        BitRate = 0x10,
        DataBits = 0x11,
        StopBits = 0x12,
        TransmitStatus = 0x40,
        TransmitData = 0x41,
    }
}
