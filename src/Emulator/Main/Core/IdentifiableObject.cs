//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Threading;

namespace Antmicro.Renode.Core
{
    public class IdentifiableObject : IIdentifiable
    {
        public IdentifiableObject()
        {
            uniqueObjectId = Interlocked.Increment(ref IdCounter);
        }

        public override string ToString()
        {
            return $"[IdentifiableObject: {uniqueObjectId}]";
        }

        public int UniqueObjectId
        {
            get { return uniqueObjectId; }
        }

        private static int IdCounter = 0;

        private readonly int uniqueObjectId;
    }
}