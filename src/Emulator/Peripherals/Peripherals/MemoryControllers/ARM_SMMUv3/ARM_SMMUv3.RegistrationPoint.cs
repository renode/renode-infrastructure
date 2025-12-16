//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core.Structure;

namespace Antmicro.Renode.Peripherals.MemoryControllers
{
    public class ARM_SMMUv3RegistrationPoint : IRegistrationPoint
    {
        public ARM_SMMUv3RegistrationPoint(int stream, ARM_SMMUv3.SecurityState securityState = ARM_SMMUv3.SecurityState.NonSecure)
        {
            Stream = stream;
            SecurityState = securityState;
        }

        public override bool Equals(object obj)
        {
            if(obj is ARM_SMMUv3RegistrationPoint other)
            {
                return ReferenceEquals(this, other) || (Stream == other.Stream && SecurityState == other.SecurityState);
            }
            return false;
        }

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 31 + Stream.GetHashCode();
            hash = hash * 31 + SecurityState.GetHashCode();
            return hash;
        }

        public override string ToString()
        {
            return $"Stream: {Stream}, SecurityState: {SecurityState}";
        }

        public int Stream { get; }

        public ARM_SMMUv3.SecurityState SecurityState { get; }

        public string PrettyString => ToString();
    }
}
