//
// Copyright (c) 2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
using System;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Peripherals.Utilities
{
    public class ByteArrayWithAccessTracking
    {
        public ByteArrayWithAccessTracking(IPeripheral parent, uint partsCount, uint accessByteWidth, string name)
        {
            if(accessByteWidth != 4)
            {
                throw new ArgumentException("Access widths other than 4 are not supported yet");
            }
            this.parent = parent;
            this.name = name;
            this.accessByteWidth = accessByteWidth;
            this.partsCount = partsCount;
            array = new byte[partsCount * accessByteWidth];
            partAccessed = new StateTracker[partsCount];

            Reset();
        }

        public void Reset()
        {
            ClearAccessTracking();
            Array.Clear(array, 0, array.Length);
        }

        public void SetPart(uint part, uint value)
        {
            if(part >= partsCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            if(AllDataWritten)
            {
                // New cycle begins; old data is invalid now
                ClearAccessTracking();
            }

            var offset = part * accessByteWidth;
            var tempArray = BitConverter.GetBytes(value);

            for(var i = 0; i < accessByteWidth; i++)
            {
                array[i + offset] = tempArray[i];
            }

            partAccessed[part] = StateTracker.Written;
        }

        public uint GetPartAsDoubleWord(uint part)
        {
            if(part >= partsCount)
            {
                throw new ArgumentOutOfRangeException();
            }

            var value = BitHelper.ToUInt32(array, (int)(part * accessByteWidth), (int)accessByteWidth, true);
            partAccessed[part] = StateTracker.Read;
            return value;
        }

        public void SetArrayTo(byte[] data, bool trackAccess = true)
        {
            if(data.Length != array.Length)
            {
                throw new ArgumentException("Tried to write array with an incorrect data length");
            }
            array = data;

            if(trackAccess)
            {
                SetAllAccessTrackingTo(StateTracker.Written);
            }
        }

        public byte[] RetriveData(bool trackAccess = true)
        {
            if(!AllDataWritten)
            {
                parent.Log(LogLevel.Warning, "Trying to retrive registers '{0}' data before all of them have been written. Returning empty array", name);
                return new byte[0];
            }

            if(trackAccess)
            {
                SetAllAccessTrackingTo(StateTracker.Read);
            }

            return array;
        }

        public bool AllDataWritten => partAccessed.All(a => a == StateTracker.Written);

        public bool AllDataRead => partAccessed.All(a => a == StateTracker.Read);

        private void ClearAccessTracking()
        {
            Array.Clear(partAccessed, 0, partAccessed.Length);
        }

        private void SetAllAccessTrackingTo(StateTracker state)
        {
            for(var index = 0; index < partAccessed.Length; index++)
            {
                partAccessed[index] = state;
            }
        }

        private byte[] array;
        private readonly string name;
        private readonly uint accessByteWidth;
        private readonly uint partsCount;

        private readonly IPeripheral parent;
        private readonly StateTracker[] partAccessed;

        private enum StateTracker
        {
            Untouched = 0,
            Written = 1,
            Read = 2,
        }
    }
}
