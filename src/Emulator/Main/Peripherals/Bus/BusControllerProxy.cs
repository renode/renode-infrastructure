//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using Antmicro.Renode.Peripherals.CPU;

namespace Antmicro.Renode.Peripherals.Bus
{
    public abstract class BusControllerProxy : IBusController
    {
        public BusControllerProxy(IBusController parentController)
        {
            ParentController = parentController;
        }

        public virtual byte ReadByte(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadByte(address, context);
            }
            return (byte)0;
        }

        public virtual void WriteByte(ulong address, byte value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteByte(address, value, context);
            }
        }

        public virtual ushort ReadWord(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadWord(address, context);
            }
            return (ushort)0;
        }

        public virtual void WriteWord(ulong address, ushort value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteWord(address, value, context);
            }
        }

        public virtual uint ReadDoubleWord(ulong address, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                return ParentController.ReadDoubleWord(address, context);
            }
            return (uint)0;
        }

        public virtual void WriteDoubleWord(ulong address, uint value, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteDoubleWord(address, value, context);
            }
        }

        public virtual void ReadBytes(ulong address, int count, byte[] destination, int startIndex, bool onlyMemory = false, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Read, context))
            {
                ParentController.ReadBytes(address, count, destination, startIndex, onlyMemory, context);
            }
        }

        public virtual byte[] ReadBytes(ulong address, int count, bool onlyMemory = false, ICPU context = null)
        {
            var result = new byte[count];
            ReadBytes(address, count, result, 0, onlyMemory, context);
            return result;
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, bytes.Length, onlyMemory, context);
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, int startingIndex, long count, bool onlyMemory = false, ICPU context = null)
        {
            if(ValidateOperation(ref address, BusAccessPrivileges.Write, context))
            {
                ParentController.WriteBytes(bytes, address, startingIndex, count, onlyMemory, context);
            }
        }

        public virtual void WriteBytes(byte[] bytes, ulong address, long count, bool onlyMemory = false, ICPU context = null)
        {
            WriteBytes(bytes, address, 0, count, onlyMemory, context);
        }

        public virtual IBusRegistered<IBusPeripheral> WhatIsAt(ulong address, ICPU context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatIsAt(address, context);
        }

        public virtual IPeripheral WhatPeripheralIsAt(ulong address, ICPU context = null)
        {
            ValidateOperation(ref address, BusAccessPrivileges.Other, context);
            return ParentController.WhatPeripheralIsAt(address, context);
        }

        public virtual IBusController ParentController { get; protected set; }

        protected virtual bool ValidateOperation(ref ulong address, BusAccessPrivileges accessType, ICPU context = null)
        {
            return true;
        }
    }
}
