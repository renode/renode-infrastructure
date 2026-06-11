//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Bus.Wrappers
{
    public class MapperProvider
    {
        public MapperProvider(RegisterMapper defaultMapper)
        {
            this.defaultMapper = defaultMapper;
        }

        public IDisposable WithCurrent(RegisterMapper mapper)
        {
            if(currentMapper is not null)
            {
                throw new InvalidOperationException("Current mapper is already set");
            }

            currentMapper = mapper;
            return DisposableWrapper.New(() => currentMapper = null);
        }

        public RegisterMapper CurrentMapper => currentMapper ?? defaultMapper;

        private RegisterMapper currentMapper;
        private readonly RegisterMapper defaultMapper;
    }
}
