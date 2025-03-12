//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.Silabs
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord | AllowedTranslation.WordToDoubleWord)]
    public class Smu_1 : IBusPeripheral, IKnownSize
    {
        public Smu_1(Machine machine, bool logRegisterAccess = false, bool logInterrupts = false)
        {
            this.machine = machine;
            this.LogRegisterAccess = logRegisterAccess;
            this.LogInterrupts = logInterrupts;

            SecureIRQ = new GPIO();
            SecurePriviledgedIRQ = new GPIO();
            NonSecurePriviledgedIRQ = new GPIO();

            privilegedPeripheralAccess = new IFlagRegisterField[NumberOfPeripherals];
            nonSecurePrivilegedPeripheralAccess = new IFlagRegisterField[NumberOfPeripherals];
            securePeripheralAccess = new IFlagRegisterField[NumberOfPeripherals];
            disablePeripheralAccess = new IFlagRegisterField[NumberOfPeripherals];
            privilegedBusMusterAccess = new IFlagRegisterField[NumberOfBusMasters];
            secureBusMusterAccess = new IFlagRegisterField[NumberOfBusMasters];

            secureRegistersCollection = BuildSecureRegistersCollection();
            nonSecureRegistersCollection = BuildNonSecureRegistersCollection();
        }

        public void Reset()
        {
        }

        [ConnectionRegionAttribute("smu_s")]
        public void WriteDoubleWordSecure(long offset, uint value)
        {
            Write<Registers>(secureRegistersCollection, "SmuSecure", offset, value);
        }

        [ConnectionRegionAttribute("smu_s")]
        public uint ReadDoubleWordSecure(long offset)
        {
            return Read<Registers>(secureRegistersCollection, "SmuSecure", offset);
        }

        [ConnectionRegionAttribute("smu_ns")]
        public void WriteDoubleWordNonSecure(long offset, uint value)
        {
            Write<Registers>(nonSecureRegistersCollection, "SmuNonSecure", offset, value);
        }

        [ConnectionRegionAttribute("smu_ns")]
        public uint ReadDoubleWordNonSecure(long offset)
        {
            return Read<Registers>(nonSecureRegistersCollection, "SmuNonSecure", offset);
        }

        private uint Read<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, bool internal_read = false)
        where T : struct, IComparable, IFormattable
        {
            var result = 0U;
            long internal_offset = offset;

            // Set, Clear, Toggle registers should only be used for write operations. But just in case we convert here as well.
            if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
            {
                // Set register
                internal_offset = offset - SetRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {  
                    this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
            {
                // Clear register
                internal_offset = offset - ClearRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            } else if (offset >= ToggleRegisterOffset)
            {
                // Toggle register
                internal_offset = offset - ToggleRegisterOffset;
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset);
                }
            }

            try
            {
                if(registersCollection.TryRead(internal_offset, out result))
                {
                    return result;
                }
            }
            finally
            {
                if (LogRegisterAccess && !internal_read)
                {
                    this.Log(LogLevel.Info, "{0}: Read from {1} at offset 0x{2:X} ({3}), returned 0x{4:X}", 
                             this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), result);
                }
            }

            if (LogRegisterAccess && !internal_read)
            {
                this.Log(LogLevel.Warning, "Unhandled read from {0} at offset 0x{1:X} ({2}).", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"));
            }

            return 0;
        }

        private void Write<T>(DoubleWordRegisterCollection registersCollection, string regionName, long offset, uint value)
        where T : struct, IComparable, IFormattable
        {
            machine.ClockSource.ExecuteInLock(delegate {
                long internal_offset = offset;
                uint internal_value = value;

                if (offset >= SetRegisterOffset && offset < ClearRegisterOffset) 
                {
                    // Set register
                    internal_offset = offset - SetRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value | value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "SET Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, SET_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ClearRegisterOffset && offset < ToggleRegisterOffset) 
                {
                    // Clear register
                    internal_offset = offset - ClearRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value & ~value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "CLEAR Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, CLEAR_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                } else if (offset >= ToggleRegisterOffset)
                {
                    // Toggle register
                    internal_offset = offset - ToggleRegisterOffset;
                    uint old_value = Read<T>(registersCollection, regionName, internal_offset, true);
                    internal_value = old_value ^ value;
                    if (LogRegisterAccess)
                    {
                        this.Log(LogLevel.Noisy, "TOGGLE Operation on {0}, offset=0x{1:X}, internal_offset=0x{2:X}, TOGGLE_value=0x{3:X}, old_value=0x{4:X}, new_value=0x{5:X}", Enum.Format(typeof(T), internal_offset, "G"), offset, internal_offset, value, old_value, internal_value);
                    }
                }

                if (LogRegisterAccess)
                {
                    this.Log(LogLevel.Info, "{0}: Write to {1} at offset 0x{2:X} ({3}), value 0x{4:X}", 
                            this.GetTime(), regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                }
                if(!registersCollection.TryWrite(internal_offset, internal_value) && LogRegisterAccess)
                {
                    this.Log(LogLevel.Warning, "Unhandled write to {0} at offset 0x{1:X} ({2}), value 0x{3:X}.", regionName, internal_offset, Enum.Format(typeof(T), internal_offset, "G"), internal_value);
                    return;
                }
            });
        }

        private DoubleWordRegisterCollection BuildSecureRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.IpVersion, new DoubleWordRegister(this, 0x1)
                    .WithValueField(0, 32, FieldMode.Read, valueProviderCallback: _ => Version, name: "IPVERSION")
                },
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out secureLock, FieldMode.Read, name: "SMULOCK")
                    .WithFlag(1, out secureProgrammingError, FieldMode.Read, name: "SMUPRGERR")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 24, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        secureLock.Value = (value != UnlockKey);
                    }, name: "SMULOCKKEY")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PPU_PrivilegeInterrupt, name: "PPUPRIVIF")
                    .WithReservedBits(1, 1)
                    .WithFlag(2, out PPU_InstructionInterrupt, name: "PPUINSTIF")
                    .WithReservedBits(3, 13)
                    .WithFlag(16, out PPU_SecurityInterrupt, name: "PPUSECIF")
                    .WithFlag(17, out BMPU_SecurityInterrupt, name: "BMPUSECIF")
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PPU_PrivilegeInterruptEnable, name: "PPUPRIVIEN")
                    .WithReservedBits(1, 1)
                    .WithFlag(2, out PPU_InstructionInterruptEnable, name: "PPUINSTIEN")
                    .WithReservedBits(3, 13)
                    .WithFlag(16, out PPU_SecurityInterruptEnable, name: "PPUSECIEN")
                    .WithFlag(17, out BMPU_SecurityInterruptEnable, name: "BMPUSECIEN")
                    .WithReservedBits(18, 14)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.M33Control, new DoubleWordRegister(this)
                    .WithFlag(0, out lockSAU, name: "LOCKSAU")
                    .WithFlag(1, out lockNonSecureMPU, name: "LOCKNSMPU")
                    .WithFlag(2, out lockSecureMPU, name: "LOCKSMPU")
                    .WithFlag(3, out lockNonSecureVTOR, name: "LOCKNSVTOR")
                    .WithFlag(4, out lockSecureVTAIRCR, name: "LOCKSVTAIRCR")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.M33InitNsVector, new DoubleWordRegister(this)
                    .WithReservedBits(0, 7)
                    .WithTag("TBLOFF", 7, 25)
                },
                {(long)Registers.M33InitSVector, new DoubleWordRegister(this)
                    .WithReservedBits(0, 7)
                    .WithTag("TBLOFF", 7, 25)
                },
                {(long)Registers.PPU_PriviledgedAttribute0, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out privilegedPeripheralAccess[0], name: "PPUPATD0")
                    .WithFlag(1, out privilegedPeripheralAccess[1], name: "PPUPATD1")
                    .WithFlag(2, out privilegedPeripheralAccess[2], name: "PPUPATD2")
                    .WithFlag(3, out privilegedPeripheralAccess[3], name: "PPUPATD3")
                    .WithFlag(4, out privilegedPeripheralAccess[4], name: "PPUPATD4")
                    .WithFlag(5, out privilegedPeripheralAccess[5], name: "PPUPATD5")
                    .WithFlag(6, out privilegedPeripheralAccess[6], name: "PPUPATD6")
                    .WithFlag(7, out privilegedPeripheralAccess[7], name: "PPUPATD7")
                    .WithFlag(8, out privilegedPeripheralAccess[8], name: "PPUPATD8")
                    .WithFlag(9, out privilegedPeripheralAccess[9], name: "PPUPATD9")
                    .WithFlag(10, out privilegedPeripheralAccess[10], name: "PPUPATD10")
                    .WithFlag(11, out privilegedPeripheralAccess[11], name: "PPUPATD11")
                    .WithFlag(12, out privilegedPeripheralAccess[12], name: "PPUPATD12")
                    .WithFlag(13, out privilegedPeripheralAccess[13], name: "PPUPATD13")
                    .WithFlag(14, out privilegedPeripheralAccess[14], name: "PPUPATD14")
                    .WithFlag(15, out privilegedPeripheralAccess[15], name: "PPUPATD15")
                    .WithFlag(16, out privilegedPeripheralAccess[16], name: "PPUPATD16")
                    .WithFlag(17, out privilegedPeripheralAccess[17], name: "PPUPATD17")
                    .WithFlag(18, out privilegedPeripheralAccess[18], name: "PPUPATD18")
                    .WithFlag(19, out privilegedPeripheralAccess[19], name: "PPUPATD19")
                    .WithFlag(20, out privilegedPeripheralAccess[20], name: "PPUPATD20")
                    .WithFlag(21, out privilegedPeripheralAccess[21], name: "PPUPATD21")
                    .WithFlag(22, out privilegedPeripheralAccess[22], name: "PPUPATD22")
                    .WithFlag(23, out privilegedPeripheralAccess[23], name: "PPUPATD23")
                    .WithFlag(24, out privilegedPeripheralAccess[24], name: "PPUPATD24")
                    .WithFlag(25, out privilegedPeripheralAccess[25], name: "PPUPATD25")
                    .WithFlag(26, out privilegedPeripheralAccess[26], name: "PPUPATD26")
                    .WithFlag(27, out privilegedPeripheralAccess[27], name: "PPUPATD27")
                    .WithFlag(28, out privilegedPeripheralAccess[28], name: "PPUPATD28")
                    .WithFlag(29, out privilegedPeripheralAccess[29], name: "PPUPATD29")
                    .WithFlag(30, out privilegedPeripheralAccess[30], name: "PPUPATD30")
                    .WithFlag(31, out privilegedPeripheralAccess[31], name: "PPUPATD31")
                },
                {(long)Registers.PPU_PriviledgedAttribute1, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out privilegedPeripheralAccess[32], name: "PPUPATD0")
                    .WithFlag(1, out privilegedPeripheralAccess[33], name: "PPUPATD1")
                    .WithFlag(2, out privilegedPeripheralAccess[34], name: "PPUPATD2")
                    .WithFlag(3, out privilegedPeripheralAccess[35], name: "PPUPATD3")
                    .WithFlag(4, out privilegedPeripheralAccess[36], name: "PPUPATD4")
                    .WithFlag(5, out privilegedPeripheralAccess[37], name: "PPUPATD5")
                    .WithFlag(6, out privilegedPeripheralAccess[38], name: "PPUPATD6")
                    .WithFlag(7, out privilegedPeripheralAccess[39], name: "PPUPATD7")
                    .WithFlag(8, out privilegedPeripheralAccess[40], name: "PPUPATD8")
                    .WithFlag(9, out privilegedPeripheralAccess[41], name: "PPUPATD9")
                    .WithFlag(10, out privilegedPeripheralAccess[42], name: "PPUPATD10")
                    .WithFlag(11, out privilegedPeripheralAccess[43], name: "PPUPATD11")
                    .WithFlag(12, out privilegedPeripheralAccess[44], name: "PPUPATD12")
                    .WithFlag(13, out privilegedPeripheralAccess[45], name: "PPUPATD13")
                    .WithFlag(14, out privilegedPeripheralAccess[46], name: "PPUPATD14")
                    .WithFlag(15, out privilegedPeripheralAccess[47], name: "PPUPATD15")
                    .WithFlag(16, out privilegedPeripheralAccess[48], name: "PPUPATD16")
                    .WithFlag(17, out privilegedPeripheralAccess[49], name: "PPUPATD17")
                    .WithFlag(18, out privilegedPeripheralAccess[50], name: "PPUPATD18")
                    .WithFlag(19, out privilegedPeripheralAccess[51], name: "PPUPATD19")
                    .WithFlag(20, out privilegedPeripheralAccess[52], name: "PPUPATD20")
                    .WithFlag(21, out privilegedPeripheralAccess[53], name: "PPUPATD21")
                    .WithFlag(22, out privilegedPeripheralAccess[54], name: "PPUPATD22")
                    .WithFlag(23, out privilegedPeripheralAccess[55], name: "PPUPATD23")
                    .WithFlag(24, out privilegedPeripheralAccess[56], name: "PPUPATD24")
                    .WithFlag(25, out privilegedPeripheralAccess[57], name: "PPUPATD25")
                    .WithFlag(26, out privilegedPeripheralAccess[58], name: "PPUPATD26")
                    .WithFlag(27, out privilegedPeripheralAccess[59], name: "PPUPATD27")
                    .WithFlag(28, out privilegedPeripheralAccess[60], name: "PPUPATD28")
                    .WithFlag(29, out privilegedPeripheralAccess[61], name: "PPUPATD29")
                    .WithFlag(30, out privilegedPeripheralAccess[62], name: "PPUPATD30")
                    .WithFlag(31, out privilegedPeripheralAccess[63], name: "PPUPATD31")
                },
                {(long)Registers.PPU_SecureAttribute0, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out securePeripheralAccess[0], name: "PPUSATD0")
                    .WithFlag(1, out securePeripheralAccess[1], name: "PPUSATD1")
                    .WithFlag(2, out securePeripheralAccess[2], name: "PPUSATD2")
                    .WithFlag(3, out securePeripheralAccess[3], name: "PPUSATD3")
                    .WithFlag(4, out securePeripheralAccess[4], name: "PPUSATD4")
                    .WithFlag(5, out securePeripheralAccess[5], name: "PPUSATD5")
                    .WithFlag(6, out securePeripheralAccess[6], name: "PPUSATD6")
                    .WithFlag(7, out securePeripheralAccess[7], name: "PPUSATD7")
                    .WithFlag(8, out securePeripheralAccess[8], name: "PPUSATD8")
                    .WithFlag(9, out securePeripheralAccess[9], name: "PPUSATD9")
                    .WithFlag(10, out securePeripheralAccess[10], name: "PPUSATD10")
                    .WithFlag(11, out securePeripheralAccess[11], name: "PPUSATD11")
                    .WithFlag(12, out securePeripheralAccess[12], name: "PPUSATD12")
                    .WithFlag(13, out securePeripheralAccess[13], name: "PPUSATD13")
                    .WithFlag(14, out securePeripheralAccess[14], name: "PPUSATD14")
                    .WithFlag(15, out securePeripheralAccess[15], name: "PPUSATD15")
                    .WithFlag(16, out securePeripheralAccess[16], name: "PPUSATD16")
                    .WithFlag(17, out securePeripheralAccess[17], name: "PPUSATD17")
                    .WithFlag(18, out securePeripheralAccess[18], name: "PPUSATD18")
                    .WithFlag(19, out securePeripheralAccess[19], name: "PPUSATD19")
                    .WithFlag(20, out securePeripheralAccess[20], name: "PPUSATD20")
                    .WithFlag(21, out securePeripheralAccess[21], name: "PPUSATD21")
                    .WithFlag(22, out securePeripheralAccess[22], name: "PPUSATD22")
                    .WithFlag(23, out securePeripheralAccess[23], name: "PPUSATD23")
                    .WithFlag(24, out securePeripheralAccess[24], name: "PPUSATD24")
                    .WithFlag(25, out securePeripheralAccess[25], name: "PPUSATD25")
                    .WithFlag(26, out securePeripheralAccess[26], name: "PPUSATD26")
                    .WithFlag(27, out securePeripheralAccess[27], name: "PPUSATD27")
                    .WithFlag(28, out securePeripheralAccess[28], name: "PPUSATD28")
                    .WithFlag(29, out securePeripheralAccess[29], name: "PPUSATD29")
                    .WithFlag(30, out securePeripheralAccess[30], name: "PPUSATD30")
                    .WithFlag(31, out securePeripheralAccess[31], name: "PPUSATD31")
                },
                {(long)Registers.PPU_SecureAttribute1, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out securePeripheralAccess[32], name: "PPUSATD0")
                    .WithFlag(1, out securePeripheralAccess[33], name: "PPUSATD1")
                    .WithFlag(2, out securePeripheralAccess[34], name: "PPUSATD2")
                    .WithFlag(3, out securePeripheralAccess[35], name: "PPUSATD3")
                    .WithFlag(4, out securePeripheralAccess[36], name: "PPUSATD4")
                    .WithFlag(5, out securePeripheralAccess[37], name: "PPUSATD5")
                    .WithFlag(6, out securePeripheralAccess[38], name: "PPUSATD6")
                    .WithFlag(7, out securePeripheralAccess[39], name: "PPUSATD7")
                    .WithFlag(8, out securePeripheralAccess[40], name: "PPUSATD8")
                    .WithFlag(9, out securePeripheralAccess[41], name: "PPUSATD9")
                    .WithFlag(10, out securePeripheralAccess[42], name: "PPUSATD10")
                    .WithFlag(11, out securePeripheralAccess[43], name: "PPUSATD11")
                    .WithFlag(12, out securePeripheralAccess[44], name: "PPUSATD12")
                    .WithFlag(13, out securePeripheralAccess[45], name: "PPUSATD13")
                    .WithFlag(14, out securePeripheralAccess[46], name: "PPUSATD14")
                    .WithFlag(15, out securePeripheralAccess[47], name: "PPUSATD15")
                    .WithFlag(16, out securePeripheralAccess[48], name: "PPUSATD16")
                    .WithFlag(17, out securePeripheralAccess[49], name: "PPUSATD17")
                    .WithFlag(18, out securePeripheralAccess[50], name: "PPUSATD18")
                    .WithFlag(19, out securePeripheralAccess[51], name: "PPUSATD19")
                    .WithFlag(20, out securePeripheralAccess[52], name: "PPUSATD20")
                    .WithFlag(21, out securePeripheralAccess[53], name: "PPUSATD21")
                    .WithFlag(22, out securePeripheralAccess[54], name: "PPUSATD22")
                    .WithFlag(23, out securePeripheralAccess[55], name: "PPUSATD23")
                    .WithFlag(24, out securePeripheralAccess[56], name: "PPUSATD24")
                    .WithFlag(25, out securePeripheralAccess[57], name: "PPUSATD25")
                    .WithFlag(26, out securePeripheralAccess[58], name: "PPUSATD26")
                    .WithFlag(27, out securePeripheralAccess[59], name: "PPUSATD27")
                    .WithFlag(28, out securePeripheralAccess[60], name: "PPUSATD28")
                    .WithFlag(29, out securePeripheralAccess[61], name: "PPUSATD29")
                    .WithFlag(30, out securePeripheralAccess[62], name: "PPUSATD30")
                    .WithFlag(31, out securePeripheralAccess[63], name: "PPUSATD31")
                },
                {(long)Registers.PPU_Disable0, new DoubleWordRegister(this)
                    .WithFlag(0, out disablePeripheralAccess[0], name: "PPUDIS0")
                    .WithFlag(1, out disablePeripheralAccess[1], name: "PPUDIS1")
                    .WithFlag(2, out disablePeripheralAccess[2], name: "PPUDIS2")
                    .WithFlag(3, out disablePeripheralAccess[3], name: "PPUDIS3")
                    .WithFlag(4, out disablePeripheralAccess[4], name: "PPUDIS4")
                    .WithFlag(5, out disablePeripheralAccess[5], name: "PPUDIS5")
                    .WithFlag(6, out disablePeripheralAccess[6], name: "PPUDIS6")
                    .WithFlag(7, out disablePeripheralAccess[7], name: "PPUDIS7")
                    .WithFlag(8, out disablePeripheralAccess[8], name: "PPUDIS8")
                    .WithFlag(9, out disablePeripheralAccess[9], name: "PPUDIS9")
                    .WithFlag(10, out disablePeripheralAccess[10], name: "PPUDIS10")
                    .WithFlag(11, out disablePeripheralAccess[11], name: "PPUDIS11")
                    .WithFlag(12, out disablePeripheralAccess[12], name: "PPUDIS12")
                    .WithFlag(13, out disablePeripheralAccess[13], name: "PPUDIS13")
                    .WithFlag(14, out disablePeripheralAccess[14], name: "PPUDIS14")
                    .WithFlag(15, out disablePeripheralAccess[15], name: "PPUDIS15")
                    .WithFlag(16, out disablePeripheralAccess[16], name: "PPUDIS16")
                    .WithFlag(17, out disablePeripheralAccess[17], name: "PPUDIS17")
                    .WithFlag(18, out disablePeripheralAccess[18], name: "PPUDIS18")
                    .WithFlag(19, out disablePeripheralAccess[19], name: "PPUDIS19")
                    .WithFlag(20, out disablePeripheralAccess[20], name: "PPUDIS20")
                    .WithFlag(21, out disablePeripheralAccess[21], name: "PPUDIS21")
                    .WithFlag(22, out disablePeripheralAccess[22], name: "PPUDIS22")
                    .WithFlag(23, out disablePeripheralAccess[23], name: "PPUDIS23")
                    .WithFlag(24, out disablePeripheralAccess[24], name: "PPUDIS24")
                    .WithFlag(25, out disablePeripheralAccess[25], name: "PPUDIS25")
                    .WithFlag(26, out disablePeripheralAccess[26], name: "PPUDIS26")
                    .WithFlag(27, out disablePeripheralAccess[27], name: "PPUDIS27")
                    .WithFlag(28, out disablePeripheralAccess[28], name: "PPUDIS28")
                    .WithFlag(29, out disablePeripheralAccess[29], name: "PPUDIS29")
                    .WithFlag(30, out disablePeripheralAccess[30], name: "PPUDIS30")
                    .WithFlag(31, out disablePeripheralAccess[31], name: "PPUDIS31")
                },
                {(long)Registers.PPU_Disable1, new DoubleWordRegister(this)
                    .WithFlag(0, out disablePeripheralAccess[32], name: "PPUDIS0")
                    .WithFlag(1, out disablePeripheralAccess[33], name: "PPUDIS1")
                    .WithFlag(2, out disablePeripheralAccess[34], name: "PPUDIS2")
                    .WithFlag(3, out disablePeripheralAccess[35], name: "PPUDIS3")
                    .WithFlag(4, out disablePeripheralAccess[36], name: "PPUDIS4")
                    .WithFlag(5, out disablePeripheralAccess[37], name: "PPUDIS5")
                    .WithFlag(6, out disablePeripheralAccess[38], name: "PPUDIS6")
                    .WithFlag(7, out disablePeripheralAccess[39], name: "PPUDIS7")
                    .WithFlag(8, out disablePeripheralAccess[40], name: "PPUDIS8")
                    .WithFlag(9, out disablePeripheralAccess[41], name: "PPUDIS9")
                    .WithFlag(10, out disablePeripheralAccess[42], name: "PPUDIS10")
                    .WithFlag(11, out disablePeripheralAccess[43], name: "PPUDIS11")
                    .WithFlag(12, out disablePeripheralAccess[44], name: "PPUDIS12")
                    .WithFlag(13, out disablePeripheralAccess[45], name: "PPUDIS13")
                    .WithFlag(14, out disablePeripheralAccess[46], name: "PPUDIS14")
                    .WithFlag(15, out disablePeripheralAccess[47], name: "PPUDIS15")
                    .WithFlag(16, out disablePeripheralAccess[48], name: "PPUDIS16")
                    .WithFlag(17, out disablePeripheralAccess[49], name: "PPUDIS17")
                    .WithFlag(18, out disablePeripheralAccess[50], name: "PPUDIS18")
                    .WithFlag(19, out disablePeripheralAccess[51], name: "PPUDIS19")
                    .WithFlag(20, out disablePeripheralAccess[52], name: "PPUDIS20")
                    .WithFlag(21, out disablePeripheralAccess[53], name: "PPUDIS21")
                    .WithFlag(22, out disablePeripheralAccess[54], name: "PPUDIS22")
                    .WithFlag(23, out disablePeripheralAccess[55], name: "PPUDIS23")
                    .WithFlag(24, out disablePeripheralAccess[56], name: "PPUDIS24")
                    .WithFlag(25, out disablePeripheralAccess[57], name: "PPUDIS25")
                    .WithFlag(26, out disablePeripheralAccess[58], name: "PPUDIS26")
                    .WithFlag(27, out disablePeripheralAccess[59], name: "PPUDIS27")
                    .WithFlag(28, out disablePeripheralAccess[60], name: "PPUDIS28")
                    .WithFlag(29, out disablePeripheralAccess[61], name: "PPUDIS29")
                    .WithFlag(30, out disablePeripheralAccess[62], name: "PPUDIS30")
                    .WithFlag(31, out disablePeripheralAccess[63], name: "PPUDIS31")
                },
                {(long)Registers.PPU_FaultStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out PPU_FaultStatus, FieldMode.Read, name: "PPUFSPERIPHID")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.BMPU_PriviledgedAttribute0, new DoubleWordRegister(this, 0x1F)
                    .WithFlag(0, out privilegedBusMusterAccess[0], name: "BMPUPATD0")
                    .WithFlag(1, out privilegedBusMusterAccess[1], name: "BMPUPATD1")
                    .WithFlag(2, out privilegedBusMusterAccess[2], name: "BMPUPATD2")
                    .WithFlag(3, out privilegedBusMusterAccess[3], name: "BMPUPATD3")
                    .WithFlag(4, out privilegedBusMusterAccess[4], name: "BMPUPATD4")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.BMPU_SecureAttribute0, new DoubleWordRegister(this, 0x1F)
                    .WithFlag(0, out secureBusMusterAccess[0], name: "BMPUSATD0")
                    .WithFlag(1, out secureBusMusterAccess[1], name: "BMPUSATD1")
                    .WithFlag(2, out secureBusMusterAccess[2], name: "BMPUSATD2")
                    .WithFlag(3, out secureBusMusterAccess[3], name: "BMPUSATD3")
                    .WithFlag(4, out secureBusMusterAccess[4], name: "BMPUSATD4")
                    .WithReservedBits(5, 27)
                },
                {(long)Registers.BMPU_FaultStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out BMPU_FaultStatus, FieldMode.Read, name: "BMPUFSMASTERID")
                    .WithReservedBits(8, 24)
                },
                {(long)Registers.BMPU_FaultStatusAddress, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out BMPU_FaultStatusAddress, FieldMode.Read, name: "BMPUFSMASTERID")
                },
                {(long)Registers.ESAU_RegionTypes0, new DoubleWordRegister(this)
                    .WithReservedBits(0, 12)
                    .WithFlag(12, out ESAU_Region3NonSecureType, name: "ESAUR3NS")
                    .WithReservedBits(13, 19)
                },
                {(long)Registers.ESAU_RegionTypes1, new DoubleWordRegister(this)
                    .WithReservedBits(0, 12)
                    .WithFlag(12, out ESAU_Region11NonSecureType, name: "ESAUR11NS")
                    .WithReservedBits(13, 19)
                },
                {(long)Registers.ESAU_MovableRegionBoundary0_1, new DoubleWordRegister(this, 0x02000000)
                    .WithReservedBits(0, 12)
                    .WithValueField(12, 16, out ESAU_MovableRegionBoundary0_1, name: "ESAUMRB01")
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => CheckMovableRegions())
                },
                {(long)Registers.ESAU_MovableRegionBoundary1_2, new DoubleWordRegister(this, 0x04000000)
                    .WithReservedBits(0, 12)
                    .WithValueField(12, 16, out ESAU_MovableRegionBoundary1_2, name: "ESAUMRB12")
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => CheckMovableRegions())
                },
                {(long)Registers.ESAU_MovableRegionBoundary4_5, new DoubleWordRegister(this, 0x02000000)
                    .WithReservedBits(0, 12)
                    .WithValueField(12, 16, out ESAU_MovableRegionBoundary4_5, name: "ESAUMRB45")
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => CheckMovableRegions())
                },
                {(long)Registers.ESAU_MovableRegionBoundary5_6, new DoubleWordRegister(this, 0x04000000)
                    .WithReservedBits(0, 12)
                    .WithValueField(12, 16, out ESAU_MovableRegionBoundary5_6, name: "ESAUMRB56")
                    .WithReservedBits(28, 4)
                    .WithChangeCallback((_, __) => CheckMovableRegions())
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        private DoubleWordRegisterCollection BuildNonSecureRegistersCollection()
        {
            var registerDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.Status, new DoubleWordRegister(this)
                    .WithFlag(0, out nonSecureLock, FieldMode.Read, name: "SMULOCK")
                    .WithFlag(1, out nonSecureProgrammingError, FieldMode.Read, name: "SMUPRGERR")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.Lock, new DoubleWordRegister(this)
                    .WithValueField(0, 24, FieldMode.Write, writeCallback: (_, value) =>
                    {
                        nonSecureLock.Value = (value != UnlockKey);
                    }, name: "SMULOCKKEY")
                    .WithReservedBits(24, 8)
                },
                {(long)Registers.InterruptFlags, new DoubleWordRegister(this)
                    .WithFlag(0, out PPUNS_PrivilegeInterrupt, name: "PPUNSPRIVIF")
                    .WithReservedBits(1, 1)
                    .WithFlag(2, out PPUNS_InstructionInterrupt, name: "PPUNSINSTIF")
                    .WithReservedBits(3, 29)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out PPUNS_PrivilegeInterruptEnable, name: "PPUNSPRIVIEN")
                    .WithReservedBits(1, 1)
                    .WithFlag(2, out PPUNS_InstructionInterruptEnable, name: "PPUNSINSTIEN")
                    .WithReservedBits(3, 29)
                    .WithChangeCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.PPU_PriviledgedAttribute0, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out nonSecurePrivilegedPeripheralAccess[0], name: "PPUPATD0")
                    .WithFlag(1, out nonSecurePrivilegedPeripheralAccess[1], name: "PPUPATD1")
                    .WithFlag(2, out nonSecurePrivilegedPeripheralAccess[2], name: "PPUPATD2")
                    .WithFlag(3, out nonSecurePrivilegedPeripheralAccess[3], name: "PPUPATD3")
                    .WithFlag(4, out nonSecurePrivilegedPeripheralAccess[4], name: "PPUPATD4")
                    .WithFlag(5, out nonSecurePrivilegedPeripheralAccess[5], name: "PPUPATD5")
                    .WithFlag(6, out nonSecurePrivilegedPeripheralAccess[6], name: "PPUPATD6")
                    .WithFlag(7, out nonSecurePrivilegedPeripheralAccess[7], name: "PPUPATD7")
                    .WithFlag(8, out nonSecurePrivilegedPeripheralAccess[8], name: "PPUPATD8")
                    .WithFlag(9, out nonSecurePrivilegedPeripheralAccess[9], name: "PPUPATD9")
                    .WithFlag(10, out nonSecurePrivilegedPeripheralAccess[10], name: "PPUPATD10")
                    .WithFlag(11, out nonSecurePrivilegedPeripheralAccess[11], name: "PPUPATD11")
                    .WithFlag(12, out nonSecurePrivilegedPeripheralAccess[12], name: "PPUPATD12")
                    .WithFlag(13, out nonSecurePrivilegedPeripheralAccess[13], name: "PPUPATD13")
                    .WithFlag(14, out nonSecurePrivilegedPeripheralAccess[14], name: "PPUPATD14")
                    .WithFlag(15, out nonSecurePrivilegedPeripheralAccess[15], name: "PPUPATD15")
                    .WithFlag(16, out nonSecurePrivilegedPeripheralAccess[16], name: "PPUPATD16")
                    .WithFlag(17, out nonSecurePrivilegedPeripheralAccess[17], name: "PPUPATD17")
                    .WithFlag(18, out nonSecurePrivilegedPeripheralAccess[18], name: "PPUPATD18")
                    .WithFlag(19, out nonSecurePrivilegedPeripheralAccess[19], name: "PPUPATD19")
                    .WithFlag(20, out nonSecurePrivilegedPeripheralAccess[20], name: "PPUPATD20")
                    .WithFlag(21, out nonSecurePrivilegedPeripheralAccess[21], name: "PPUPATD21")
                    .WithFlag(22, out nonSecurePrivilegedPeripheralAccess[22], name: "PPUPATD22")
                    .WithFlag(23, out nonSecurePrivilegedPeripheralAccess[23], name: "PPUPATD23")
                    .WithFlag(24, out nonSecurePrivilegedPeripheralAccess[24], name: "PPUPATD24")
                    .WithFlag(25, out nonSecurePrivilegedPeripheralAccess[25], name: "PPUPATD25")
                    .WithFlag(26, out nonSecurePrivilegedPeripheralAccess[26], name: "PPUPATD26")
                    .WithFlag(27, out nonSecurePrivilegedPeripheralAccess[27], name: "PPUPATD27")
                    .WithFlag(28, out nonSecurePrivilegedPeripheralAccess[28], name: "PPUPATD28")
                    .WithFlag(29, out nonSecurePrivilegedPeripheralAccess[29], name: "PPUPATD29")
                    .WithFlag(30, out nonSecurePrivilegedPeripheralAccess[30], name: "PPUPATD30")
                    .WithFlag(31, out nonSecurePrivilegedPeripheralAccess[31], name: "PPUPATD31")
                },
                {(long)Registers.PPU_PriviledgedAttribute1, new DoubleWordRegister(this, 0xFFFFFFFF)
                    .WithFlag(0, out nonSecurePrivilegedPeripheralAccess[32], name: "PPUPATD0")
                    .WithFlag(1, out nonSecurePrivilegedPeripheralAccess[33], name: "PPUPATD1")
                    .WithFlag(2, out nonSecurePrivilegedPeripheralAccess[34], name: "PPUPATD2")
                    .WithFlag(3, out nonSecurePrivilegedPeripheralAccess[35], name: "PPUPATD3")
                    .WithFlag(4, out nonSecurePrivilegedPeripheralAccess[36], name: "PPUPATD4")
                    .WithFlag(5, out nonSecurePrivilegedPeripheralAccess[37], name: "PPUPATD5")
                    .WithFlag(6, out nonSecurePrivilegedPeripheralAccess[38], name: "PPUPATD6")
                    .WithFlag(7, out nonSecurePrivilegedPeripheralAccess[39], name: "PPUPATD7")
                    .WithFlag(8, out nonSecurePrivilegedPeripheralAccess[40], name: "PPUPATD8")
                    .WithFlag(9, out nonSecurePrivilegedPeripheralAccess[41], name: "PPUPATD9")
                    .WithFlag(10, out nonSecurePrivilegedPeripheralAccess[42], name: "PPUPATD10")
                    .WithFlag(11, out nonSecurePrivilegedPeripheralAccess[43], name: "PPUPATD11")
                    .WithFlag(12, out nonSecurePrivilegedPeripheralAccess[44], name: "PPUPATD12")
                    .WithFlag(13, out nonSecurePrivilegedPeripheralAccess[45], name: "PPUPATD13")
                    .WithFlag(14, out nonSecurePrivilegedPeripheralAccess[46], name: "PPUPATD14")
                    .WithFlag(15, out nonSecurePrivilegedPeripheralAccess[47], name: "PPUPATD15")
                    .WithFlag(16, out nonSecurePrivilegedPeripheralAccess[48], name: "PPUPATD16")
                    .WithFlag(17, out nonSecurePrivilegedPeripheralAccess[49], name: "PPUPATD17")
                    .WithFlag(18, out nonSecurePrivilegedPeripheralAccess[50], name: "PPUPATD18")
                    .WithFlag(19, out nonSecurePrivilegedPeripheralAccess[51], name: "PPUPATD19")
                    .WithFlag(20, out nonSecurePrivilegedPeripheralAccess[52], name: "PPUPATD20")
                    .WithFlag(21, out nonSecurePrivilegedPeripheralAccess[53], name: "PPUPATD21")
                    .WithFlag(22, out nonSecurePrivilegedPeripheralAccess[54], name: "PPUPATD22")
                    .WithFlag(23, out nonSecurePrivilegedPeripheralAccess[55], name: "PPUPATD23")
                    .WithFlag(24, out nonSecurePrivilegedPeripheralAccess[56], name: "PPUPATD24")
                    .WithFlag(25, out nonSecurePrivilegedPeripheralAccess[57], name: "PPUPATD25")
                    .WithFlag(26, out nonSecurePrivilegedPeripheralAccess[58], name: "PPUPATD26")
                    .WithFlag(27, out nonSecurePrivilegedPeripheralAccess[59], name: "PPUPATD27")
                    .WithFlag(28, out nonSecurePrivilegedPeripheralAccess[60], name: "PPUPATD28")
                    .WithFlag(29, out nonSecurePrivilegedPeripheralAccess[61], name: "PPUPATD29")
                    .WithFlag(30, out nonSecurePrivilegedPeripheralAccess[62], name: "PPUPATD30")
                    .WithFlag(31, out nonSecurePrivilegedPeripheralAccess[63], name: "PPUPATD31")
                },
                {(long)Registers.PPU_FaultStatus, new DoubleWordRegister(this)
                    .WithValueField(0, 8, out PPU_NonSecureFaultStatus, FieldMode.Read, name: "PPUFSPERIPHID")
                    .WithReservedBits(8, 24)
                },
            };
            return new DoubleWordRegisterCollection(this, registerDictionary);
        }

        public long Size => 0x4000;
        public GPIO SecureIRQ { get; }
        public GPIO SecurePriviledgedIRQ { get; }
        public GPIO NonSecurePriviledgedIRQ { get; }
        private readonly Machine machine;
        private readonly DoubleWordRegisterCollection secureRegistersCollection;
        private readonly DoubleWordRegisterCollection nonSecureRegistersCollection;
        private const uint SetRegisterOffset = 0x1000;
        private const uint ClearRegisterOffset = 0x2000;
        private const uint ToggleRegisterOffset = 0x3000;
        private const uint Version = 1;
        private const uint UnlockKey = 0xACCE55;
        private const uint NumberOfPeripherals = 64;
        private const uint NumberOfBusMasters = 5;
        private bool LogRegisterAccess;
        private bool LogInterrupts;
        
#region register fields
        private readonly IFlagRegisterField[] privilegedPeripheralAccess;
        private readonly IFlagRegisterField[] nonSecurePrivilegedPeripheralAccess;
        private readonly IFlagRegisterField[] securePeripheralAccess;
        private readonly IFlagRegisterField[] disablePeripheralAccess;
        private readonly IFlagRegisterField[] privilegedBusMusterAccess;
        private readonly IFlagRegisterField[] secureBusMusterAccess;
        private IValueRegisterField ESAU_MovableRegionBoundary0_1;
        private IValueRegisterField ESAU_MovableRegionBoundary1_2;
        private IValueRegisterField ESAU_MovableRegionBoundary4_5;
        private IValueRegisterField ESAU_MovableRegionBoundary5_6;
        private IFlagRegisterField ESAU_Region3NonSecureType;
        private IFlagRegisterField ESAU_Region11NonSecureType;
        private IFlagRegisterField secureLock;
        private IFlagRegisterField nonSecureLock;
        private IFlagRegisterField secureProgrammingError;
        private IFlagRegisterField nonSecureProgrammingError;
        private IValueRegisterField PPU_FaultStatus;
        private IValueRegisterField PPU_NonSecureFaultStatus;
        private IValueRegisterField BMPU_FaultStatus;
        private IValueRegisterField BMPU_FaultStatusAddress;
        // TODO: SAU, MPUs, VTOR, VTAIRCR access must be filtered by SMU.
        private IFlagRegisterField lockSAU;
        private IFlagRegisterField lockNonSecureMPU;
        private IFlagRegisterField lockSecureMPU;
        private IFlagRegisterField lockNonSecureVTOR;
        private IFlagRegisterField lockSecureVTAIRCR;
        // Secure Interrupts
        private IFlagRegisterField PPU_PrivilegeInterrupt;
        private IFlagRegisterField PPU_InstructionInterrupt;
        private IFlagRegisterField PPU_SecurityInterrupt;
        private IFlagRegisterField BMPU_SecurityInterrupt;
        private IFlagRegisterField PPU_PrivilegeInterruptEnable;
        private IFlagRegisterField PPU_InstructionInterruptEnable;
        private IFlagRegisterField PPU_SecurityInterruptEnable;
        private IFlagRegisterField BMPU_SecurityInterruptEnable;
        // Non-Secure Interrupts
        private IFlagRegisterField PPUNS_PrivilegeInterrupt;
        private IFlagRegisterField PPUNS_InstructionInterrupt;
        private IFlagRegisterField PPUNS_PrivilegeInterruptEnable;
        private IFlagRegisterField PPUNS_InstructionInterruptEnable;
#endregion

#region methods
        private TimeInterval GetTime() => machine.LocalTimeSource.ElapsedVirtualTime;
        
        private void UpdateInterrupts()
        {

            /*
            The SMU contains three external interrupt lines, privileged, secure, and ns_privileged. 
            The privileged interrupt is asserted when the PPUINSTIF or the PPUPRIVIF is high and 
            the corresponding IEN bit is high. 
            The secure interrupt is asserted when the BMPUSECIF or the PPUSECIF is high and 
            the corresponding IEN bit is high. 
            The ns_privileged interrupt is asserted when the PPUNSPRIVIF or the PPUNSINSTIF is high and 
            the corresponding NSIEN bit is high.
            */
            machine.ClockSource.ExecuteInLock(delegate {
                var irq = ((BMPU_SecurityInterruptEnable.Value && BMPU_SecurityInterrupt.Value)
                           || PPU_SecurityInterruptEnable.Value && PPU_SecurityInterrupt.Value);
                SecureIRQ.Set(irq);
                
                irq = ((PPU_PrivilegeInterruptEnable.Value && PPU_PrivilegeInterrupt.Value)
                       || (PPU_InstructionInterruptEnable.Value && PPU_InstructionInterrupt.Value));
                SecurePriviledgedIRQ.Set(irq);
                
                irq = ((PPUNS_PrivilegeInterruptEnable.Value && PPUNS_PrivilegeInterrupt.Value)
                       || (PPUNS_InstructionInterruptEnable.Value && PPUNS_InstructionInterrupt.Value));
                NonSecurePriviledgedIRQ.Set(irq);
            });
        }

        /* 
         ESAU Memory Regions
          Region Num. |  Base Address       |   Limit Address      |  Security Attribute
          ------------+---------------------+----------------------+----------------------
             0        |  0x00000000         |   0x00000000|mrb01   |  Secure
             1        |  0x00000000|mrb01   |   0x00000000|mrb12   |  Non-Secure-Callable
             2        |  0x00000000|mrb12   |   0x0FE00000         |  Non-Secure
             3        |  0x0FE00000         |   0x10000000         |  Secure or Non-Secure
             4        |  0x20000000         |   0x20000000|mrb45   |  Secure
             5        |  0x20000000|mrb45   |   0x20000000|mrb56   |  Non-Secure-Callable
             6        |  0x20000000|mrb56   |   0x30000000         |  Non-Secure
             7        |  0x40000000         |   0x50000000         |  Secure
             8        |  0x50000000         |   0x60000000         |  Non-Secure
             9        |  0xA0000000         |   0xB0000000         |  Secure
             10       |  0xB0000000         |   0xC0000000         |  Non-Secure
             11       |  0xE0044000         |   0xE00FE000         |  Secure or Non-Secure
             12       |  0xE00FE000         |   0xE00FF000         |  Exempt
        */
        private SecurityType GetMemoryAddressSecurityType(uint address)
        {
            // Region 0
            if (address < (((uint)ESAU_MovableRegionBoundary0_1.Value) << 12))
            {
                return SecurityType.Secure;
            }
            // Region 1
            else if ((((uint)ESAU_MovableRegionBoundary0_1.Value) << 12) <= address
                     && address < (((uint)ESAU_MovableRegionBoundary1_2.Value) << 12))
            {
                return SecurityType.NonSecureCallable;
            }
            // Region 2
            else if ((((uint)ESAU_MovableRegionBoundary1_2.Value) << 12) <= address
                     && address < 0x0FE00000)
            {
                return SecurityType.NonSecure;
            }
            // Region 3
            else if (0x0FE00000 <= address && address < 0x10000000)
            {
                return (ESAU_Region3NonSecureType.Value) ? SecurityType.NonSecure : SecurityType.Secure;
            }
            // Region 4
            else if (0x20000000 <= address
                     && address < (0x20000000 | (((uint)ESAU_MovableRegionBoundary4_5.Value) << 12)))
            {
                return SecurityType.Secure;
            }
            // Region 5
            else if ((0x20000000 | (((uint)ESAU_MovableRegionBoundary4_5.Value) << 12)) <= address
                     && address < (0x20000000 | (((uint)ESAU_MovableRegionBoundary5_6.Value) << 12)))
            {
                return SecurityType.NonSecureCallable;
            }
            // Region 6
            else if ((0x20000000 | (((uint)ESAU_MovableRegionBoundary5_6.Value) << 12)) <= address
                     && address < 0x30000000)
            {
                return SecurityType.NonSecure;
            }
            // Region 7
            else if (0x40000000 <= address && address < 0x50000000)
            {
                return SecurityType.Secure;
            }
            // Region 8
            else if (0x50000000 <= address && address < 0x60000000)
            {
                return SecurityType.NonSecure;
            }
            // Region 9
            else if (0xA0000000 <= address && address < 0xB0000000)
            {
                return SecurityType.Secure;
            }
            // Region 10
            else if (0xB0000000 <= address && address < 0xC0000000)
            {
                return SecurityType.NonSecure;
            }
            // Region 11
            else if (0xE0044000 <= address && address < 0xE00FE000)
            {
                return (ESAU_Region11NonSecureType.Value) ? SecurityType.NonSecure : SecurityType.Secure;
            }
            // Region 12
            else if (0xE00FE000 <= address && address < 0xE00FF000)
            {
                return SecurityType.Exempt;
            }

            throw new Exception("GetMemoryAddressSecurityType() invalid address");
        }

        private void CheckMovableRegions()
        {
            if (ESAU_MovableRegionBoundary0_1.Value > ESAU_MovableRegionBoundary1_2.Value
                || ESAU_MovableRegionBoundary1_2.Value > 0x0FE00000
                || ESAU_MovableRegionBoundary4_5.Value > ESAU_MovableRegionBoundary5_6.Value
                || ((0x20000000 | (((uint)ESAU_MovableRegionBoundary5_6.Value) << 12))) >= 0x30000000)
            {
                // TODO: do we need to fire an interrupt or trigger a fault?
                secureProgrammingError.Value = true;
            }
        }
#endregion

#region enums
        private enum SecurityType
        {
            None              = 0,
            Secure            = 1,
            NonSecure         = 2,
            NonSecureCallable = 3,
            Exempt            = 4,
        }

        private enum PeripheralIndex
        {
            Scratchpad   = 0,
            Emu          = 1,
            Cmu          = 2,
            Hfxo0        = 3,
            Hfrco0       = 4,
            Fsrco        = 5,
            Dpll0        = 6,
            Lfxo         = 7,
            Lfrco        = 8,
            Ulfrco       = 9,
            Msc          = 10,
            Icache0      = 11,
            Prs          = 12,
            Gpio         = 13,
            Ldma         = 14,
            LdmaXbar     = 15,
            Timer0       = 16,
            Timer1       = 17,
            Timer2       = 18,
            Timer3       = 19,
            Timer4       = 20,
            Usart0       = 21,
            Usart1       = 22,
            Burtc        = 23,
            I2c1         = 24,
            ChipTestCtrl = 25,
            SyscfgCfgNs  = 26,
            Syscfg       = 27,
            Buram        = 28,
            IfadcDebug   = 29,
            Gpcrc        = 30,
            Dci          = 31,
            RootCfg      = 32,
            Dcdc         = 33,
            Pdm          = 34,
            RfSense      = 35,
            RadioAes     = 36,
            Smu          = 37,
            SmuCfgNs     = 38,
            Rtcc         = 39,
            LeTimer0     = 40,
            Iadc0        = 41,
            I2c0         = 42,
            Wdog0        = 43,
            Amuxcp0      = 44,
            Euart0       = 45,
            CryptoAcc    = 46,
            AhbRadio     = 47,
        }

        private enum BusMasterIndex
        {
            RadioAes        = 0,
            CryptoAcc       = 1,
            RadioSubSystem  = 2,
            RadioFadcdDebug = 3,
            Ldma            = 4,
        }

        private enum Registers
        {
            IpVersion                                 = 0x0000,
            Status                                    = 0x0004,
            Lock                                      = 0x0008,
            InterruptFlags                            = 0x000C,
            InterruptEnable                           = 0x0010,
            M33Control                                = 0x0020,
            M33InitNsVector                           = 0x0024,
            M33InitSVector                            = 0x0028,
            PPU_PriviledgedAttribute0                 = 0x0040,
            PPU_PriviledgedAttribute1                 = 0x0044,
            PPU_SecureAttribute0                      = 0x0060,
            PPU_SecureAttribute1                      = 0x0064,
            PPU_Disable0                              = 0x0120,
            PPU_Disable1                              = 0x0124,
            PPU_FaultStatus                           = 0x0140,
            BMPU_PriviledgedAttribute0                = 0x0150,
            BMPU_SecureAttribute0                     = 0x0170,
            BMPU_FaultStatus                          = 0x0250,
            BMPU_FaultStatusAddress                   = 0x0254,
            ESAU_RegionTypes0                         = 0x0260,
            ESAU_RegionTypes1                         = 0x0264,
            ESAU_MovableRegionBoundary0_1             = 0x0270,
            ESAU_MovableRegionBoundary1_2             = 0x0274,
            ESAU_MovableRegionBoundary4_5             = 0x0280,
            ESAU_MovableRegionBoundary5_6             = 0x0284,
            // Set registers
            IpVersion_Set                             = 0x1000,
            Status_Set                                = 0x1004,
            Lock_Set                                  = 0x1008,
            InterruptFlags_Set                        = 0x100C,
            InterruptEnable_Set                       = 0x1010,
            M33Control_Set                            = 0x1020,
            M33InitNsVector_Set                       = 0x1024,
            M33InitSVector_Set                        = 0x1028,
            PPU_PriviledgedAttribute0_Set             = 0x1040,
            PPU_PriviledgedAttribute1_Set             = 0x1044,
            PPU_SecureAttribute0_Set                  = 0x1060,
            PPU_SecureAttribute1_Set                  = 0x1064,
            PPU_Disable0_Set                          = 0x1120,
            PPU_Disable1_Set                          = 0x1124,
            PPU_FaultStatus_Set                       = 0x1140,
            BMPU_PriviledgedAttribute0_Set            = 0x1150,
            BMPU_SecureAttribute0_Set                 = 0x1170,
            BMPU_FaultStatus_Set                      = 0x1250,
            BMPU_FaultStatusAddress_Set               = 0x1254,
            ESAU_RegionTypes0_Set                     = 0x1260,
            ESAU_RegionTypes1_Set                     = 0x1264,
            ESAU_MovableRegionBoundary0_1_Set         = 0x1270,
            ESAU_MovableRegionBoundary1_2_Set         = 0x1274,
            ESAU_MovableRegionBoundary4_5_Set         = 0x1280,
            ESAU_MovableRegionBoundary5_6_Set         = 0x1284,
            // Clear registers
            IpVersion_Clr                             = 0x2000,
            Status_Clr                                = 0x2004,
            Lock_Clr                                  = 0x2008,
            InterruptFlags_Clr                        = 0x200C,
            InterruptEnable_Clr                       = 0x2010,
            M33Control_Clr                            = 0x2020,
            M33InitNsVector_Clr                       = 0x2024,
            M33InitSVector_Clr                        = 0x2028,
            PPU_PriviledgedAttribute0_Clr             = 0x2040,
            PPU_PriviledgedAttribute1_Clr             = 0x2044,
            PPU_SecureAttribute0_Clr                  = 0x2060,
            PPU_SecureAttribute1_Clr                  = 0x2064,
            PPU_Disable0_Clr                          = 0x2120,
            PPU_Disable1_Clr                          = 0x2124,
            PPU_FaultStatus_Clr                       = 0x2140,
            BMPU_PriviledgedAttribute0_Clr            = 0x2150,
            BMPU_SecureAttribute0_Clr                 = 0x2170,
            BMPU_FaultStatus_Clr                      = 0x2250,
            BMPU_FaultStatusAddress_Clr               = 0x2254,
            ESAU_RegionTypes0_Clr                     = 0x2260,
            ESAU_RegionTypes1_Clr                     = 0x2264,
            ESAU_MovableRegionBoundary0_1_Clr         = 0x2270,
            ESAU_MovableRegionBoundary1_2_Clr         = 0x2274,
            ESAU_MovableRegionBoundary4_5_Clr         = 0x2280,
            ESAU_MovableRegionBoundary5_6_Clr         = 0x2284,
            // Toggle registers
            IpVersion_Tgl                             = 0x3000,
            Status_Tgl                                = 0x3004,
            Lock_Tgl                                  = 0x3008,
            InterruptFlags_Tgl                        = 0x300C,
            InterruptEnable_Tgl                       = 0x3010,
            M33Control_Tgl                            = 0x3020,
            M33InitNsVector_Tgl                       = 0x3024,
            M33InitSVector_Tgl                        = 0x3028,
            PPU_PriviledgedAttribute0_Tgl             = 0x3040,
            PPU_PriviledgedAttribute1_Tgl             = 0x3044,
            PPU_SecureAttribute0_Tgl                  = 0x3060,
            PPU_SecureAttribute1_Tgl                  = 0x3064,
            PPU_Disable0_Tgl                          = 0x3120,
            PPU_Disable1_Tgl                          = 0x3124,
            PPU_FaultStatus_Tgl                       = 0x3140,
            BMPU_PriviledgedAttribute0_Tgl            = 0x3150,
            BMPU_SecureAttribute0_Tgl                 = 0x3170,
            BMPU_FaultStatus_Tgl                      = 0x3250,
            BMPU_FaultStatusAddress_Tgl               = 0x3254,
            ESAU_RegionTypes0_Tgl                     = 0x3260,
            ESAU_RegionTypes1_Tgl                     = 0x3264,
            ESAU_MovableRegionBoundary0_1_Tgl         = 0x3270,
            ESAU_MovableRegionBoundary1_2_Tgl         = 0x3274,
            ESAU_MovableRegionBoundary4_5_Tgl         = 0x3280,
            ESAU_MovableRegionBoundary5_6_Tgl         = 0x3284,
      }
#endregion
    }
}