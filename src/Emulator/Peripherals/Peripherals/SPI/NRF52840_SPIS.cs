//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.SPI
{
  public class NRF52840_SPIS : IDoubleWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IKnownSize, ISPIPeripheral
  {
    public NRF52840_SPIS(IMachine machine)
    {
      this.machine = machine;
      sysbus = machine.GetSystemBus(this);

      IRQ = new GPIO();

      receiveFifo = new Queue<byte>();
      transmitFifo = new Queue<byte>();
      RegistersCollection = new DoubleWordRegisterCollection(this);
      DefineRegisters();
      Reset();
    }

    public void Reset()
    {
      receiveFifo.Clear();
      transmitFifo.Clear();
      enabled = false;
      acquired = false;
      RegistersCollection.Reset();
      UpdateInterrupts();
    }

    public uint ReadDoubleWord(long offset)
    {
      return RegistersCollection.Read(offset);
    }

    public void WriteDoubleWord(long offset, uint value)
    {
      RegistersCollection.Write(offset, value);
    }

    // ISPIPeripheral implementation
    public byte Transmit(byte data)
    {
      if (!enabled) // Temporarily not checking acquired flag
      {
        this.Log(LogLevel.Warning, "Received SPI data but SPIS is not enabled");
        return 0xFF; // Return default value
      }

      // Store received data directly to device memory using EasyDMA
      if (rxDataPointer.Value != 0 && rxMaxDataCount.Value > 0)
      {
        var currentAmount = rxTransferredAmount.Value;
        if (currentAmount < rxMaxDataCount.Value)
        {
          // Write byte directly to device memory at RXD.PTR + current amount
          var memoryAddress = rxDataPointer.Value + currentAmount;
          sysbus.WriteByte(memoryAddress, data);
          this.Log(LogLevel.Noisy, "SPIS received byte: 0x{0:X} -> memory[0x{1:X}]", data, memoryAddress);

          // Update RX transferred amount
          rxTransferredAmount.Value = currentAmount + 1;

          // Check if we've reached MAXCNT for RX
          if (rxTransferredAmount.Value >= rxMaxDataCount.Value)
          {
            this.Log(LogLevel.Debug, "SPIS received MAXCNT bytes ({0}), triggering END interrupt", rxMaxDataCount.Value);
            this.Log(LogLevel.Debug, "Data: {0}", BitConverter.ToString(sysbus.ReadBytes(rxDataPointer.Value, (int)rxMaxDataCount.Value)).Replace("-", ""));
            endRxPending.Value = true;
            endPending.Value = true; // END event when MAXCNT is reached
            UpdateInterrupts();
          }
          else
          {
            // Just update interrupts for partial reception - no END yet
            UpdateInterrupts();
          }
        }
        else
        {
          this.Log(LogLevel.Warning, "SPIS receive buffer full - MAXCNT ({0}) reached", rxMaxDataCount.Value);
          overflow.Value = true;
        }
      }
      else
      {
        // No RX buffer configured - just store in local FIFO for compatibility
        lock (receiveFifo)
        {
          if (receiveFifo.Count < ReceiveBufferSize)
          {
            receiveFifo.Enqueue(data);
            this.Log(LogLevel.Debug, "SPIS received byte: 0x{0:X} (no DMA buffer configured)", data);
          }
          else
          {
            this.Log(LogLevel.Warning, "SPIS receive FIFO overflow");
            overflow.Value = true;
          }
        }
      }

      // Return transmit data from device memory using EasyDMA
      byte result = 0xFF; // Default value when no data to transmit

      if (txDataPointer.Value != 0 && txMaxDataCount.Value > 0)
      {
        var currentTxAmount = txTransferredAmount.Value;
        if (currentTxAmount < txMaxDataCount.Value)
        {
          // Read byte from device memory at TXD.PTR + current amount
          var memoryAddress = txDataPointer.Value + currentTxAmount;
          result = sysbus.ReadByte(memoryAddress);
          txTransferredAmount.Value = currentTxAmount + 1;
          this.Log(LogLevel.Noisy, "SPIS transmitted byte: 0x{0:X} from memory[0x{1:X}]", result, memoryAddress);

          // Update semaphore - more data available if we haven't reached MAXCNT
          txdSemaphore.Value = txTransferredAmount.Value < txMaxDataCount.Value;
        }
        else
        {
          this.Log(LogLevel.Debug, "SPIS TX MAXCNT reached, sending ORC byte 0x{0:X}", orcByte.Value);
          result = (byte)orcByte.Value;
          overread.Value = true;
        }
      }
      else
      {
        // No TX buffer configured - check local FIFO for compatibility
        lock (transmitFifo)
        {
          if (transmitFifo.Count > 0)
          {
            result = transmitFifo.Dequeue();
            this.Log(LogLevel.Noisy, "SPIS transmitted byte from FIFO: 0x{0:X}", result);
            txdSemaphore.Value = transmitFifo.Count > 0;
          }
          else
          {
            this.Log(LogLevel.Debug, "SPIS no data to transmit, sending ORC byte 0x{0:X}", orcByte.Value);
            result = (byte)orcByte.Value;
            overread.Value = true;
          }
        }
      }

      return result;
    }

    public void FinishTransmission()
    {
      if (!enabled) // Temporarily not checking acquired flag
      {
        return;
      }

      this.Log(LogLevel.Debug, "SPIS transmission finished");

      // Update TX transferred amount based on how much we actually sent
      if (txMaxDataCount.Value > 0)
      {
        var sentBytes = Math.Min((long)txMaxDataCount.Value, (long)txMaxDataCount.Value - transmitFifo.Count);
        txTransferredAmount.Value = (uint)sentBytes;
        this.Log(LogLevel.Debug, "SPIS TX transferred amount: {0}", sentBytes);
      }

      // Generate END event if not already set by RX MAXCNT
      if (!endPending.Value)
      {
        this.Log(LogLevel.Debug, "SPIS setting END event on transmission finish");
        endPending.Value = true;
      }

      // Auto-release if configured
      if (shortEndAcquire.Value)
      {
        this.Log(LogLevel.Debug, "SPIS short end enabled, release and reacquire");
        Release();
        Acquire();
      }
      else
      {
        this.Log(LogLevel.Debug, "SPIS short end disabled, release");
        Release();
      }

      UpdateInterrupts();
    }

    public GPIO IRQ { get; }

    public DoubleWordRegisterCollection RegistersCollection { get; }

    public long Size => 0x1000;

    private void DefineRegisters()
    {
      Registers.TasksAcquire.Define(this)
          .WithFlag(0, FieldMode.Write, name: "TASKS_ACQUIRE", writeCallback: (_, val) =>
          {
            if (val)
            {
              Acquire();
            }
          })
          .WithReservedBits(1, 31)
      ;

      Registers.TasksRelease.Define(this)
          .WithFlag(0, FieldMode.Write, name: "TASKS_RELEASE", writeCallback: (_, val) =>
          {
            if (val)
            {
              this.Log(LogLevel.Debug, "SPIS tasks release");
              Release();
            }
          })
          .WithReservedBits(1, 31)
      ;

      Registers.EventsEnd.Define(this)
          .WithFlag(0, out endPending, name: "EVENTS_END")
          .WithReservedBits(1, 31)
          .WithWriteCallback((_, __) => UpdateInterrupts())
      ;

      Registers.EventsEndrx.Define(this)
          .WithFlag(0, out endRxPending, name: "EVENTS_ENDRX")
          .WithReservedBits(1, 31)
          .WithWriteCallback((_, __) => UpdateInterrupts())
      ;

      Registers.EventsAcquired.Define(this)
          .WithFlag(0, out acquiredPending, name: "EVENTS_ACQUIRED")
          .WithReservedBits(1, 31)
          .WithWriteCallback((_, __) => UpdateInterrupts())
      ;

      Registers.Shorts.Define(this)
          .WithFlag(2, out shortEndAcquire, name: "END_ACQUIRE")
          .WithReservedBits(0, 2)
          .WithReservedBits(3, 29)
      ;

      Registers.EnableInterrupt.Define(this)
    .WithReservedBits(0, 1)
    .WithFlag(1, out endEnabled, FieldMode.Read | FieldMode.Set, name: "END", writeCallback: (_, val) =>
    {
      this.Log(LogLevel.Debug, "SPIS EnableInterrupt END set to {0}", val);
    })
    .WithReservedBits(2, 2)
    .WithFlag(4, out endRxEnabled, FieldMode.Read | FieldMode.Set, name: "ENDRX", writeCallback: (_, val) =>
    {
      this.Log(LogLevel.Debug, "SPIS EnableInterrupt ENDRX set to {0}", val);
    })
    .WithReservedBits(5, 5)
    .WithFlag(10, out acquiredEnabled, FieldMode.Read | FieldMode.Set, name: "ACQUIRED", writeCallback: (_, val) =>
    {
      this.Log(LogLevel.Debug, "SPIS EnableInterrupt ACQUIRED set to {0}", val);
    })
    .WithReservedBits(11, 21)
    .WithWriteCallback((_, __) => UpdateInterrupts())
;

      Registers.DisableInterrupt.Define(this)
    .WithReservedBits(0, 1)
    .WithFlag(1, name: "END",
        valueProviderCallback: _ => endEnabled.Value,
        writeCallback: (_, val) =>
        {
          this.Log(LogLevel.Debug, "SPIS DisableInterrupt END set to {0}", val);
          if (val) endEnabled.Value = false;
        })
    .WithReservedBits(2, 2)
    .WithFlag(4, name: "ENDRX",
        valueProviderCallback: _ => endRxEnabled.Value,
        writeCallback: (_, val) =>
        {
          this.Log(LogLevel.Debug, "SPIS DisableInterrupt ENDRX set to {0}", val);
          if (val) endRxEnabled.Value = false;
        })
    .WithReservedBits(5, 5)
    .WithFlag(10, name: "ACQUIRED",
        valueProviderCallback: _ => acquiredEnabled.Value,
        writeCallback: (_, val) =>
        {
          this.Log(LogLevel.Debug, "SPIS DisableInterrupt ACQUIRED set to {0}", val);
          if (val) acquiredEnabled.Value = false;
        })
    .WithReservedBits(11, 21)
    .WithWriteCallback((_, __) => UpdateInterrupts())
;

      Registers.Enable.Define(this)
          .WithValueField(0, 4,
              valueProviderCallback: _ => enabled ? 2u : 0u, // SPIS enable value is 2
              writeCallback: (_, val) =>
              {
                switch (val)
                {
                  case 0:
                    // disabled
                    enabled = false;
                    acquired = false;
                    this.Log(LogLevel.Debug, "SPIS disabled");
                    break;

                  case 2:
                    // enabled, SPIS slave mode
                    enabled = true;
                    this.Log(LogLevel.Debug, "SPIS enabled");
                    break;

                  default:
                    this.Log(LogLevel.Warning, "Unhandled SPIS enable value: 0x{0:X}", val);
                    return;
                }

                UpdateInterrupts();
              })
          .WithReservedBits(4, 28)
      ;

      Registers.PinSelectSCK.Define(this, resetValue: 0xFFFFFFFF)
          .WithValueField(0, 5, name: "PIN", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectSCK PIN set to {0}", value);
          })
          .WithValueField(5, 1, name: "PORT", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectSCK PORT set to {0}", value);
          })
          .WithIgnoredBits(6, 25)
          .WithFlag(31, name: "CONNECT", writeCallback: (_, value) =>
          {
            var connectionStr = value ? "Disconnected" : "Connected";
            this.Log(LogLevel.Noisy, "SPIS PinSelectSCK {0}", connectionStr);
          })
      ;

      Registers.PinSelectMISO.Define(this, resetValue: 0xFFFFFFFF)
          .WithValueField(0, 5, name: "PIN", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectMISO PIN set to {0}", value);
          })
          .WithValueField(5, 1, name: "PORT", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectMISO PORT set to {0}", value);
          })
          .WithIgnoredBits(6, 25)
          .WithFlag(31, name: "CONNECT", writeCallback: (_, value) =>
          {
            var connectionStr = value ? "Disconnected" : "Connected";
            this.Log(LogLevel.Noisy, "SPIS PinSelectMISO {0}", connectionStr);
          })
      ;

      Registers.PinSelectMOSI.Define(this, resetValue: 0xFFFFFFFF)
          .WithValueField(0, 5, name: "PIN", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectMOSI PIN set to {0}", value);
          })
          .WithValueField(5, 1, name: "PORT", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectMOSI PORT set to {0}", value);
          })
          .WithIgnoredBits(6, 25)
          .WithFlag(31, name: "CONNECT", writeCallback: (_, value) =>
          {
            var connectionStr = value ? "Disconnected" : "Connected";
            this.Log(LogLevel.Noisy, "SPIS PinSelectMOSI {0}", connectionStr);
          })
      ;

      Registers.PinSelectCSN.Define(this, resetValue: 0xFFFFFFFF)
          .WithValueField(0, 5, name: "PIN", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectCSN PIN set to {0}", value);
          })
          .WithValueField(5, 1, name: "PORT", writeCallback: (_, value) =>
          {
            this.Log(LogLevel.Noisy, "SPIS PinSelectCSN PORT set to {0}", value);
          })
          .WithIgnoredBits(6, 25)
          .WithFlag(31, name: "CONNECT", writeCallback: (_, value) =>
          {
            var connectionStr = value ? "Disconnected" : "Connected";
            this.Log(LogLevel.Noisy, "SPIS PinSelectCSN {0}", connectionStr);
          })
      ;

      Registers.RxdPtr.Define(this)
          .WithValueField(0, 32, out rxDataPointer, name: "PTR")
      ;

      Registers.RxdMaxcnt.Define(this)
          .WithValueField(0, 16, out rxMaxDataCount, name: "MAXCNT")
          .WithReservedBits(16, 16)
      ;

      Registers.RxdAmount.Define(this)
          .WithValueField(0, 16, out rxTransferredAmount, FieldMode.Read, name: "AMOUNT")
          .WithReservedBits(16, 16)
      ;

      Registers.TxdPtr.Define(this)
          .WithValueField(0, 32, out txDataPointer, name: "PTR")
      ;

      Registers.TxdMaxcnt.Define(this)
          .WithValueField(0, 16, out txMaxDataCount, name: "MAXCNT")
          .WithReservedBits(16, 16)
      ;

      Registers.TxdAmount.Define(this)
          .WithValueField(0, 16, out txTransferredAmount, FieldMode.Read, name: "AMOUNT")
          .WithReservedBits(16, 16)
      ;

      Registers.Config.Define(this)
          .WithFlag(0, name: "ORDER", writeCallback: (_, value) =>
          {
            var orderStr = value ? "LSB first" : "MSB first";
            this.Log(LogLevel.Debug, "SPIS bit order set to {0}", orderStr);
          })
          .WithFlag(1, name: "CPHA", writeCallback: (_, value) =>
          {
            var phaseStr = value ? "Sample on trailing edge" : "Sample on leading edge";
            this.Log(LogLevel.Debug, "SPIS clock phase set to {0}", phaseStr);
          })
          .WithFlag(2, name: "CPOL", writeCallback: (_, value) =>
          {
            var polarityStr = value ? "Active low" : "Active high";
            this.Log(LogLevel.Debug, "SPIS clock polarity set to {0}", polarityStr);
          })
          .WithReservedBits(3, 29)
      ;

      Registers.Def.Define(this, resetValue: 0xFF)
          .WithValueField(0, 8, name: "DEF")
          .WithReservedBits(8, 24)
      ;

      Registers.Orc.Define(this)
          .WithValueField(0, 8, out orcByte, name: "ORC")
          .WithReservedBits(8, 24)
      ;

      Registers.Status.Define(this)
          .WithFlag(0, out overread, FieldMode.Read, name: "OVERREAD")
          .WithFlag(1, out overflow, FieldMode.Read, name: "OVERFLOW")
          .WithReservedBits(2, 30)
      ;

      Registers.TxdSemaphore.Define(this)
          .WithFlag(0, out txdSemaphore, FieldMode.Read, name: "SEMAPHORE")
          .WithReservedBits(1, 31)
      ;
    }

    private void Acquire()
    {
      if (!enabled)
      {
        this.Log(LogLevel.Warning, "SPIS acquire called but not enabled");
        return;
      }

      this.Log(LogLevel.Debug, "SPIS acquired");
      acquired = true;
      acquiredPending.Value = true;
      UpdateInterrupts();
    }

    private void Release()
    {
      if (!enabled)
      {
        this.Log(LogLevel.Warning, "SPIS release called but not enabled");
        return;
      }

      this.Log(LogLevel.Debug, "SPIS released");
      acquired = false;

      // Reset transfer counters for new transfer
      rxTransferredAmount.Value = 0;
      txTransferredAmount.Value = 0;

      // Clear status flags
      overflow.Value = false;
      overread.Value = false;

      // Clear semaphore
      txdSemaphore.Value = false;

      UpdateInterrupts();
    }

    private void UpdateInterrupts()
    {
      var status = false;

      status |= endPending.Value && endEnabled.Value;
      status |= endRxPending.Value && endRxEnabled.Value;
      status |= acquiredPending.Value && acquiredEnabled.Value;

      status &= enabled;

      this.Log(LogLevel.Noisy, "SPIS interrupt state: status={0}, endPending={1}, endRxPending={2}, acquiredPending={3}, endEnabled={4}, endRxEnabled={5}, acquiredEnabled={6}, enabled={7}",
          status,
          endPending.Value,
          endRxPending.Value,
          acquiredPending.Value,
          endEnabled.Value,
          endRxEnabled.Value,
          acquiredEnabled.Value,
          enabled);

      this.Log(LogLevel.Noisy, "SPIS setting IRQ to {0}", status);
      IRQ.Set(status);
    }

    // Register fields
    private IFlagRegisterField endPending;
    private IFlagRegisterField endRxPending;
    private IFlagRegisterField acquiredPending;

    private IFlagRegisterField endEnabled;
    private IFlagRegisterField endRxEnabled;
    private IFlagRegisterField acquiredEnabled;

    private IFlagRegisterField shortEndAcquire;
    private IFlagRegisterField overread;
    private IFlagRegisterField overflow;
    private IFlagRegisterField txdSemaphore;

    private IValueRegisterField rxDataPointer;
    private IValueRegisterField rxMaxDataCount;
    private IValueRegisterField rxTransferredAmount;
    private IValueRegisterField txDataPointer;
    private IValueRegisterField txMaxDataCount;
    private IValueRegisterField txTransferredAmount;
    private IValueRegisterField orcByte;

    private bool enabled;
    private bool acquired;

    private readonly Queue<byte> receiveFifo;
    private readonly Queue<byte> transmitFifo;
    private readonly IMachine machine;
    private readonly IBusController sysbus;

    private const int ReceiveBufferSize = 256; // SPIS buffer size

    private enum Registers
    {
      TasksAcquire = 0x24,
      TasksRelease = 0x28,
      EventsEnd = 0x104,
      EventsEndrx = 0x110,
      EventsAcquired = 0x128,
      Shorts = 0x200,
      EnableInterrupt = 0x304,
      DisableInterrupt = 0x308,
      Status = 0x440,
      Enable = 0x500,
      PinSelectSCK = 0x508,
      PinSelectMISO = 0x50C,
      PinSelectMOSI = 0x510,
      PinSelectCSN = 0x514,
      RxdPtr = 0x534,
      RxdMaxcnt = 0x538,
      RxdAmount = 0x53C,
      TxdPtr = 0x544,
      TxdMaxcnt = 0x548,
      TxdAmount = 0x54C,
      Config = 0x554,
      Def = 0x55C,
      Orc = 0x5C0,
      TxdSemaphore = 0x680,
    }
  }
}
