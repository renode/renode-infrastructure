//
// Copyright (c) 2010-2020 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core.USB
{
        public enum USBClassCode : byte
        {
            NotSpecified = 0x0,
            Audio = 0x1,
            CommunicationsCDCControl = 0x2,
            HumanInterfaceDevice = 0x3,
            MassStorage = 0x8,
            CDCData = 0xA,
            VendorSpecific = 0xFF
        }
}