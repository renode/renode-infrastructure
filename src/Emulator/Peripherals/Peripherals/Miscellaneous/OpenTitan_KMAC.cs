//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_KMAC : IDoubleWordPeripheral, IWordPeripheral, IBytePeripheral, IKnownSize, ISideloadableKey
    {
        public OpenTitan_KMAC()
        {
            KmacDoneIRQ = new GPIO();
            FifoEmptyIRQ = new GPIO();
            KmacErrorIRQ = new GPIO();
            FatalAlert = new GPIO();
            RecoverableAlert = new GPIO();

            keyShare = new byte[NumberOfSecretKeys][];
            for(var i = 0; i < NumberOfSecretKeys; ++i)
            {
                keyShare[i] = new byte[NumberOfRegistersForSecretKey * 4];
            }
            prefix = new byte[NumberOfRegistersForPrefix * 4];
            state = new byte[StateSize];
            stateMask = new byte[StateSize];
            sideloadKey = new byte[ExpectedKeyLength];
            fifo = new Queue<byte>();
            registers = new DoubleWordRegisterCollection(this, BuildRegisterMap());
            previousCommand = Command.Done;
        }

        public uint ReadDoubleWord(long offset)
        {
            if(IsInState(offset))
            {
                return ReadDoubleWordFromState(offset);
            }
            if(IsInFifo(offset))
            {
                return ReadDoubleWordFromFifo(offset);
            }
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            if(IsInState(offset))
            {
                WriteToState(offset, value);
            }
            else if(IsInFifo(offset))
            {
                WriteToFifo(BitConverter.GetBytes(value));
            }
            else
            {
                registers.Write(offset, value);
            }
        }

        public ushort ReadWord(long offset)
        {
            this.Log(LogLevel.Warning, "Tried to read value at offset 0x{0:X}, but word access to this region is not supported", offset);
            return 0x0;
        }

        public void WriteWord(long offset, ushort value)
        {
            if(IsInFifo(offset))
            {
                WriteToFifo(BitConverter.GetBytes(value));
            }
            else
            {
                this.Log(LogLevel.Warning, "Tried to write value 0x{0:X} at offset 0x{1:X}, but word access to this region is not supported", value, offset);
            }
        }

        public byte ReadByte(long offset)
        {
            this.Log(LogLevel.Warning, "Tried to read value at offset 0x{0:X}, but byte access to this region is not supported", offset);
            return 0x0;
        }

        public void WriteByte(long offset, byte value)
        {
            if(IsInFifo(offset))
            {
                WriteToFifo(new byte[] { value });
            }
            else
            {
                this.Log(LogLevel.Warning, "Tried to write value 0x{0:X} at offset 0x{1:X}, but byte access to this region is not supported", value, offset);
            }
        }

        public void Reset()
        {
            registers.Reset();
            fifo.Clear();
            for(var i = 0; i < NumberOfSecretKeys; ++i)
            {
                Array.Clear(keyShare[i], 0, keyShare[i].Length);
            }
            Array.Clear(prefix, 0, prefix.Length);
            Array.Clear(state, 0, state.Length);
            Array.Clear(stateMask, 0, stateMask.Length);
            sideloadKey = new byte[ExpectedKeyLength];
            UpdateInterrupts();
            FatalAlert.Unset();
            RecoverableAlert.Unset();

            previousCommand = Command.Done;
            stateBuffer = null;
            ClearHasher();
        }

        public long Size => 0x1000;

        public GPIO KmacDoneIRQ { get; }

        public GPIO FifoEmptyIRQ { get; }

        public GPIO KmacErrorIRQ { get; }

        public GPIO FatalAlert { get; }

        public GPIO RecoverableAlert { get; }

        public IEnumerable<byte> SideloadKey
        {
            set
            {
                var tempKey = value.ToArray();
                if(tempKey.Length != ExpectedKeyLength)
                {
                    throw new RecoverableException($"Key has invalid length {tempKey.Length}, expected {ExpectedKeyLength}, ignoring write");
                }
                sideloadKey = tempKey;
            }
        }

        private static bool TryDecodeOutputLength(byte[] data, out int length)
        {
            if(data == null || data.Length < 1)
            {
                length = default(int);
                return false;
            }
            var lengthSize = data[data.Length - 1];
            if(lengthSize < 1 || lengthSize > 4)
            {
                length = default(int);
                return false;
            }

            var i = data.Length - lengthSize - 1;
            if(i < 0)
            {
                length = default(int);
                return false;
            }

            length = 0;
            for(; i < data.Length - 1; ++i)
            {
                length = (length << 8) | data[i];
            }
            // encoded length is in bits, return length in bytes
            length /= 8;
            return true;
        }

        private static bool TryLeftDecode(byte[] data, int offset, out byte[] str, out int bytesUsed)
        {
            // this function assumes max stringLengthSize of 2
            bytesUsed = 0;
            if(offset < 0 || offset >= data.Length)
            {
                str = default(byte[]);
                return false;
            }

            var stringLengthSize = (int)data[offset];
            if(stringLengthSize < 1 || stringLengthSize > 2 || data.Length - offset < stringLengthSize + 1)
            {
                str = default(byte[]);
                return false;
            }

            var stringLength = (int)data[offset + 1];
            if(stringLengthSize == 2)
            {
                stringLength = stringLength << 8 | data[offset + 2];
            }
            stringLength /= 8;
            bytesUsed = 1 + stringLengthSize + stringLength;

            if(data.Length < offset + bytesUsed)
            {
                bytesUsed = 0;
                str = default(byte[]);
                return false;
            }

            str = data.Skip(offset + 1 + stringLengthSize).Take(stringLength).ToArray();
            return true;
        }

        private static readonly byte[] kmacFunctionName = new byte[] { 0x4B, 0x4D, 0x41, 0x43 }; // KMAC

        private uint ReadDoubleWordFromState(long offset)
        {
            if(offset >= (long)Registers.State && offset < (long)Registers.State + StateSize)
            {
                return (uint)BitConverter.ToInt32(state, (int)offset - (int)Registers.State);
            }
            else if(offset >= (long)Registers.StateMask && offset < (long)Registers.StateMask + StateSize)
            {
                return (uint)BitConverter.ToInt32(stateMask, (int)offset - (int)Registers.StateMask);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled read from state at 0x{0:X}", offset);
                return 0x0;
            }
        }

        private void WriteToState(long offset, uint value)
        {
            if(offset >= (long)Registers.State && offset < (long)Registers.State + StateSize)
            {
                state.SetBytesFromValue(value, (int)offset - (int)Registers.State);
            }
            else if(offset >= (long)Registers.StateMask && offset < (long)Registers.StateMask + StateSize)
            {
                stateMask.SetBytesFromValue(value, (int)offset - (int)Registers.StateMask);
            }
            else
            {
                this.Log(LogLevel.Warning, "Unhandled write to state at 0x{0:X}, value 0x{1:X}", offset, value);
            }
        }

        private uint ReadDoubleWordFromFifo(long offset)
        {
            this.Log(LogLevel.Warning, "Tried to read from fifo at 0x{0:X}, but this region is write only", offset);
            return 0x0;
        }

        private void WriteToFifo(byte[] values)
        {
            foreach(var b in values)
            {
                if(sha3Absorb.Value || fifo.Count < FifoMaxCount)
                {
                    fifo.Enqueue(b);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Attempted write to full fifo, value 0x{0:X}", b);
                }
            }
        }

        private Dictionary<long, DoubleWordRegister> BuildRegisterMap()
        {
            var registersDictionary = new Dictionary<long, DoubleWordRegister>
            {
                {(long)Registers.InterruptState, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptKmacDone, FieldMode.Read | FieldMode.WriteOneToClear, name: "kmac_done")
                    .WithFlag(1, out interruptFifoEmpty, FieldMode.Read | FieldMode.WriteOneToClear, name: "fifo_empty")
                    .WithFlag(2, out interruptKmacError, FieldMode.Read | FieldMode.WriteOneToClear, name: "kmac_err")
                    .WithReservedBits(3, 29)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptEnable, new DoubleWordRegister(this)
                    .WithFlag(0, out interruptEnableKmacDone, name: "kmac_done")
                    .WithFlag(1, out interruptEnableFifoEmpty, name: "fifo_empty")
                    .WithFlag(2, out interruptEnableKmacError, name: "kmac_err")
                    .WithReservedBits(3, 29)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.InterruptTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { interruptKmacDone.Value |= val; }, name: "kmac_done")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { interruptFifoEmpty.Value |= val; }, name: "fifo_empty")
                    .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { interruptKmacError.Value |= val; }, name: "kmac_err")
                    .WithReservedBits(3, 29)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.AlertTest, new DoubleWordRegister(this)
                    .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov_alert")
                    .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_fault")
                    .WithReservedBits(2, 30)
                },
                {(long)Registers.ConfigurationWriteEnable, new DoubleWordRegister(this)
                    .WithTaggedFlag("en", 0)
                    .WithReservedBits(1, 31)
                },
                {(long)Registers.Configuration, new DoubleWordRegister(this)
                    .WithFlag(0, out kmacEnable, name: "kmac_en")
                    .WithEnumField<DoubleWordRegister, HashingStrength>(1, 3, out hashingStrength, name: "kstrength")
                    .WithEnumField<DoubleWordRegister, HashingMode>(4, 2, out hashingMode, name: "mode")
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("msg_endianness", 8)
                    .WithTaggedFlag("state_endianness", 9)
                    .WithReservedBits(10, 2)
                    .WithFlag(12, out useSideloadedKey, name: "sideload")
                    .WithReservedBits(13, 3)
                    .WithTag("entropy_mode", 16, 2)
                    .WithReservedBits(18, 1)
                    .WithTaggedFlag("entropy_fast_process", 19)
                    .WithTaggedFlag("msg_mask", 20)
                    .WithReservedBits(21, 3)
                    .WithTaggedFlag("entropy_ready", 24)
                    .WithTaggedFlag("err_processed", 25)
                    .WithTaggedFlag("en_unsupported_modestrength", 26)
                    .WithReservedBits(27, 5)
                },
                {(long)Registers.Command, new DoubleWordRegister(this)
                    .WithEnumField<DoubleWordRegister, Command>(0, 6, writeCallback: (_, val) => { RunCommand(val); }, valueProviderCallback: _ => 0x0, name: "cmd")
                    .WithReservedBits(6, 2)
                    .WithTaggedFlag("entropy_req", 8)
                    .WithTaggedFlag("hash_cnt_clr", 9)
                    .WithReservedBits(10, 22)
                    .WithWriteCallback((_, __) => UpdateInterrupts())
                },
                {(long)Registers.Status, new DoubleWordRegister(this, 0x4001)
                    .WithFlag(0, out sha3Idle, name: "sha3_idle")
                    .WithFlag(1, out sha3Absorb, name: "sha3_absorb")
                    .WithFlag(2, out sha3Squeeze, name: "sha3_squeeze")
                    .WithReservedBits(3, 5)
                    .WithValueField(8, 5, valueProviderCallback: _ => sha3Absorb.Value ? 0 : (uint)fifo.Count / 8, name: "fifo_depth")
                    .WithReservedBits(13, 1)
                    .WithFlag(14, valueProviderCallback: _ => fifo.Count == 0 || sha3Absorb.Value, name: "fifo_empty")
                    .WithFlag(15, valueProviderCallback: _ => fifo.Count == FifoMaxCount, name: "fifo_full")
                    .WithReservedBits(16, 16)
                },
                {(long)Registers.EntropyTimerPeriods, new DoubleWordRegister(this)
                    .WithTag("prescaler", 0, 10)
                    .WithReservedBits(10, 6)
                    .WithTag("wait_timer", 16, 16)
                },
                {(long)Registers.EntropyRefreshCounter, new DoubleWordRegister(this)
                    .WithTag("hash_cnt", 0, 10)
                    .WithReservedBits(10, 22)
                },
                {(long)Registers.EntropyRefreshThreshold, new DoubleWordRegister(this)
                    .WithTag("threshold", 0, 10)
                    .WithReservedBits(10, 22)
                },
                {(long)Registers.EntropySeed0, new DoubleWordRegister(this)
                    .WithTag("seed", 0, 32)
                },
                {(long)Registers.EntropySeed1, new DoubleWordRegister(this)
                    .WithTag("seed", 0, 32)
                },
                {(long)Registers.EntropySeed2, new DoubleWordRegister(this)
                    .WithTag("seed", 0, 32)
                },
                {(long)Registers.EntropySeed3, new DoubleWordRegister(this)
                    .WithTag("seed", 0, 32)
                },
                {(long)Registers.EntropySeed4, new DoubleWordRegister(this)
                    .WithTag("seed", 0, 32)
                },
                // KeyShareN_M Registers
                {(long)Registers.KeyLength, new DoubleWordRegister(this)
                // KeySh(PrefixN, 0, prefix.Length); Registers
                    .WithEnumField<DoubleWordRegister, KeyLength>(0, 3, out keyLength, name: "len")
                    .WithReservedBits(3, 29)
                },
                {(long)Registers.ErrorCode, new DoubleWordRegister(this)
                    .WithValueField(0, 32, out errorCode, name: "err_code")
                },
            };

            for(var jj = 0; jj < NumberOfSecretKeys; ++jj)
            {
                var j = jj;
                var offset = Registers.KeyShare1_0 - Registers.KeyShare0_0;
                for(var ii = 0; ii < NumberOfRegistersForSecretKey; ++ii)
                {
                    var i = ii;
                    registersDictionary.Add((long)Registers.KeyShare0_0 + i * 4 + offset * j, new DoubleWordRegister(this)
                        .WithValueField(0, 32,
                            writeCallback: (_, val) => { keyShare[j].SetBytesFromValue((uint)val, i * 4); },
                            valueProviderCallback: _ => (uint)BitConverter.ToInt32(keyShare[j], i * 4), name: $"key_{i}")
                    );
                }
            }

            for(var ii = 0; ii < NumberOfRegistersForPrefix; ++ii)
            {
                var i = ii;
                registersDictionary.Add((long)Registers.Prefix0 + i * 4, new DoubleWordRegister(this)
                    .WithValueField(0, 32,
                        writeCallback: (_, val) => { prefix.SetBytesFromValue((uint)val, i * 4); },
                        valueProviderCallback: _ => (uint)BitConverter.ToInt32(prefix, i * 4), name: $"prefix_{i}")
                );
            }

            return registersDictionary;
        }

        private void UpdateInterrupts()
        {
            KmacDoneIRQ.Set(interruptKmacDone.Value && interruptEnableKmacDone.Value);
            FifoEmptyIRQ.Set(interruptFifoEmpty.Value && interruptEnableFifoEmpty.Value);
            KmacErrorIRQ.Set(interruptKmacError.Value && interruptEnableKmacError.Value);
        }

        private void RunCommand(Command command)
        {
            CheckComandSequence(command);
            var written = state.Length;
            switch(command)
            {
                case Command.None:
                    return;
                case Command.Start:
                    ClearHasher();
                    if(!CheckModeAndStrength())
                    {
                        errorCode.Value = (uint)ErrorCode.UnexpectedModeStrength;
                        interruptKmacError.Value = true;
                        this.Log(LogLevel.Warning, "Failed to run `Start` command, unexpexted mode strength");
                    }
                    else
                    {
                        InitHasher();
                    }
                    sha3Absorb.Value = true;
                    sha3Squeeze.Value = false;
                    break;
                case Command.Process:
                    var data = fifo.ToArray();
                    fifo.Clear();
                    written = RunFirstHasher(data);
                    sha3Absorb.Value = false;
                    sha3Squeeze.Value = true;
                    break;
                case Command.Run:
                    written = RunHasher();
                    break;
                case Command.Done:
                    sha3Absorb.Value = false;
                    sha3Squeeze.Value = false;
                    ClearHasher();
                    break;
                default:
                    this.Log(LogLevel.Warning, "Incorrect command 0x{0:X}", command);
                    return;
            }
            Array.Clear(state, written, state.Length - written);
        }

        private void InitHasher()
        {
            if(kmacEnable.Value)
            {
                if(TryDecodePrefix(out var functionName, out var customization) && functionName.SequenceEqual(kmacFunctionName))
                {
                    kmac = new KMac(HashBitLength, customization);
                }
                else
                {
                    errorCode.Value = (uint)ErrorCode.IncorrectFunctionName;
                    interruptKmacError.Value = true;
                    this.Log(LogLevel.Warning, "Failed to run `Start` command, incorrect function name in KMAC mode");
                }
                return;
            }

            switch(hashingMode.Value)
            {
                case HashingMode.SHA3:
                    sha3 = new Sha3Digest(HashBitLength);
                    break;
                case HashingMode.SHAKE:
                    shake = new ShakeDigest(HashBitLength);
                    break;
                case HashingMode.CSHAKE:
                    if(TryDecodePrefix(out var functionName, out var customization))
                    {
                        cshake = new CShakeDigest(HashBitLength, functionName, customization);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Failed to run `Start` command, unexpexted prefix value for cSHAKE");
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Hashing mode is in reserved state");
                    break;
            }
        }

        private int RunFirstHasher(byte[] data)
        {
            if(kmacEnable.Value)
            {
                if(kmac == null)
                {
                    this.Log(LogLevel.Warning, "Attempted to run `Process` command in KMAC mode after failed initialization");
                    return 0;
                }

                if(TryDecodeOutputLength(data, out var outputLength))
                {
                    kmac.Init(new KeyParameter(Key.Take(KeyLengthInBytes).ToArray()));
                    // remove output length bytes
                    data = data.Take(data.Length - data[data.Length - 1] - 1).ToArray();
                    kmac.BlockUpdate(data, 0, data.Length);

                    if(outputLength != 0) // fixed-length output
                    {
                        var output = new byte[outputLength];
                        kmac.DoFinal(output, 0, outputLength);
                        stateBuffer = new Queue<byte[]>(output.Split(kmac.GetByteLength()));
                        var buffer = stateBuffer.Dequeue();
                        Array.Copy(buffer, state, buffer.Length);
                        kmac = null;
                        return buffer.Length;
                    }
                    else // arbitrary-length output
                    {
                        stateBuffer = null;
                        return kmac.DoOutput(state, 0, kmac.GetByteLength());
                    }
                }

                this.Log(LogLevel.Warning, "Unexpexted data in KMAC mode");
                return 0;
            }

            switch(hashingMode.Value)
            {
                case HashingMode.SHA3:
                    if(sha3 != null)
                    {
                        sha3.BlockUpdate(data, 0, data.Length);
                        return sha3.DoFinal(state, 0);
                    }
                    break;
                case HashingMode.SHAKE:
                    if(shake != null)
                    {
                        shake.BlockUpdate(data, 0, data.Length);
                        return shake.DoOutput(state, 0, shake.GetByteLength());
                    }
                    break;
                case HashingMode.CSHAKE:
                    if(cshake != null)
                    {
                        cshake.BlockUpdate(data, 0, data.Length);
                        return cshake.DoOutput(state, 0, cshake.GetByteLength());
                    }
                    break;
                default:
                    this.Log(LogLevel.Warning, "Hashing mode is in reserved state");
                    return 0;
            }

            this.Log(LogLevel.Warning, "Attempted to run `Process` command in {0} after failed initialization", hashingMode.Value);
            return 0;
        }

        private int RunHasher()
        {
            if(kmacEnable.Value)
            {
                if(stateBuffer != null && stateBuffer.Count > 0)
                {
                    var buffer = stateBuffer.Dequeue();
                    Array.Copy(buffer, state, buffer.Length);
                    return buffer.Length;
                }

                if(kmac != null)
                {
                    return kmac.DoOutput(state, 0, kmac.GetByteLength());
                }

                this.Log(LogLevel.Warning, "No digest data available for `Run` command in KMAC mode");
                return 0;
            }

            switch(hashingMode.Value)
            {
                case HashingMode.SHAKE:
                    if(shake != null)
                    {
                        return shake.DoOutput(state, 0, shake.GetByteLength());
                    }
                    break;
                case HashingMode.CSHAKE:
                    if(cshake != null)
                    {
                        return cshake.DoOutput(state, 0, cshake.GetByteLength());
                    }
                    break;
                default:
                    errorCode.Value = (uint)ErrorCode.SwCmdSequence | (uint)Command.Run;
                    interruptKmacError.Value = true;
                    this.Log(LogLevel.Warning, "Unexpected hashing mode ({0}) for `Run` command", hashingMode.Value);
                    return 0;
            }

            this.Log(LogLevel.Warning, "Attempted to run `Run` command in {0} after failed initialization", hashingMode.Value);
            return 0;
        }

        private void ClearHasher()
        {
            sha3 = null;
            shake = null;
            cshake = null;
            kmac = null;
        }

        private void CheckComandSequence(Command command)
        {
            var error = false;
            switch(command)
            {
                case Command.None:
                    return;
                case Command.Start:
                    error = previousCommand != Command.Done;
                    break;
                case Command.Process:
                    error = previousCommand != Command.Start;
                    break;
                case Command.Run:
                case Command.Done:
                    error = previousCommand != Command.Process && previousCommand != Command.Run;
                    break;
                default:
                    error = true;
                    break;
            }
            errorCode.Value = (uint)ErrorCode.SwCmdSequence | (uint)command;
            interruptKmacError.Value = true;
            previousCommand = command;
        }

        private bool CheckModeAndStrength()
        {
            var mode = kmacEnable.Value ? HashingMode.CSHAKE : hashingMode.Value;
            switch(mode)
            {
                case HashingMode.SHA3:
                    switch(HashBitLength)
                    {
                        case 224:
                        case 256:
                        case 384:
                        case 512:
                            return true;
                        default:
                            return false;
                    }
                case HashingMode.SHAKE:
                case HashingMode.CSHAKE:
                    switch(HashBitLength)
                    {
                        case 128:
                        case 256:
                            return true;
                        default:
                            return false;
                    }
                default:
                    return false;
            }
        }

        private bool TryDecodePrefix(out byte[] functionName, out byte[] customization)
        {
            if(!TryLeftDecode(prefix, 0, out functionName, out var bytesUsed) || bytesUsed > NumberOfRegistersForPrefix * 4 - 2)
            {
                customization = default(byte[]);
                return false;
            }

            return TryLeftDecode(prefix, bytesUsed, out customization, out bytesUsed);
        }

        private bool IsInState(long offset)
        {
            return offset >= (long)Registers.State && offset < (long)Registers.State + StateLength;
        }

        private bool IsInFifo(long offset)
        {
            return offset >= (long)Registers.Fifo && offset < (long)Registers.Fifo + FifoLength;
        }

        private IEnumerable<byte> Key
        {
            get
            {
                if(useSideloadedKey.Value)
                {
                    return sideloadKey;
                }
                else
                {
                    return keyShare[0];
                }
            }
        }

        private int HashBitLength
        {
            get
            {
                switch(hashingStrength.Value)
                {
                    case HashingStrength.L128:
                        return 128;
                    case HashingStrength.L224:
                        return 224;
                    case HashingStrength.L256:
                        return 256;
                    case HashingStrength.L384:
                        return 384;
                    case HashingStrength.L512:
                        return 512;
                    default:
                        this.Log(LogLevel.Warning, "Hashing strength set to reserved value, 0x{0:X}", hashingStrength.Value);
                        return 0;
                }
            }
        }

        private int KeyLengthInBytes
        {
            get
            {
                switch(keyLength.Value)
                {
                    case KeyLength.Key128:
                        return 128 / 8;
                    case KeyLength.Key192:
                        return 192 / 8;
                    case KeyLength.Key256:
                        return 256 / 8;
                    case KeyLength.Key384:
                        return 284 / 8;
                    case KeyLength.Key512:
                        return 512 / 8;
                    default:
                        this.Log(LogLevel.Warning, "Key length set to reserved value, 0x{0:X}", keyLength.Value);
                        return 0;
                }
            }
        }

        private IFlagRegisterField interruptKmacDone;
        private IFlagRegisterField interruptFifoEmpty;
        private IFlagRegisterField interruptKmacError;
        private IFlagRegisterField interruptEnableKmacDone;
        private IFlagRegisterField interruptEnableFifoEmpty;
        private IFlagRegisterField interruptEnableKmacError;
        private IFlagRegisterField kmacEnable;
        private IFlagRegisterField useSideloadedKey;
        private IFlagRegisterField sha3Idle;
        private IFlagRegisterField sha3Absorb;
        private IFlagRegisterField sha3Squeeze;
        private IEnumRegisterField<HashingStrength> hashingStrength;
        private IEnumRegisterField<HashingMode> hashingMode;
        private IEnumRegisterField<KeyLength> keyLength;
        private IValueRegisterField errorCode;
        private Command previousCommand;
        private byte[] sideloadKey;
        private Queue<byte[]> stateBuffer;
        private byte[] stateMask;
        private Sha3Digest sha3;
        private ShakeDigest shake;
        private CShakeDigest cshake;
        private KMac kmac;

        private readonly DoubleWordRegisterCollection registers;
        private readonly byte[][] keyShare;
        private readonly byte[] prefix;
        private readonly byte[] state;
        private readonly Queue<byte> fifo;

        private const int NumberOfRegistersForSecretKey = 16;
        private const int NumberOfRegistersForPrefix = 11;
        private const int NumberOfSecretKeys = 2;
        private const int StateLength = 0x200;
        private const int FifoLength = 0x800;
        private const int ExpectedKeyLength = 64;
        private const int StateSize = 0xC8;
        private const int FifoMaxCount = 8 * 9;

        private enum HashingStrength
        {
            L128 = 0,
            L224 = 1,
            L256 = 2,
            L384 = 3,
            L512 = 4
        }

        private enum KeyLength
        {
            Key128 = 0,
            Key192 = 1,
            Key256 = 2,
            Key384 = 3,
            Key512 = 4,
        }

        private enum HashingMode
        {
            SHA3   = 0,
            SHAKE  = 2,
            CSHAKE = 3
        }

        private enum Command
        {
            None    = 0x0,
            Start   = 0x1d,
            Process = 0x2e,
            Run     = 0x31,
            Done    = 0x16
        }

        private enum ErrorCode
        {
            KeyNotValid             = 0x01,
            SwPushedMsgFifo         = 0x02,
            SwIssuedCmdInAppActive  = 0x03,
            WaitTimerExpired        = 0x04,
            IncorrectEntropyMode    = 0x05,
            UnexpectedModeStrength  = 0x06,
            IncorrectFunctionName   = 0x07,
            SwCmdSequence           = 0x08,
            Sha3Control             = 0x80,
        }

        private enum Registers : long
        {
            InterruptState              = 0x00,
            InterruptEnable             = 0x04,
            InterruptTest               = 0x08,
            AlertTest                   = 0x0C,
            ConfigurationWriteEnable    = 0x10,
            Configuration               = 0x14,
            Command                     = 0x18,
            Status                      = 0x1C,
            EntropyTimerPeriods         = 0x20,
            EntropyRefreshCounter       = 0x24,
            EntropyRefreshThreshold     = 0x28,
            EntropySeed0                = 0x2C,
            EntropySeed1                = 0x30,
            EntropySeed2                = 0x34,
            EntropySeed3                = 0x38,
            EntropySeed4                = 0x3C,
            KeyShare0_0                 = 0x40,
            KeyShare0_1                 = 0x44,
            KeyShare0_2                 = 0x48,
            KeyShare0_3                 = 0x4C,
            KeyShare0_4                 = 0x50,
            KeyShare0_5                 = 0x54,
            KeyShare0_6                 = 0x58,
            KeyShare0_7                 = 0x5C,
            KeyShare0_8                 = 0x60,
            KeyShare0_9                 = 0x64,
            KeyShare0_10                = 0x68,
            KeyShare0_11                = 0x6C,
            KeyShare0_12                = 0x70,
            KeyShare0_13                = 0x74,
            KeyShare0_14                = 0x78,
            KeyShare0_15                = 0x7C,
            KeyShare1_0                 = 0x80,
            KeyShare1_1                 = 0x84,
            KeyShare1_2                 = 0x88,
            KeyShare1_3                 = 0x8C,
            KeyShare1_4                 = 0x90,
            KeyShare1_5                 = 0x94,
            KeyShare1_6                 = 0x98,
            KeyShare1_7                 = 0x9C,
            KeyShare1_8                 = 0xA0,
            KeyShare1_9                 = 0xA4,
            KeyShare1_10                = 0xA8,
            KeyShare1_11                = 0xAC,
            KeyShare1_12                = 0xB0,
            KeyShare1_13                = 0xB4,
            KeyShare1_14                = 0xB8,
            KeyShare1_15                = 0xBC,
            KeyLength                   = 0xC0,
            Prefix0                     = 0xC4,
            Prefix1                     = 0xC8,
            Prefix2                     = 0xCC,
            Prefix3                     = 0xD0,
            Prefix4                     = 0xD4,
            Prefix5                     = 0xD8,
            Prefix6                     = 0xDC,
            Prefix7                     = 0xE0,
            Prefix8                     = 0xE4,
            Prefix9                     = 0xE8,
            Prefix10                    = 0xEC,
            ErrorCode                   = 0xF0,
            State                       = 0x400,
            StateMask                   = 0x500,
            Fifo                        = 0x800
        }
    }
}
