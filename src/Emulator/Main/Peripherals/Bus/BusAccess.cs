//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Peripherals.Bus
{
    public class BusAccess
    {
        static BusAccess()
        {
            Delegates = new []
            {
                typeof(QuadWordReadMethod), typeof(QuadWordWriteMethod),
                typeof(DoubleWordReadMethod), typeof(DoubleWordWriteMethod),
                typeof(WordReadMethod), typeof(WordWriteMethod),
                typeof(ByteReadMethod), typeof(ByteWriteMethod)
            };

            accessMethods = new Dictionary<Type, Method>();
            accessOperations = new Dictionary<Type, Operation>();

            foreach(var delegateType in Delegates)
            {
                Method accessMethod;
                var delegateMethodInfo = delegateType.GetMethod("Invoke");
                var parameters = delegateMethodInfo.GetParameters();
                if(delegateMethodInfo.ReturnType == typeof(void))
                {
                    if(parameters.Length != 2
                        || parameters[0].ParameterType != typeof(long)
                        || !TryGetMethodFromType(parameters[1].ParameterType, out accessMethod))
                    {
                        throw new ArgumentException("Wrong method signature");
                    }
                    accessOperations[delegateType] = Operation.Write;
                }
                else
                {
                    if(parameters.Length != 1
                        || parameters[0].ParameterType != typeof(long)
                        || !TryGetMethodFromType(delegateMethodInfo.ReturnType, out accessMethod))
                    {
                        throw new ArgumentException("Wrong method signature");
                    }
                    accessOperations[delegateType] = Operation.Read;
                }

                accessMethods[delegateType] = accessMethod;
            }
        }

        private static bool TryGetMethodFromType(Type type, out Method method)
        {
            foreach(var member in typeof(Method).GetMembers())
            {
                var interestingAttributes = member.GetCustomAttributes(typeof(ReferencedTypeAttribute), false);
                if(interestingAttributes.Length == 0)
                {
                    continue;
                }

                if(((ReferencedTypeAttribute)interestingAttributes[0]).ReferencedType == type)
                {
                    method = (Method)Enum.Parse(typeof(Method), member.Name);
                    return true;
                }
            }

            method = Method.Byte;
            return false;
        }

        public static Operation GetOperationFromSignature(Type t)
        {
            return accessOperations[t];
        }

        public static Method GetMethodFromSignature(Type t)
        {
            return accessMethods[t];
        }

        public static Operation GetComplementingOperation(Operation operation)
        {
            return operation == Operation.Read ? Operation.Write : Operation.Read;
        }

        public static Type[] Delegates { get; private set; }

        private static readonly Dictionary<Type, Method> accessMethods;
        private static readonly Dictionary<Type, Operation> accessOperations;

        public delegate ulong QuadWordReadMethod(long offset);
        public delegate void QuadWordWriteMethod(long offset, ulong value);
        public delegate uint DoubleWordReadMethod(long offset);
        public delegate void DoubleWordWriteMethod(long offset, uint value);
        public delegate ushort WordReadMethod(long offset);
        public delegate void WordWriteMethod(long offset, ushort value);
        public delegate byte ByteReadMethod(long offset);
        public delegate void ByteWriteMethod(long offset, byte value);

        public enum Operation
        {
            Read,
            Write
        }

        public enum Method
        {
            [ReferencedType(typeof(byte))]
            Byte,
            [ReferencedType(typeof(ushort))]
            Word,
            [ReferencedType(typeof(uint))]
            DoubleWord,
            [ReferencedType(typeof(ulong))]
            QuadWord
        }

        private class ReferencedTypeAttribute : Attribute
        {
            public ReferencedTypeAttribute(Type t)
            {
                ReferencedType = t;
            }

            public Type ReferencedType { get; private set; }
        }
    }
}
