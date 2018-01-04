//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

namespace Antmicro.Renode.Utilities
{
    public class I2CCommandManager<T>
    {
        public I2CCommandManager()
        {
            commands = new List<Tuple<byte[], T>>();
        }

        public void RegisterCommand(T handler, params byte[] address)
        {
            commands.Add(Tuple.Create<byte[], T>(address, handler));
        }

        public bool TryGetCommand(byte[] data, out T handler)
        {
            var command = commands.FirstOrDefault(x => x.Item1.SequenceEqual(data.Take(x.Item1.Length)));
            if(command == null)
            {
                handler = default(T);
                return false;
            }

            handler = command.Item2;
            return true;
        }

        private readonly List<Tuple<byte[], T>> commands;
    }
}