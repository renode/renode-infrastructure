//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System.Collections.Generic;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.S32K3XX_FlexIOModel
{
    public class ResourceBlocksManager<T> where T : ResourceBlock
    {
        public ResourceBlocksManager(IEmulationElement owner, string resourceBlockName, IReadOnlyList<T> resourceBlocks)
        {
            logger = owner;
            blockName = resourceBlockName;
            blocks = resourceBlocks;
        }

        public bool TryGet(uint identifier, out T block)
        {
            var exists = identifier < blocks.Count;
            block = exists ? blocks[(int)identifier] : null;
            return exists;
        }

        public bool Reserve(IPeripheral reserver, uint identifier, out T block)
        {
            var exists = TryGet(identifier, out block);
            if(exists)
            {
                if(reservations.ContainsKey(block))
                {
                    // Sharing blocks may be valid for more complex scenarios
                    logger.Log(LogLevel.Warning, "The {0} with the {1} identifier is already used by another peripheral, sharing a {0} between peripherals may cause an unexpected result", blockName, identifier);
                }
                reservations[block] = reserver;
            }
            return exists;
        }

        private readonly string blockName;
        private readonly IReadOnlyList<T> blocks;
        private readonly IEmulationElement logger;
        private readonly IDictionary<T, IPeripheral> reservations = new Dictionary<T, IPeripheral>();
    }
}
