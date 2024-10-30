//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.CRC
{
    public class SAM4S_CRCCU : BasicDoubleWordPeripheral, IKnownSize
    {
        public SAM4S_CRCCU(IMachine machine) : base(machine)
        {
            IRQ = new GPIO();
            DefineRegisters();
        }

        public override void Reset()
        {
            base.Reset();
            UpdateInterrupts();
        }

        public long Size => 0x100;
        public GPIO IRQ { get; }

        private void DefineRegisters()
        {
           Registers.DescriptorBase.Define(this)
               .WithReservedBits(0, 9) 
               .WithValueField(9, 23,
                       out descriptorAddress,
                       name: "DSCR");        // Descriptor Base Address
           Registers.DMAEnable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { if (value) { ComputeCRC(); } },
                    name: "DMAEN")           // DMA Enable bit
               .WithReservedBits(1, 31);
           Registers.DMADisable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    name: "DMADIS")          // DMA Disable bit
               .WithReservedBits(1, 31);
           Registers.DMAStatus.Define(this)
               .WithFlag(0, FieldMode.Read,
                       valueProviderCallback: (_) => false,
                       name: "DMASR")        // DMA Status bit
               .WithReservedBits(1, 31);
           Registers.DMAInterruptEnable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { if (value) { this.interruptEnableDMA = true; } },
                    name: "DMAIER")          // DMA Interrupt Enable bit
               .WithReservedBits(1, 31);
           Registers.DMAInterruptDisable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { if (value) { this.interruptEnableDMA = false; } },
                    name: "DMAIDR")          // DMA Interrupt Disable bit
               .WithReservedBits(1, 31);
           Registers.DMAInterruptMask.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { this.interruptMaskDMA = value; },
                    name: "DMAIMR")          // DMA Interrupt Mask bit
               .WithReservedBits(1, 31);
           Registers.DMAInterruptStatus.Define(this)
               .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ => { var ret = transferDone; transferDone = false; return ret; },
                    name: "DMAISR")          // DMA Interrupt Status bit. This flag is reset after read.
               .WithReservedBits(1, 31);
           Registers.Control.Define(this)
               .WithFlag(0, FieldMode.Read | FieldMode.WriteOneToClear,
                    valueProviderCallback: _ => false,
                    name: "RESET")           // CRC Computation Reset
               .WithReservedBits(1, 31)
               .WithWriteCallback((_, __) => { crcConfigDirty = true; });
           Registers.Mode.Define(this)
               .WithFlag(0,
                    valueProviderCallback: _ => this.globalEnable,
                    writeCallback: (_, value) => { this.globalEnable = value; },
                    name: "ENABLE")          // CRC Enable
               .WithFlag(1,
                    valueProviderCallback: _ => this.compareMode,
                    writeCallback: (_, value) => { this.compareMode = value; },
                    name: "COMPARE")         // CRC Compare
               .WithEnumField(2, 2,
                    out poly,
                    name: "PTYPE")           // Primitive Polynomial 0 - CCITT8023, 1 - CASTAGNOLI, 2 - CCITT16
               .WithTag("DIVIDER", 4, 4)     // Request Divider
               .WithReservedBits(8, 24)
               .WithWriteCallback((_, __) => { crcConfigDirty = true; });
           Registers.Status.Define(this)
               .WithValueField(0, 32, FieldMode.Read,
                    valueProviderCallback: _ => CRC.Value,
                    name: "CRC");            // Cyclic Redundancy Check Value
           Registers.InterruptEnable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { if (value) { this.interruptEnableError = true; } },
                    name: "ERRIER")          // Error Interrupt Enable bit
               .WithReservedBits(1, 31);
           Registers.InterruptDisable.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { if (value) { this.interruptEnableError = false; } },
                    name: "ERRIDR")          // Error Interrupt Disable bit
               .WithReservedBits(1, 31);
           Registers.InterruptMask.Define(this)
               .WithFlag(0, FieldMode.Write,
                    writeCallback: (_, value) => { this.interruptMaskError = value; },
                    name: "ERRIMR")          // Error Interrupt Mask bit
               .WithReservedBits(1, 31);
           Registers.InterruptStatus.Define(this)
               .WithFlag(0, FieldMode.Read,
                    valueProviderCallback: _ => this.InterruptStatusError,
                    name: "ERRISR")          // Error Interrupt Status bit
               .WithReservedBits(1, 31);
        }

        private void UpdateInterrupts()
        {
            var state = MaskedErrorInterruptStatus || MaskedDMAInterruptStatus;
            this.DebugLog("Setting IRQ to {0}", state ? "set" : "unset");
            IRQ.Set(state);
        }

        private void ReloadCRCConfig()
        {
            var config = new CRCConfig(
                polyMap[poly.Value],
                reflectInput: false,
                reflectOutput: false,
                init: 0xFFFFFFFF,
                xorOutput: 0x0
            );
            if(crc == null || !config.Equals(crc.Config))
            {
                crc = new CRCEngine(config);
            }
            else
            {
                crc.Reset();
            }
            crcConfigDirty = false;
        }

        private void ComputeCRC()
        {
            if(globalEnable)
            {   
                tcRegisters = TransferControlPacket.ReadFrom(this.descriptorAddress.Value << 9, this.sysbus);
                var data = this.sysbus.ReadBytes(tcRegisters.transferAddress, (int)tcRegisters.transferSize * tcRegisters.readByteMultiplier);
                CRC.Calculate(data);
                transferDone = true;
                UpdateInterrupts();
            }
            else
            {
                this.Log(LogLevel.Warning, "Trying to compute CRC without setting the ENABLE bit in CRCCU_MR");
            }
        }
        
        private CRCEngine CRC
        {
            get
            {
                if(crc == null || crcConfigDirty)
                {
                    ReloadCRCConfig();
                }
                return crc;
            }
        }

        private bool MaskedDMAInterruptStatus => transferDone && interruptEnableDMA && interruptMaskDMA && tcRegisters.contextDoneInterruptEnable;

        private bool InterruptStatusError => (CRC.Value != tcRegisters.referenceCRC) && compareMode; 

        private bool MaskedErrorInterruptStatus => InterruptStatusError && interruptEnableError && interruptMaskError;

        private bool crcConfigDirty;
        private bool compareMode;
        private bool globalEnable;
        private bool interruptEnableDMA;
        private bool interruptEnableError; 
        private bool interruptMaskDMA;
        private bool interruptMaskError; 
        private bool transferDone;
        private CRCEngine crc;
        private IValueRegisterField descriptorAddress;
        private IEnumRegisterField<CRCPolyType> poly;
        private TransferControlPacket tcRegisters;
        private static readonly Dictionary<CRCPolyType, CRCPolynomial> polyMap = new Dictionary<CRCPolyType, CRCPolynomial> () 
        {
            {
                CRCPolyType.CRC32,
                CRCPolynomial.CRC32
            },
            {
                CRCPolyType.CRC32C,
                CRCPolynomial.CRC32C
            },
            {
                CRCPolyType.CRC16_CCITT,
                CRCPolynomial.CRC16_CCITT
            },
        };

        private enum CRCPolyType : byte
        {
            CRC32 = 0x0,
            CRC32C = 0x1,
            CRC16_CCITT = 0x2
        }

        private enum Registers : long
        {
            DescriptorBase = 0x0,
            DMAEnable = 0x8,
            DMADisable = 0xC,
            DMAStatus = 0x10,
            DMAInterruptEnable = 0x14,
            DMAInterruptDisable = 0x18,
            DMAInterruptMask = 0x1C,
            DMAInterruptStatus = 0x20,
            Control = 0x34,
            Mode = 0x38,
            Status = 0x3C,
            InterruptEnable = 0x40,
            InterruptDisable = 0x44,
            InterruptMask = 0x48,
            InterruptStatus = 0x4C
        }

        [LeastSignificantByteFirst]
        private struct TransferControlPacket
        {
#pragma warning disable 649
            [PacketField, Offset(doubleWords: 0, bits: 0), Width(32)]
            public uint transferAddress;
            [PacketField, Offset(doubleWords: 1, bits: 0), Width(16)]
            public uint transferSize;
            [PacketField, Offset(doubleWords: 1, bits: 24), Width(2)]
            public uint transferWidth;
            [PacketField, Offset(doubleWords: 1, bits: 27), Width(1)]
            public bool contextDoneInterruptEnable;
            [PacketField, Offset(doubleWords: 4, bits: 0), Width(32)]
            public uint referenceCRC;
#pragma warning restore 649

            public int readByteMultiplier;

            public static TransferControlPacket ReadFrom(ulong address, IBusController sysbus)
            {
                var tcBuffer = sysbus.ReadBytes(address, Packet.CalculateLength<TransferControlPacket>());
                var tcRegisters = Packet.Decode<TransferControlPacket>(tcBuffer);
                switch(tcRegisters.transferWidth)
                {
                    case 1:
                        // HALFWORD
                        tcRegisters.readByteMultiplier = 2;
                        break;
                    case 2:
                        // WORD
                        tcRegisters.readByteMultiplier = 4;
                        break;
                    default:
                        // BYTE
                        tcRegisters.readByteMultiplier = 1;
                        break;
                }
                return tcRegisters;
            }
        }
    }
}
