//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.DMA
{
    public class ZynqMP_DMA : BasicDoubleWordPeripheral, IKnownSize
    {
        public ZynqMP_DMA(IMachine machine, long numberOfChannels = 8) : base(machine)
        {
            NumberOfChannels = numberOfChannels;
            channels = Misc.Iterate(() => new Channel(this, machine)).Take((int)NumberOfChannels).ToArray();
            Reset();
        }

        public override uint ReadDoubleWord(long offset)
        {
            var channelIndex = offset / ChannelSize;

            if(channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to read from unknown offset {0:X}", offset);
                return 0;
            }
            return channels[(int)channelIndex].ReadDoubleWord(offset % ChannelSize);
        }

        public override void WriteDoubleWord(long offset, uint value)
        {
            var channelIndex = offset / ChannelSize;
            if(channelIndex >= NumberOfChannels)
            {
                this.WarningLog("Trying to write to unknown offset {0:X}", offset);
                return;
            }
            channels[(int)channelIndex].WriteDoubleWord(offset % ChannelSize, value);
        }

        public long Size => ChannelSize * NumberOfChannels;

        public long ChannelSize = 0x10000;

        public readonly long NumberOfChannels;

        private T ReadStruct<T>(ulong address) where T : struct
        {
            var length = Packet.CalculateLength<T>();
            return Packet.Decode<T>(ReadBytes(address, length));
        }

        private byte[] ReadBytes(ulong address, int count)
        {
            return machine.GetSystemBus(this).ReadBytes(address, count, context: this);
        }

        private void WriteBytes(byte[] data, ulong address)
        {
            machine.GetSystemBus(this).WriteBytes(data, address, context: this);
        }

        private readonly Channel[] channels;

        public enum State
        {
            DoneNoError = 0b00,
            Paused = 0b01,
            Busy = 0b10,
            DoneError = 0b11,
        }

        public enum OperationMode
        {
            ScatterGather,
            Simple,
        }

        public enum Cmd
        {
            NextValid = 0,
            Pause = 1,
            Stop = 2,
        }

        private class Descriptor
        {
            public override string ToString() => this.ToDebugString();

            public ulong From;
            public ulong To;
            public ulong Size;
            public bool Int;
            public Cmd Type;
        }

        private class Channel : BasicDoubleWordPeripheral
        {
            public Channel(ZynqMP_DMA parent, IMachine mach) : base(mach)
            {
                Parent = parent;
                DefineRegisters();
            }

            public override void Reset()
            {
                base.Reset();
                sourceStartAddress = 0;
                destinationStartAddress = 0;
                IRQ.Unset();
            }

            public void StartTransaction()
            {
                if(state.Value == State.Busy)
                {
                    Parent.WarningLog("Transaction started while busy");
                    return;
                }

                state.Value = State.Busy;
                totalByteCount.Value = 0;
                nextSourceDescriptorAddress = sourceStartAddress;
                nextDestinationDescriptorAddress = destinationStartAddress;

                var descriptors = GetSGDescriptorsFromMemory().ToList();
                foreach(var d in descriptors)
                {
                    Transfer(d);
                    if(d.Type == Cmd.Pause)
                    {
                        state.Value = State.Paused;
                        dmaPause.Value = true;
                        UpdateInterrupts();
                        Parent.DebugLog("Transaction paused");
                        return;
                    }
                }

                state.Value = State.DoneNoError;
                dmaDone.Value = true;
                UpdateInterrupts();
            }

            public IEnumerable<Descriptor> GetSGDescriptorsFromMemory()
            {
                var descriptor = default(Descriptor);
                do
                {
                    var srcDescriptor = Parent.ReadStruct<DescriptorInMemory>(nextSourceDescriptorAddress);
                    var dstDescriptor = Parent.ReadStruct<DescriptorInMemory>(nextDestinationDescriptorAddress);
                    descriptor = new Descriptor
                    {
                        From = (ulong)srcDescriptor.AddressMSB << 8 * 4 | srcDescriptor.AddressLSB,
                        To = (ulong)dstDescriptor.AddressMSB << 8 * 4 | dstDescriptor.AddressLSB,
                        Size = srcDescriptor.Size,
                        Int = srcDescriptor.INTR,
                        Type = srcDescriptor.CMD,
                    };
                    nextSourceDescriptorAddress = (ulong)srcDescriptor.NextDescriptorAddrMSB << 32 | srcDescriptor.NextDescriptorAddrLSB;
                    nextDestinationDescriptorAddress = (ulong)dstDescriptor.NextDescriptorAddrMSB << 32 | dstDescriptor.NextDescriptorAddrLSB;
                    yield return descriptor;
                } while(descriptor.Type == Cmd.NextValid);
            }

            public ZynqMP_DMA Parent { get; }

            public GPIO IRQ { get; } = new GPIO();

            private void DefineRegisters()
            {
                Registers.ERR_CTRL.Define(this, 0x1)
                    .WithTaggedFlag("APB_ERR_RES", 0)
                    .WithReservedBits(1, 31)
                ;

                interruptStatus = Registers.CH_ISR.Define(this)
                    .WithFlag(0, out invalidApbAccess, FieldMode.Read | FieldMode.WriteOneToClear, name: "INV_APB")
                    .WithFlag(1, out sourceDescriptorDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "SRC_DSCR_DONE")
                    .WithFlag(2, out destinationDescriptorDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "DST_DSCR_DONE")
                    .WithFlag(3, out byteCountOverflow, FieldMode.Read | FieldMode.WriteOneToClear, name: "BYTE_CNT_OVRFL")
                    .WithFlag(4, out sourceAccountingOverflow, FieldMode.Read | FieldMode.WriteOneToClear, name: "IRQ_SRC_ACCT_ERR")
                    .WithFlag(5, out destinationAccountingOverflow, FieldMode.Read | FieldMode.WriteOneToClear, name: "IRQ_DST_ACCT_ERR")
                    .WithFlag(6, out sourceDescriptorFetchError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AXI_RD_SRC_DSCR")
                    .WithFlag(7, out destinationDescriptorFetchError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AXI_RD_DST_DSCR")
                    .WithFlag(8, out sourceDataReadError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AXI_RD_DATA")
                    .WithFlag(9, out destinationDataWriteError, FieldMode.Read | FieldMode.WriteOneToClear, name: "AXI_WR_DATA")
                    .WithFlag(10, out dmaDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMA_DONE")
                    .WithFlag(11, out dmaPause, FieldMode.Read | FieldMode.WriteOneToClear, name: "DMA_PAUSE")
                    .WithReservedBits(12, 20)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                ;

                // interruptMask stores which interrupts are *disabled*
                interruptMask = Registers.CH_IMR.Define(this, 0xFFF)
                    .WithFlag(0, out invalidApbAccessMask, FieldMode.Read, name: "INV_APB")
                    .WithFlag(1, out sourceDescriptorDoneMask, FieldMode.Read, name: "SRC_DSCR_DONE")
                    .WithFlag(2, out destinationDescriptorDoneMask, FieldMode.Read, name: "DST_DSCR_DONE")
                    .WithFlag(3, out byteCountOverflowMask, FieldMode.Read, name: "BYTE_CNT_OVRFL")
                    .WithFlag(4, out sourceAccountingOverflowMask, FieldMode.Read, name: "IRQ_SRC_ACCT_ERR")
                    .WithFlag(5, out destinationAccountingOverflowMask, FieldMode.Read, name: "IRQ_DST_ACCT_ERR")
                    .WithFlag(6, out sourceDescriptorFetchErrorMask, FieldMode.Read, name: "AXI_RD_SRC_DSCR")
                    .WithFlag(7, out destinationDescriptorFetchErrorMask, FieldMode.Read, name: "AXI_RD_DST_DSCR")
                    .WithFlag(8, out sourceDataReadErrorMask, FieldMode.Read, name: "AXI_RD_DATA")
                    .WithFlag(9, out destinationDataWriteErrorMask, FieldMode.Read, name: "AXI_WR_DATA")
                    .WithFlag(10, out dmaDoneMask, FieldMode.Read, name: "DMA_DONE")
                    .WithFlag(11, out dmaPauseMask, FieldMode.Read, name: "DMA_PAUSE")
                    .WithReservedBits(12, 20)
                ;

                Registers.CH_IEN.Define(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) invalidApbAccessMask.Value = false; }, name: "INV_APB")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDescriptorDoneMask.Value = false; }, name: "SRC_DSCR_DONE")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDescriptorDoneMask.Value = false; }, name: "DST_DSCR_DONE")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) byteCountOverflowMask.Value = false; }, name: "BYTE_CNT_OVRFL")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceAccountingOverflowMask.Value = false; }, name: "IRQ_SRC_ACCT_ERR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationAccountingOverflowMask.Value = false; }, name: "IRQ_DST_ACCT_ERR")
                    .WithFlag(6, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDescriptorFetchErrorMask.Value = false; }, name: "AXI_RD_SRC_DSCR")
                    .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDescriptorFetchErrorMask.Value = false; }, name: "AXI_RD_DST_DSCR")
                    .WithFlag(8, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDataReadErrorMask.Value = false; }, name: "AXI_RD_DATA")
                    .WithFlag(9, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDataWriteErrorMask.Value = false; }, name: "AXI_WR_DATA")
                    .WithFlag(10, FieldMode.Write, writeCallback: (_, val) => { if(val) dmaDoneMask.Value = false; }, name: "DMA_DONE")
                    .WithFlag(11, FieldMode.Write, writeCallback: (_, val) => { if(val) dmaPauseMask.Value = false; }, name: "DMA_PAUSE")
                    .WithReservedBits(12, 20)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                ;

                Registers.CH_IDS.Define(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) invalidApbAccessMask.Value = true; }, name: "INV_APB")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDescriptorDoneMask.Value = true; }, name: "SRC_DSCR_DONE")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDescriptorDoneMask.Value = true; }, name: "DST_DSCR_DONE")
                    .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) byteCountOverflowMask.Value = true; }, name: "BYTE_CNT_OVRFL")
                    .WithFlag(4, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceAccountingOverflowMask.Value = true; }, name: "IRQ_SRC_ACCT_ERR")
                    .WithFlag(5, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationAccountingOverflowMask.Value = true; }, name: "IRQ_DST_ACCT_ERR")
                    .WithFlag(6, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDescriptorFetchErrorMask.Value = true; }, name: "AXI_RD_SRC_DSCR")
                    .WithFlag(7, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDescriptorFetchErrorMask.Value = true; }, name: "AXI_RD_DST_DSCR")
                    .WithFlag(8, FieldMode.Write, writeCallback: (_, val) => { if(val) sourceDataReadErrorMask.Value = true; }, name: "AXI_RD_DATA")
                    .WithFlag(9, FieldMode.Write, writeCallback: (_, val) => { if(val) destinationDataWriteErrorMask.Value = true; }, name: "AXI_WR_DATA")
                    .WithFlag(10, FieldMode.Write, writeCallback: (_, val) => { if(val) dmaDoneMask.Value = true; }, name: "DMA_DONE")
                    .WithFlag(11, FieldMode.Write, writeCallback: (_, val) => { if(val) dmaPauseMask.Value = true; }, name: "DMA_PAUSE")
                    .WithReservedBits(12, 20)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                ;

                Registers.CH_CTRL0.Define(this, 0x80)
                    .WithReservedBits(0, 1)
                    .WithTaggedFlag("CONT", 1)
                    .WithTaggedFlag("CONT_ADDR", 2)
                    .WithTaggedFlag("RATE_CTRL", 3)
                    .WithTag("MODE", 4, 2)
                    .WithFlag(6, out pointType, writeCallback: (oldValue, value) =>
                        {
                            if(isEnabled.Value)
                            {
                                this.WarningLog("This field must remain stable while DMA Channel is enabled");
                                pointType.Value = oldValue;
                                return;
                            }
                            if(!value)
                            {
                                this.WarningLog("Only Scatter Gather mode is currently supported in this model");
                            }
                        }, valueProviderCallback: _ => pointType.Value, name: "POINT_TYPE")
                    .WithTaggedFlag("OVR_FETCH", 7)
                    .WithReservedBits(8, 24)
                ;

                Registers.CH_CTRL1.Define(this, 0x3FF)
                    .WithTag("SRC_ISSUE", 0, 5)
                    .WithTag("DST_ISSUE", 5, 5)
                    .WithReservedBits(10, 22)
                ;

                Registers.CH_FCI.Define(this)
                    .WithTaggedFlag("EN", 0)
                    .WithTaggedFlag("SIDE", 1)
                    .WithTag("PROG_CELL_CNT", 2, 2)
                    .WithReservedBits(4, 28)
                ;

                Registers.CH_SRC_START_LSB.Define(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => sourceStartAddress = sourceStartAddress.ReplaceBits(val, 32, destinationPosition: 0, sourcePosition: 0), name: "ADDR_LSB")
                ;

                Registers.CH_SRC_START_MSB.Define(this)
                    .WithValueField(0, 17, writeCallback: (_, val) => sourceStartAddress = sourceStartAddress.ReplaceBits(val, 17, destinationPosition: 32, sourcePosition: 0), name: "ADDR_MSB")
                    .WithReservedBits(17, 15)
                ;

                Registers.CH_DST_START_LSB.Define(this)
                    .WithValueField(0, 32, writeCallback: (_, val) => destinationStartAddress = destinationStartAddress.ReplaceBits(val, 32, destinationPosition: 0, sourcePosition: 0), name: "ADDR_LSB")
                ;

                Registers.CH_DST_START_MSB.Define(this)
                    .WithValueField(0, 17, writeCallback: (_, val) => destinationStartAddress = destinationStartAddress.ReplaceBits(val, 17, destinationPosition: 32, sourcePosition: 0), name: "ADDR_MSB")
                    .WithReservedBits(17, 15)
                ;

                Registers.CH_STATUS.Define(this)
                    .WithEnumField(0, 2, out state, FieldMode.Read, name: "STATE")
                    .WithReservedBits(2, 30)
                ;

                Registers.CH_DATA_ATTR.Define(this, 0x0483D20F)
                    .WithTag("AWLEN", 0, 4)
                    .WithTag("AWQOS", 4, 4)
                    .WithTag("AWCACHE", 8, 4)
                    .WithTag("AWBURST", 12, 2)
                    .WithTag("ARLEN", 14, 4)
                    .WithTag("ARQOS", 18, 4)
                    .WithTag("ARCACHE", 22, 4)
                    .WithTag("ARBURST", 26, 2)
                    .WithReservedBits(28, 4)
                ;

                Registers.CH_DSCR_ATTR.Define(this)
                    .WithTag("AXQOS", 0, 4)
                    .WithTag("AXCACHE", 4, 4)
                    .WithTaggedFlag("AXCOHRNT", 8)
                    .WithReservedBits(9, 23)
                ;

                Registers.CH_TOTAL_BYTE.Define(this)
                    .WithValueField(0, 32, out totalByteCount, name: "CNT")
                ;

                Registers.CH_IRQ_SRC_ACCT.Define(this)
                    .WithTag("CNT", 0, 8)
                    .WithReservedBits(8, 24)
                ;

                Registers.CH_IRQ_DST_ACCT.Define(this)
                    .WithTag("CNT", 0, 8)
                    .WithReservedBits(8, 24)
                ;

                Registers.CH_CTRL2.Define(this)
                    .WithFlag(0, out isEnabled, writeCallback: (_, value) =>
                        {
                            if(value)
                            {
                                if(state.Value == State.Busy)
                                {
                                    this.WarningLog("Channel has been enabled, but it's busy");
                                    return;
                                }
                                if(pointType.Value) // SG Mode
                                {
                                    StartTransaction();
                                }
                                else
                                {
                                    this.WarningLog("Simple mode enabled, but not implemented");
                                }
                            }
                            else
                            {
                                if(state.Value == State.Paused)
                                {
                                    state.Value = State.DoneNoError;
                                }
                            }
                        }, name: "EN")
                    .WithReservedBits(1, 31);
            }

            private void UpdateInterrupts()
            {
                // interruptMask stores which interrupts are *disabled*
                var irq = (interruptStatus.Value & ~interruptMask.Value) != 0;
                this.DebugLog("Set IRQ to {0}", irq);
                IRQ.Set(irq);
            }

            private void Transfer(Descriptor descriptor)
            {
                var data = Parent.ReadBytes(descriptor.From, (int)descriptor.Size);
                Parent.WriteBytes(data, descriptor.To);

                totalByteCount.Value += (uint)descriptor.Size;

                sourceDescriptorDone.Value = true;
                destinationDescriptorDone.Value = true;

                if(descriptor.Int)
                {
                    dmaDone.Value = true;
                }

                UpdateInterrupts();
            }

            private ulong nextSourceDescriptorAddress;
            private ulong nextDestinationDescriptorAddress;

            private ulong sourceStartAddress;
            private ulong destinationStartAddress;

            private DoubleWordRegister interruptStatus;
            private DoubleWordRegister interruptMask;

            private IEnumRegisterField<State> state;
            private IFlagRegisterField isEnabled;
            private IFlagRegisterField pointType;
            private IValueRegisterField totalByteCount;
            private IFlagRegisterField invalidApbAccess;
            private IFlagRegisterField sourceDescriptorDone;
            private IFlagRegisterField destinationDescriptorDone;
            private IFlagRegisterField byteCountOverflow;
            private IFlagRegisterField sourceAccountingOverflow;
            private IFlagRegisterField destinationAccountingOverflow;
            private IFlagRegisterField sourceDescriptorFetchError;
            private IFlagRegisterField destinationDescriptorFetchError;
            private IFlagRegisterField sourceDataReadError;
            private IFlagRegisterField destinationDataWriteError;
            private IFlagRegisterField dmaDone;
            private IFlagRegisterField dmaPause;
            private IFlagRegisterField invalidApbAccessMask;
            private IFlagRegisterField sourceDescriptorDoneMask;
            private IFlagRegisterField destinationDescriptorDoneMask;
            private IFlagRegisterField byteCountOverflowMask;
            private IFlagRegisterField sourceAccountingOverflowMask;
            private IFlagRegisterField destinationAccountingOverflowMask;
            private IFlagRegisterField sourceDescriptorFetchErrorMask;
            private IFlagRegisterField destinationDescriptorFetchErrorMask;
            private IFlagRegisterField sourceDataReadErrorMask;
            private IFlagRegisterField destinationDataWriteErrorMask;
            private IFlagRegisterField dmaDoneMask;
            private IFlagRegisterField dmaPauseMask;
        }

        [LeastSignificantByteFirst]
        private struct DescriptorInMemory
        {
            public override string ToString() => this.ToDebugString();

#pragma warning disable 649
            [PacketField, Offset(doubleWords: 0), Width(bits: 32)]
            public uint AddressLSB;
            [PacketField, Offset(doubleWords: 1), Width(bits: 12)]
            public uint AddressMSB;
            [PacketField, Offset(doubleWords: 2), Width(bits: 30)]
            public uint Size;
            [PacketField, Offset(doubleWords: 3), Width(bits: 1)]
            public bool Coherency;

            [PacketField, Offset(doubleWords: 3, bits: 1), Width(bits: 1)]
            public bool DSCR; // BRESP
            [PacketField, Offset(doubleWords: 3, bits: 2), Width(bits: 1)]
            public bool INTR;
            [PacketField, Offset(doubleWords: 3, bits: 3), Width(bits: 2)]
            public Cmd CMD;
            [PacketField, Offset(doubleWords: 4), Width(bits: 32)]
            public uint NextDescriptorAddrLSB;
            [PacketField, Offset(doubleWords: 5), Width(bits: 12)]
            public uint NextDescriptorAddrMSB;
#pragma warning restore 649
        }

        private enum Registers : long
        {
            ERR_CTRL = 0x0000000000,
            CH_ISR = 0x0000000100,
            CH_IMR = 0x0000000104,
            CH_IEN = 0x0000000108,
            CH_IDS = 0x000000010C,
            CH_CTRL0 = 0x0000000110,
            CH_CTRL1 = 0x0000000114,
            CH_FCI = 0x0000000118,
            CH_STATUS = 0x000000011C,
            CH_DATA_ATTR = 0x0000000120,
            CH_DSCR_ATTR = 0x0000000124,
            CH_SRC_DSCR_WORD0 = 0x0000000128,
            CH_SRC_DSCR_WORD1 = 0x000000012C,
            CH_SRC_DSCR_WORD2 = 0x0000000130,
            CH_SRC_DSCR_WORD3 = 0x0000000134,
            CH_DST_DSCR_WORD0 = 0x0000000138,
            CH_DST_DSCR_WORD1 = 0x000000013C,
            CH_DST_DSCR_WORD2 = 0x0000000140,
            CH_DST_DSCR_WORD3 = 0x0000000144,
            CH_WR_ONLY_WORD0 = 0x0000000148,
            CH_WR_ONLY_WORD1 = 0x000000014C,
            CH_WR_ONLY_WORD2 = 0x0000000150,
            CH_WR_ONLY_WORD3 = 0x0000000154,
            CH_SRC_START_LSB = 0x0000000158,
            CH_SRC_START_MSB = 0x000000015C,
            CH_DST_START_LSB = 0x0000000160,
            CH_DST_START_MSB = 0x0000000164,
            CH_TOTAL_BYTE = 0x0000000188,
            CH_RATE_CTRL = 0x000000018C,
            CH_IRQ_SRC_ACCT = 0x0000000190,
            CH_IRQ_DST_ACCT = 0x0000000194,
            CH_CTRL2 = 0x0000000200,
        }
    }
}
