//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public sealed class GPIOConnection
    {
        public GPIOConnection(int sourceNumber, GPIOEndpoint endpoint)
        {
            this.SourceNumber = sourceNumber;
            this.GPIOEndpoint = endpoint;
        }

        public int SourceNumber
        {
            get;
            private set;
        }

        public GPIOEndpoint GPIOEndpoint
        {
            get;
            private set;
        }
    }
}