//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System.Threading;

namespace Antmicro.Renode.Debugging
{
    public class IdentifiableObject : IIdentifiable
    {
#if DEBUG
        public IdentifiableObject()
        {
            uniqueObjectId = Interlocked.Increment(ref IdCounter);
        }

        public int UniqueObjectId
        {
            get { return uniqueObjectId; }
        }

        public override string ToString()
        {
            return $"[IdentifiableObject: {uniqueObjectId}]";
        }

        private int uniqueObjectId;

        private static int IdCounter = 0;
#endif
    }
}
