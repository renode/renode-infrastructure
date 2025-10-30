//
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
    public abstract class SiLabs_BUFC_Base : SiLabsPeripheral
    {
        public SiLabs_BUFC_Base(Machine machine, uint numberOfBuffers) : base(machine, false)
        {
            this.numberOfBuffers = numberOfBuffers;

            buffer = new BUFC_Buffer[numberOfBuffers];
            for(var i = 0u; i < numberOfBuffers; ++i)
            {
                buffer[i] = new BUFC_Buffer(this, machine, i);
            }

            IRQ = new GPIO();
            SequencerIRQ = new GPIO();

            registersCollection = BuildRegistersCollection();
        }

        public uint Peek(uint bufferIndex, uint byteIndex)
        {
            CheckBufferIndex(bufferIndex);

            return buffer[bufferIndex].Peek(byteIndex);
        }

        public bool TryReadBytes(uint bufferIndex, uint length, out byte[] data)
        {
            CheckBufferIndex(bufferIndex);

            return buffer[bufferIndex].TryReadBytes(length, out data);
        }

        public bool TryWriteBytes(uint bufferIndex, byte[] data, out uint i)
        {
            CheckBufferIndex(bufferIndex);

            return buffer[bufferIndex].TryWriteBytes(data, out i);
        }

        public void WriteData(uint bufferIndex, uint data)
        {
            CheckBufferIndex(bufferIndex);

            buffer[bufferIndex].WriteData = data;
        }

        public bool Overflow(uint bufferIndex)
        {
            CheckBufferIndex(bufferIndex);

            return buffer[bufferIndex].Field_6.Value;
        }

        public void UpdateWriteStartOffset(uint bufferIndex)
        {
            CheckBufferIndex(bufferIndex);

            buffer[bufferIndex].UpdateWriteStartOffset();
        }

        public void RestoreWriteOffset(uint bufferIndex)
        {
            CheckBufferIndex(bufferIndex);

            buffer[bufferIndex].RestoreWriteOffset();
        }

        public uint WriteOffset(uint bufferIndex)
        {
            CheckBufferIndex(bufferIndex);

            return buffer[bufferIndex].WriteOffset;
        }

        public GPIO IRQ { get; }

        public GPIO SequencerIRQ { get; }

        protected readonly BUFC_Buffer[] buffer;

        private void CheckBufferIndex(uint bufferIndex)
        {
            if(bufferIndex >= numberOfBuffers)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferIndex), "Invalid buffer index");
            }
        }

        private readonly uint numberOfBuffers;

        public enum BUFC_SizeMode
        {
            Size64   = 0x0,
            Size128  = 0x1,
            Size256  = 0x2,
            Size512  = 0x3,
            Size1024 = 0x4,
            Size2048 = 0x5,
            Size4096 = 0x6,
        }

        public enum BUFC_ThresholdMode
        {
            Larger      = 0x0,
            LessOrEqual = 0x1,
        }

        protected class BUFC_Buffer
        {
            public BUFC_Buffer(SiLabs_BUFC_Base parent, Machine machine, uint index)
            {
                this.parent = parent;
                this.machine = machine;
                this.index = index;
            }

            public uint Peek(uint index)
            {
                var offset = (ulong)(Address + ((ReadOffset + index) % Size));

                return machine.SystemBus.ReadByte(offset);
            }

            public bool TryReadBytes(uint length, out byte[] data)
            {
                bool savedUnderflowValue = Field_23.Value;
                Field_23.Value = false;
                bool retValue = true;
                data = new byte[length];
                var i = 0;
                for(; i < data.Length; ++i)
                {
                    var value = (byte)ReadData;
                    if(Field_23.Value)
                    {
                        retValue = false;
                        break;
                    }
                    data[i] = value;
                }
                if(i != length)
                {
                    Array.Resize(ref data, i);
                }
                if(savedUnderflowValue)
                {
                    Field_23.Value = true;
                }
                return retValue;
            }

            public bool TryWriteBytes(byte[] data, out uint i)
            {
                bool savedOverflowValue = Field_6.Value;
                Field_6.Value = false;
                bool retValue = true;
                for(i = 0; i < data.Length; ++i)
                {
                    WriteData = data[i];
                    if(Field_6.Value)
                    {
                        retValue = false;
                        break;
                    }
                }
                if(savedOverflowValue)
                {
                    Field_6.Value = true;
                }
                return retValue;
            }

            public void Clear()
            {

                readOffset = 0;
                writeOffset = 0;
                UpdateThresholdFlag();
            }

            public void Prefetch()
            {
                if(BytesNumber == 0)
                {

                    Field_23.Value = true;
                    Field_16.Value = true;
                    parent.UpdateInterrupts();
                }
            }

            public void UpdateWriteStartOffset()
            {
                Field_24.Value = WriteOffset;
            }

            public void RestoreWriteOffset()
            {
                WriteOffset = (uint)Field_24.Value;
                UpdateThresholdFlag();
            }

            public void UpdateThresholdFlag()
            {
                if(ThresholdFlag)
                {
                    Field_19.Value = true;
                    Field_14.Value = true;

                    parent.UpdateInterrupts();
                }
            }

            public bool ReadReady => true;

            public bool Read32Ready => true;

            public uint Size => 64u << (int)Field_17.Value;

            public uint Address
            {
                get
                {
                    return address;
                }

                set
                {
                    address = value;
                }
            }

            public uint ReadOffset
            {
                get
                {
                    return readOffset;
                }

                set
                {
                    readOffset = value;
                    UpdateThresholdFlag();
                }
            }

            public uint WriteOffset
            {
                get
                {
                    return writeOffset;
                }

                set
                {
                    writeOffset = value;
                    UpdateThresholdFlag();
                }
            }

            public uint ReadData
            {
                get
                {
                    if(BytesNumber == 0)
                    {
                        Field_23.Value = true;
                        Field_16.Value = true;
                        parent.UpdateInterrupts();
                        return 0;
                    }

                    var offset = (ulong)(Address + (ReadOffset % Size));
                    var value = machine.SystemBus.ReadByte(offset);
                    var newOffset = (ReadOffset + 1) % (2 * Size);
                    ReadOffset = newOffset;
                    return value;
                }
            }

            public uint ReadData32
            {
                get
                {
                    var offset = (ulong)(Address + (ReadOffset % Size));
                    uint value = 0;

                    if(BytesNumber < 4)
                    {
                        Field_23.Value = true;
                        Field_16.Value = true;
                        var bytesRead = BytesNumber;
                        for(uint i = 0; i < bytesRead; i++)
                        {
                            value = value | ((uint)machine.SystemBus.ReadByte(offset + i) << 8 * (int)i);
                        }
                        parent.UpdateInterrupts();
                    }
                    else
                    {
                        value = machine.SystemBus.ReadDoubleWord(offset);
                        var newOffset = (ReadOffset + 4) % (2 * Size);
                        ReadOffset = newOffset;
                    }

                    return value;
                }
            }

            public uint WriteData
            {
                set
                {
                    if(BytesNumber == Size)
                    {
                        Field_6.Value = true;
                        Field_12.Value = true;
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    machine.SystemBus.WriteByte(offset, (byte)value);
                    var newOffset = (WriteOffset + 1) % (2 * Size);
                    WriteOffset = newOffset;
                }
            }

            public uint WriteData32
            {
                set
                {
                    if(BytesNumber > Size - 4)
                    {
                        Field_6.Value = true;
                        Field_12.Value = true;
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    machine.SystemBus.WriteDoubleWord(offset, value);
                    var newOffset = (WriteOffset + 4) % (2 * Size);
                    WriteOffset = newOffset;
                }
            }

            public uint XorWriteData
            {
                set
                {
                    if(BytesNumber == Size)
                    {
                        Field_6.Value = true;
                        Field_12.Value = true;
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    var oldData = machine.SystemBus.ReadByte(offset);
                    var newData = (byte)(oldData ^ (byte)value);
                    machine.SystemBus.WriteByte(offset, newData);
                    var newOffset = (WriteOffset + 1) % (2* Size);
                    WriteOffset = newOffset;
                }
            }

            public uint XorWriteData32
            {
                set
                {
                    if(BytesNumber > (Size - 4))
                    {
                        Field_6.Value = true;
                        Field_12.Value = true;
                        parent.UpdateInterrupts();
                        return;
                    }

                    var offset = (ulong)(Address + (WriteOffset % Size));
                    var oldData = machine.SystemBus.ReadDoubleWord(offset);
                    var newData = oldData ^ value;
                    machine.SystemBus.WriteDoubleWord(offset, newData);
                    var newOffset = (WriteOffset + 4) % (2 * Size);
                    WriteOffset = newOffset;
                }
            }

            public uint BytesNumber
            {
                get
                {
                    return (uint)((WriteOffset - ReadOffset) % (2 * Size));
                }
            }

            public bool ThresholdFlag
            {
                get
                {
                    bool flag = false;

                    switch(Field_21.Value)
                    {
                    case BUFC_ThresholdMode.Larger:
                        if(BytesNumber > Field_20.Value)
                        {
                            flag = true;
                        }
                        break;
                    case BUFC_ThresholdMode.LessOrEqual:
                        if(BytesNumber <= Field_20.Value)
                        {
                            flag = true;
                        }
                        break;
                    }

                    return flag;
                }
            }

            public bool Interrupt => ((Field_2.Value && Field_1.Value)
                                      || (Field_19.Value && Field_18.Value)
                                      || (Field_23.Value && Field_22.Value)
                                      || (Field_6.Value && Field_5.Value)
                                      || (Field_4.Value && Field_3.Value));

            public bool SeqInterrupt => ((Field_8.Value && Field_7.Value)
                                         || (Field_14.Value && Field_13.Value)
                                         || (Field_16.Value && Field_15.Value)
                                         || (Field_12.Value && Field_11.Value)
                                         || (Field_10.Value && Field_9.Value));

            public IFlagRegisterField Field_2;
            public IFlagRegisterField Field_1;
            public IFlagRegisterField Field_19;
            public IFlagRegisterField Field_18;
            public IFlagRegisterField Field_23;
            public IFlagRegisterField Field_22;
            public IFlagRegisterField Field_6;
            public IFlagRegisterField Field_5;
            public IFlagRegisterField Field_4;
            public IFlagRegisterField Field_3;
            public IFlagRegisterField Field_8;
            public IFlagRegisterField Field_7;
            public IFlagRegisterField Field_14;
            public IFlagRegisterField Field_13;
            public IFlagRegisterField Field_16;
            public IFlagRegisterField Field_15;
            public IFlagRegisterField Field_12;
            public IFlagRegisterField Field_11;
            public IFlagRegisterField Field_10;
            public IFlagRegisterField Field_9;
            public IEnumRegisterField<BUFC_SizeMode> Field_17;
            public IValueRegisterField Field_24;
            public IEnumRegisterField<BUFC_ThresholdMode> Field_21;
            public IValueRegisterField Field_20;
            private uint address;
            private uint readOffset;
            private uint writeOffset;
            private readonly SiLabs_BUFC_Base parent;
            private readonly Machine machine;
            private readonly uint index;
        }
    }
}