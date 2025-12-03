//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.IRQControllers
{
    public enum APICPeripheralType
    {
        LAPIC,
        IOAPIC
    }

    public interface IAPICPeripheral : IPeripheral
    {
        APICPeripheralType APICPeripheralType { get; }
    }

    public class APICMessageHelper
    {
        public APICMessageHelper(IAPICPeripheral apicPeripheral)
        {
            this.apicPeripheral = apicPeripheral;
        }

        public void SendEOIMessage(EOIMessage message)
        {
            foreach(var ioapic in IOAPICs)
            {
                // We need to broadcast EOI to all IOAPICs
                ioapic.EndOfInterrupt(message.Vector);
            }

            // TODO: Add EOI broadcast suppresion and directed EOI in IOAPIC
        }

        public void SendShortMessage(ShortMessage message, DestinationShortland destinationShortland = DestinationShortland.None)
        {
            var sender = apicPeripheral;

            if(sender.APICPeripheralType == APICPeripheralType.LAPIC)
            {
                if(!ValidateShortland(ref message, destinationShortland))
                {
                    apicPeripheral.Log(LogLevel.Warning, "Rejecting message - delivery mode: {0} is illegal for destination shortland: {1}", message.DeliveryMode, destinationShortland);
                    return;
                }
            }
            else if(sender.APICPeripheralType == APICPeripheralType.IOAPIC)
            {
                if(destinationShortland != DestinationShortland.None)
                {
                    sender.Log(LogLevel.Warning, "Only LAPIC have destination shortland option");
                    return;
                }
            }

            IEnumerable<LAPIC> search = null;

            switch(destinationShortland)
            {
            case DestinationShortland.None:
                search = LAPICs.Where(lapic => lapic.CheckID(message.DestinationMode, message.Destination));
                break;
            case DestinationShortland.Self:
                search = new List<LAPIC> { sender as LAPIC };
                break;
            case DestinationShortland.AllIncludingSelf:
                search = LAPICs;
                break;
            case DestinationShortland.AllExcludingSelf:
                var senderAsLAPIC = sender as LAPIC;
                search = LAPICs.Where(l => l != senderAsLAPIC);
                break;
            }

            if(message.DeliveryMode == DeliveryMode.Fixed ||
                message.DeliveryMode == DeliveryMode.LowestPriority ||
                message.DeliveryMode == DeliveryMode.ExtINT)
            {
                // NMI, SMI, INIT and SIPI delivery modes can be used
                // to access also disabled LAPICs
                search = search.Where(l => l.HardwareEnabled);
            }

            bool isLevelTriggered = message.TriggerMode == TriggerMode.Level;

            switch(message.DeliveryMode)
            {
            case DeliveryMode.Fixed:
                search.FirstOrDefault()?.RaiseInterrupt(message.Vector, isLevelTriggered);
                break;
            case DeliveryMode.LowestPriority:
                search.OrderBy(lapic => lapic.ProcessorPriority).FirstOrDefault()?.RaiseInterrupt(message.Vector, isLevelTriggered);
                break;
            case DeliveryMode.SMI:
                sender.Log(LogLevel.Warning, "SMI delivery mode is unsupported");
                break;
            case DeliveryMode.NMI:
                sender.Log(LogLevel.Warning, "NMI delivery mode is unsupported");
                break;
            case DeliveryMode.INIT:
                sender.Log(LogLevel.Warning, "INIT delivery mode is unsupported");
                break;
            case DeliveryMode.StartUp:
                var tmp = search.FirstOrDefault();
                if(tmp != null)
                {
                    sender.Log(LogLevel.Info, "Unhalting cpu{0} [PC: {1}]", tmp.PhysicalID, tmp.Cpu.PC.RawValue);
                    tmp.Cpu.IsHalted = false;
                }
                else
                {
                    sender.Log(LogLevel.Warning, message.DestinationMode == DestinationMode.Physical ?
                        "There is no cpu with Physical ID: {0}" :
                        "There is no cpu with Logical ID: {0}",
                        message.Destination);
                }
                break;
            case DeliveryMode.ExtINT:
                sender.Log(LogLevel.Warning, "ExtINT delivery mode is unsupported");
                break;
            }
        }

        private bool ValidateShortland(ref ShortMessage message, DestinationShortland destinationShortland)
        {
            if(destinationShortland != DestinationShortland.None && message.DeliveryMode == DeliveryMode.ExtINT)
            {
                return false;
            }

            if(message.TriggerMode == TriggerMode.Level)
            {
                apicPeripheral.Log(LogLevel.Warning, "Selected level trigger mode - overriding to edge");
                message.TriggerMode = TriggerMode.Edge;
            }

            switch(destinationShortland)
            {
            case DestinationShortland.None:
                return true;
            case DestinationShortland.Self:
                return message.DeliveryMode == DeliveryMode.Fixed;
            case DestinationShortland.AllIncludingSelf:
                return message.DeliveryMode == DeliveryMode.Fixed;
            case DestinationShortland.AllExcludingSelf:
                return message.DeliveryMode != DeliveryMode.ExtINT;
            }

            return false;
        }

        private void CachePeripheralsSearch()
        {
            var machine = apicPeripheral.GetMachine();
            cachedLAPICs = machine.GetPeripheralsOfType<LAPIC>();
            cachedIOAPICs = machine.GetPeripheralsOfType<IOAPIC>();
            isCachePresent = true;
        }

        private IEnumerable<LAPIC> LAPICs
        {
            get
            {
                if(!isCachePresent)
                {
                    CachePeripheralsSearch();
                }

                return cachedLAPICs;
            }
        }

        private IEnumerable<IOAPIC> IOAPICs
        {
            get
            {
                if(!isCachePresent)
                {
                    CachePeripheralsSearch();
                }

                return cachedIOAPICs;
            }
        }

        private bool isCachePresent;
        private IEnumerable<LAPIC> cachedLAPICs;
        private IEnumerable<IOAPIC> cachedIOAPICs;
        private readonly IAPICPeripheral apicPeripheral;

        public readonly struct EOIMessage
        {
            public EOIMessage(byte vector)
            {
                Vector = vector;
            }

            public readonly byte Vector;
        }

        public struct ShortMessage
        {
            public ShortMessage(DestinationMode destinationMode, DeliveryMode deliveryMode,
                TriggerMode triggerMode, byte vector, byte destination)
            {
                DestinationMode = destinationMode;
                DeliveryMode = deliveryMode;
                TriggerMode = triggerMode;
                Vector = vector;
                Destination = destination;
            }

            public ShortMessage(ulong value)
            {
                DestinationMode = (DestinationMode)((value >> 11) & 0x1);
                DeliveryMode = (DeliveryMode)((value >> 8) & 0x7);
                TriggerMode = (TriggerMode)((value >> 15) & 0x1);
                Vector = (byte)(value & 0xFF);
                Destination = (byte)((value >> 56) & 0xFF);
            }

            // In xAPIC there is also ArbitrationID field 
            // which controls bus access order,
            // but it's irrelevant for us, since it's used by
            // physical bus only, which is below the scope we
            // usually model - our messages won't collide

            public readonly DestinationMode DestinationMode;
            public readonly DeliveryMode DeliveryMode;
            public readonly byte Vector;
            public readonly byte Destination;

            // Trigger mode can be overriden during message parsing
            public TriggerMode TriggerMode;
        }

        public enum DeliveryMode
        {
            Fixed = 0b000,
            LowestPriority = 0b001,
            SMI = 0b010,
            NMI = 0b100,
            INIT = 0b101,
            StartUp = 0b110,
            ExtINT = 0b111
        }

        public enum DestinationMode
        {
            Physical,
            Logical
        }

        public enum TriggerMode
        {
            Edge,
            Level
        }

        public enum DestinationShortland
        {
            None = 0b00,
            Self = 0b01,
            AllIncludingSelf = 0b10,
            AllExcludingSelf = 0b11
        }
    }
}