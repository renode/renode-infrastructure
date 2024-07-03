//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Threading;
using Antmicro.Migrant;
using System.Reflection;
using Antmicro.Renode.Peripherals.Bus.Wrappers;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;
using System.Collections.Generic;

using Endianess = ELFSharp.ELF.Endianess;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class PeripheralAccessMethods
    {
        public BusAccess.ByteReadMethod ReadByte;
        public BusAccess.ByteWriteMethod WriteByte;
        public BusAccess.WordReadMethod ReadWord;
        public BusAccess.WordWriteMethod WriteWord;
        public BusAccess.DoubleWordReadMethod ReadDoubleWord;
        public BusAccess.DoubleWordWriteMethod WriteDoubleWord;
        public BusAccess.QuadWordReadMethod ReadQuadWord;
        public BusAccess.QuadWordWriteMethod WriteQuadWord;
        public Action<ulong> SetAbsoluteAddress;
        public IBusPeripheral Peripheral;
        public string Tag;
        [Constructor(true)]
        public SpinLock Lock;

        public static PeripheralAccessMethods CreateWithLock()
        {
            // Thread ownership tracking should be enabled. We use the IsHeldByCurrentThread
            // property to simulate recursive locking on spinlocks
            return new PeripheralAccessMethods { Lock = new SpinLock(true) };
        }

        public PeripheralAccessMethods()
        {
            AllTranslationsEnabled = false;
            byteAccessTranslationEnabledDynamically = false;
            wordAccessTranslationEnabledDynamically = false;
            doubleWordAccessTranslationEnabledDynamically = false;
            quadWordAccessTranslationEnabledDynamically = false;
        }

        public void SetMethod(MethodInfo i, object obj, BusAccess.Operation operation, BusAccess.Method method)
        {
            switch(method)
            {
            case BusAccess.Method.Byte:
                SetReadOrWriteMethod(i, obj, operation, ref ReadByte, ref WriteByte);
                break;
            case BusAccess.Method.Word:
                SetReadOrWriteMethod(i, obj, operation, ref ReadWord, ref WriteWord);
                break;
            case BusAccess.Method.DoubleWord:
                SetReadOrWriteMethod(i, obj, operation, ref ReadDoubleWord, ref WriteDoubleWord);
                break;
            case BusAccess.Method.QuadWord:
                SetReadOrWriteMethod(i, obj, operation, ref ReadQuadWord, ref WriteQuadWord);
                break;
            default:
                throw new ArgumentException(string.Format("Unsupported access method: {0}", method));
            }
        }

        public void EnableAllTranslations(bool enable, Endianess endianess)
        {
            // Methods can be wrapped, e.g. in Read/WriteLoggingWrapper. Wrappers must be left intact.
            // Therefore, we will only overwrite the "base method" at the bottom of the "wrapper stack".
            // To achieve this, unwrap each wrapper onto a temporary stack, overwrite the base method,
            // and then rewrap it in everything from the stack.
            var wrapperStack = new Stack<Tuple<Type, Type>>(); // two types, for read and write
            while(ReadByte.Target is HookWrapper)
            {
                wrapperStack.Push(UnwrapMethods());
            }

            if(enable)
            { 
                BuildMissingAccesses(endianess);
            }
            else
            {
                DisableTranslatedAccesses();
            }

            // Restore wrappers from stack
            while(wrapperStack.Count != 0)
            {
                var t = wrapperStack.Pop();
                WrapMethods(t.Item1, t.Item2);
            }

            AllTranslationsEnabled = enable;
        }

        private void BuildMissingAccesses(Endianess endianess)
        {
            DebugHelper.Assert(!(ReadByte.Target is HookWrapper));

            var accssMethods = new dynamic []
            {
                Tuple.Create(ReadByte, WriteByte, (BusAccess.ByteReadMethod)Peripheral.ReadByteNotTranslated),
                Tuple.Create(ReadWord, WriteWord, (BusAccess.WordReadMethod)Peripheral.ReadWordNotTranslated),
                Tuple.Create(ReadDoubleWord, WriteDoubleWord, (BusAccess.DoubleWordReadMethod)Peripheral.ReadDoubleWordNotTranslated),
                Tuple.Create(ReadQuadWord, WriteQuadWord, (BusAccess.QuadWordReadMethod)Peripheral.ReadQuadWordNotTranslated),
            };

            // find implemented access methods
            dynamic read = null;
            dynamic write = null;

            foreach(var methods in accssMethods)
            {
                var readMethod = methods.Item1;
                var writeMethod = methods.Item2;
                var notTranslated = methods.Item3;

                if(readMethod != notTranslated)
                {
                    read = readMethod;
                    write = writeMethod;
                    break;
                }
            }

            if(read == null)
            {
                throw new RecoverableException($"Peripheral {Peripheral} does not implement any memory mapped access methods");
            }

            if(ReadByte == Peripheral.ReadByteNotTranslated)
            {
                byteAccessTranslationEnabledDynamically = true;
                if(endianess == Endianess.LittleEndian)
                {
                    ReadByte = ReadWriteExtensions.BuildByteReadUsing(read);
                    WriteByte = ReadWriteExtensions.BuildByteWriteUsing(read, write);
                }
                else
                {
                    ReadByte = ReadWriteExtensions.BuildByteReadBigEndianUsing(read);
                    WriteByte = ReadWriteExtensions.BuildByteWriteBigEndianUsing(read, write);
                }
            }

            if(ReadWord == Peripheral.ReadWordNotTranslated)
            {
                wordAccessTranslationEnabledDynamically = true;
                if(endianess == Endianess.LittleEndian)
                {
                    ReadWord = ReadWriteExtensions.BuildWordReadUsing(read);
                    WriteWord = ReadWriteExtensions.BuildWordWriteUsing(read, write);
                }
                else
                {
                    ReadWord = ReadWriteExtensions.BuildWordReadBigEndianUsing(read);
                    WriteWord = ReadWriteExtensions.BuildWordWriteBigEndianUsing(read, write);
                }
            }
            if(ReadDoubleWord == Peripheral.ReadDoubleWordNotTranslated)
            {
                doubleWordAccessTranslationEnabledDynamically = true;
                if(endianess == Endianess.LittleEndian)
                {
                    ReadDoubleWord = ReadWriteExtensions.BuildDoubleWordReadUsing(read);
                    WriteDoubleWord = ReadWriteExtensions.BuildDoubleWordWriteUsing(read, write);
                }
                else
                {
                    ReadDoubleWord = ReadWriteExtensions.BuildDoubleWordReadBigEndianUsing(read);
                    WriteDoubleWord = ReadWriteExtensions.BuildDoubleWordWriteBigEndianUsing(read, write);
                }
            }
            if(ReadQuadWord == Peripheral.ReadQuadWordNotTranslated)
            {
                quadWordAccessTranslationEnabledDynamically = true;
                if(endianess == Endianess.LittleEndian)
                {
                    ReadQuadWord = ReadWriteExtensions.BuildQuadWordReadUsing(read);
                    WriteQuadWord = ReadWriteExtensions.BuildQuadWordWriteUsing(read, write);
                }
                else
                {
                    ReadQuadWord = ReadWriteExtensions.BuildQuadWordReadBigEndianUsing(read);
                    WriteQuadWord = ReadWriteExtensions.BuildQuadWordWriteBigEndianUsing(read, write);
                }
            }
        }

        void DisableTranslatedAccesses()
        {
            DebugHelper.Assert(!(ReadByte.Target is HookWrapper));

            if(byteAccessTranslationEnabledDynamically)
            {
                byteAccessTranslationEnabledDynamically = false;
                ReadByte = Peripheral.ReadByteNotTranslated;
                WriteByte = Peripheral.WriteByteNotTranslated;
            }

            if(wordAccessTranslationEnabledDynamically)
            {
                wordAccessTranslationEnabledDynamically = false;
                ReadWord = Peripheral.ReadWordNotTranslated;
                WriteWord = Peripheral.WriteWordNotTranslated;
            }

            if(doubleWordAccessTranslationEnabledDynamically)
            {
                doubleWordAccessTranslationEnabledDynamically = false;
                ReadDoubleWord = Peripheral.ReadDoubleWordNotTranslated;
                WriteDoubleWord = Peripheral.WriteDoubleWordNotTranslated;
            }

            if(quadWordAccessTranslationEnabledDynamically)
            {
                quadWordAccessTranslationEnabledDynamically = false;
                ReadQuadWord = Peripheral.ReadQuadWordNotTranslated;
                WriteQuadWord = Peripheral.WriteQuadWordNotTranslated;
            }
        }

        public void WrapMethods(Type readWrapperType, Type writeWrapperType)
        {
            // Make closed types by binding generic parameters of the wrapper classes
            var byteReadWrapperType = readWrapperType.MakeGenericType(new [] {typeof(byte)});
            var wordReadWrapperType = readWrapperType.MakeGenericType(new [] {typeof(ushort)});
            var doubleWordReadWrapperType = readWrapperType.MakeGenericType(new [] {typeof(uint)});
            var quadWordReadWrapperType = readWrapperType.MakeGenericType(new [] {typeof(ulong)});
            var byteWriteWrapperType = writeWrapperType.MakeGenericType(new [] {typeof(byte)});
            var wordWriteWrapperType = writeWrapperType.MakeGenericType(new [] {typeof(ushort)});
            var doubleWordWriteWrapperType = writeWrapperType.MakeGenericType(new [] {typeof(uint)});
            var quadWordWriteWrapperType = writeWrapperType.MakeGenericType(new [] {typeof(ulong)});

            // Prepare argument lists for each type's constructor
            var byteReadWrapperArgs = new object[] {Peripheral, new Func<long, byte>(ReadByte)};
            var wordReadWrapperArgs = new object[] {Peripheral, new Func<long, ushort>(ReadWord)};
            var doubleWordReadWrapperArgs = new object[] {Peripheral, new Func<long, uint>(ReadDoubleWord)};
            var quadWordReadWrapperArgs = new object[] {Peripheral, new Func<long, ulong>(ReadQuadWord)};
            var byteWriteWrapperArgs = new object[] {Peripheral, new Action<long, byte>(WriteByte)};
            var wordWriteWrapperArgs = new object[] {Peripheral, new Action<long, ushort>(WriteWord)};
            var doubleWordWriteWrapperArgs = new object[] {Peripheral, new Action<long, uint>(WriteDoubleWord)};
            var quadWordWriteWrapperArgs = new object[] {Peripheral, new Action<long, ulong>(WriteQuadWord)};

            // Instantiate each type
            var byteReadWrapperObj = (ReadHookWrapper<byte>)Activator.CreateInstance(byteReadWrapperType, byteReadWrapperArgs);
            var wordReadWrapperObj = (ReadHookWrapper<ushort>)Activator.CreateInstance(wordReadWrapperType, wordReadWrapperArgs);
            var doubleWordReadWrapperObj = (ReadHookWrapper<uint>)Activator.CreateInstance(doubleWordReadWrapperType, doubleWordReadWrapperArgs);
            var quadWordReadWrapperObj = (ReadHookWrapper<ulong>)Activator.CreateInstance(quadWordReadWrapperType, quadWordReadWrapperArgs);
            var byteWriteWrapperObj = (WriteHookWrapper<byte>)Activator.CreateInstance(byteWriteWrapperType, byteWriteWrapperArgs);
            var wordWriteWrapperObj = (WriteHookWrapper<ushort>)Activator.CreateInstance(wordWriteWrapperType, wordWriteWrapperArgs);
            var doubleWordWriteWrapperObj = (WriteHookWrapper<uint>)Activator.CreateInstance(doubleWordWriteWrapperType, doubleWordWriteWrapperArgs);
            var quadWordWriteWrapperObj = (WriteHookWrapper<ulong>)Activator.CreateInstance(quadWordWriteWrapperType, quadWordWriteWrapperArgs);

            // Replace methods with wrapped versions
            ReadByte = new BusAccess.ByteReadMethod(byteReadWrapperObj.Read);
            ReadWord = new BusAccess.WordReadMethod(wordReadWrapperObj.Read);
            ReadDoubleWord = new BusAccess.DoubleWordReadMethod(doubleWordReadWrapperObj.Read);
            ReadQuadWord = new BusAccess.QuadWordReadMethod(quadWordReadWrapperObj.Read);
            WriteByte = new BusAccess.ByteWriteMethod(byteWriteWrapperObj.Write);
            WriteWord = new BusAccess.WordWriteMethod(wordWriteWrapperObj.Write);
            WriteDoubleWord = new BusAccess.DoubleWordWriteMethod(doubleWordWriteWrapperObj.Write);
            WriteQuadWord = new BusAccess.QuadWordWriteMethod(quadWordWriteWrapperObj.Write);
        }

        public Tuple<Type, Type> UnwrapMethods()
        {
            var readWrapperType = ReadByte.Target.GetType().GetGenericTypeDefinition();
            var writeWrapperType = WriteByte.Target.GetType().GetGenericTypeDefinition();
            ReadByte = (BusAccess.ByteReadMethod)(((ReadHookWrapper<byte>)ReadByte.Target).OriginalMethod).Target;
            ReadWord = (BusAccess.WordReadMethod)(((ReadHookWrapper<ushort>)ReadWord.Target).OriginalMethod).Target;
            ReadDoubleWord = (BusAccess.DoubleWordReadMethod)(((ReadHookWrapper<uint>)ReadDoubleWord.Target).OriginalMethod).Target;
            ReadQuadWord = (BusAccess.QuadWordReadMethod)(((ReadHookWrapper<ulong>)ReadQuadWord.Target).OriginalMethod).Target;
            WriteByte = (BusAccess.ByteWriteMethod)(((WriteHookWrapper<byte>)WriteByte.Target).OriginalMethod).Target;
            WriteWord = (BusAccess.WordWriteMethod)(((WriteHookWrapper<ushort>)WriteWord.Target).OriginalMethod).Target;
            WriteDoubleWord = (BusAccess.DoubleWordWriteMethod)(((WriteHookWrapper<uint>)WriteDoubleWord.Target).OriginalMethod).Target;
            WriteQuadWord = (BusAccess.QuadWordWriteMethod)(((WriteHookWrapper<ulong>)WriteQuadWord.Target).OriginalMethod).Target;
            return new Tuple<Type, Type>(readWrapperType, writeWrapperType);
        }

        public void RemoveWrappersOfType(Type readWrapperType, Type writeWrapperType)
        {
            var wrapperStack = new Stack<Tuple<Type, Type>>();

            while(ReadByte.Target is HookWrapper)
            {
                if(readWrapperType == ReadByte.Target.GetType().GetGenericTypeDefinition()
                    && writeWrapperType == WriteByte.Target.GetType().GetGenericTypeDefinition())
                {
                    UnwrapMethods();
                    continue;
                }

                wrapperStack.Push(UnwrapMethods());
            }

            while(wrapperStack.Count != 0)
            {
                var t = wrapperStack.Pop();
                WrapMethods(t.Item1, t.Item2);
            }
        }

        public bool AllTranslationsEnabled { get; private set; }

        private static void SetReadOrWriteMethod<TR, TW>(MethodInfo i, object obj, BusAccess.Operation operation, ref TR readMethod, ref TW writeMethod)
        {
            switch(operation)
            {
            case BusAccess.Operation.Read:
                readMethod = (TR)(object)i.CreateDelegate(typeof(TR), obj);
                break;
            case BusAccess.Operation.Write:
                writeMethod = (TW)(object)i.CreateDelegate(typeof(TW), obj);
                break;
            default:
                throw new ArgumentException(string.Format("Unsupported access operation: {0}", operation));
            }
        }

        private bool byteAccessTranslationEnabledDynamically;
        private bool wordAccessTranslationEnabledDynamically;
        private bool doubleWordAccessTranslationEnabledDynamically;
        private bool quadWordAccessTranslationEnabledDynamically;
    }
}

