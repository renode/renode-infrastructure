//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

using Antmicro.Migrant;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class S32K3XX_FlexIO_UART : IUART, IUART<ushort>, IUART<uint>, IEndpoint
    {
        public S32K3XX_FlexIO_UART(uint? rxShifterId = null, uint? txShifterId = null)
        {
            this.rxShifterId = rxShifterId;
            this.txShifterId = txShifterId;
        }

        public void RegisterInFlexIO(S32K3XX_FlexIO flexIO)
        {
            this.flexIO = flexIO;
            if(!rxShifterId.HasValue && !txShifterId.HasValue)
            {
                this.Log(LogLevel.Warning, "The endpoint doesn't have set any shifter identifier");
                return;
            }

            var errors = new List<string>();

            if(TryReserveShifter(flexIO, rxShifterId, out var shifter, errors, nameof(rxShifterId)))
            {
                receiver = new UARTReceiver(this, shifter);
            }

            if(TryReserveShifter(flexIO, txShifterId, out shifter, errors, nameof(txShifterId)))
            {
                transmitter = new UARTTransmitter(this, shifter);
                transmitter.CharReceived += val => CharReceived?.Invoke(val);
                transmitter.WordReceived += val => WordCharReceived?.Invoke(val);
                transmitter.DoubleWordReceived += val => DoubleWordCharReceived?.Invoke(val);
            }

            if(errors.Count > 0)
            {
                throw new ConstructionException(String.Join(" ", errors));
            }
        }

        public void Reset()
        {
            receiver?.Reset();
            transmitter?.Reset();
        }

        public void WriteChar(byte value)
        {
            if(receiver == null)
            {
                this.Log(LogLevel.Warning, "The UART doesn't support receiving, no shifter set");
                return;
            }
            receiver.WriteChar(value);
        }

        public void WriteChar(ushort value)
        {
            if(receiver == null)
            {
                this.Log(LogLevel.Warning, "The UART doesn't support receiving, no shifter set");
                return;
            }
            receiver.WriteChar(value);
        }

        public void WriteChar(uint value)
        {
            if(receiver == null)
            {
                this.Log(LogLevel.Warning, "The UART doesn't support receiving, no shifter set");
                return;
            }
            receiver.WriteChar(value);
        }

        public uint BaudRate => LogWarningWhenDirectionsDiffer(GetBaudRate(receiver), GetBaudRate(transmitter), "BaudRate") ?? 0;

        public Bits StopBits => LogWarningWhenDirectionsDiffer(receiver?.StopBits, transmitter?.StopBits, "StopBits") ?? Bits.None;

        public Parity ParityBit => Parity.None;

        [field: Transient]
        public event Action<byte> CharReceived;

        [event: Transient]
        event Action<ushort> IUART<ushort>.CharReceived
        {
            add => WordCharReceived += value;
            remove => WordCharReceived -= value;
        }

        [event: Transient]
        event Action<uint> IUART<uint>.CharReceived
        {
            add => DoubleWordCharReceived += value;
            remove => DoubleWordCharReceived -= value;
        }

        private bool TryReserveShifter(S32K3XX_FlexIO flexIO, uint? id, out Shifter shifter, IList<string> errors, string parameterName)
        {
            if(!id.HasValue)
            {
                shifter = null;
                return false;
            }

            if(!flexIO.ShiftersManager.Reserve(this, id.Value, out shifter))
            {
                errors.Add($"Invalid {parameterName}, unable to reserve shifter with the {id.Value} identifier.");
                return false;
            }

            return true;
        }

        private uint? GetBaudRate(UARTDirectionBase uart)
        {
            if(uart == null)
            {
                return null;
            }
            return flexIO.Frequency / uart.BaudRateDivider;
        }

        private T LogWarningWhenDirectionsDiffer<T>(T receiverProperty, T transmitterProperty, string propertyName)
        {
            if(receiverProperty != null && transmitterProperty != null && !receiverProperty.Equals(transmitterProperty))
            {
                this.Log(LogLevel.Warning, "The {0} property is different for the receiver ({1}) and transmitter ({2}) sides", propertyName, receiverProperty, transmitterProperty);
            }
            return receiverProperty != null ? receiverProperty : transmitterProperty;
        }

        private S32K3XX_FlexIO flexIO;
        private UARTReceiver receiver;
        private UARTTransmitter transmitter;

        private readonly uint? rxShifterId;
        private readonly uint? txShifterId;

        private event Action<ushort> WordCharReceived;

        private event Action<uint> DoubleWordCharReceived;
    }
}
