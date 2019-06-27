//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.UserInterface;

namespace Antmicro.Renode.Peripherals.Network
{
    [Icon("phy")]
    public class EthernetPhysicalLayer : IPhysicalLayer<ushort>
    {
        public EthernetPhysicalLayer()
        {
            BasicStatus = (ushort)(1 << 5 | 1 << 2); // link up, auto-negotiation complete
        }

        public ushort Read(ushort addr)
        {
            switch((Register)addr)
            {
            case Register.BasicControl:
                return (ushort)BasicControl;
            case Register.BasicStatus:
                return (ushort)BasicStatus;
            case Register.Id1:
                return (ushort)Id1;
            case Register.Id2:
                return (ushort)Id2;
            case Register.AutoNegotiationAdvertisement:
                return (ushort)AutoNegotiationAdvertisement;
            case Register.AutoNegotiationLinkPartnerBasePageAbility:
                return (ushort)AutoNegotiationLinkPartnerBasePageAbility;
            case Register.AutoNegotiationExpansion:
                return (ushort)AutoNegotiationExpansion;
            case Register.AutoNegotiationNextPageTransmit:
                return (ushort)AutoNegotiationNextPageTransmit;
            case Register.AutoNegotiationLinkPartnerReceivedNextPage:
                return (ushort)AutoNegotiationLinkPartnerReceivedNextPage;
            case Register.MasterSlaveControl:
                return (ushort)MasterSlaveControl;
            case Register.MasterSlaveStatus:
                return (ushort)MasterSlaveStatus;
            case Register.PowerSourcingEquipmentControl:
                return (ushort)PowerSourcingEquipmentControl;
            case Register.PowerSourcingEquipmentStatus:
                return (ushort)PowerSourcingEquipmentStatus;
            case Register.MDIOManageableDeviceAccessControl:
                return (ushort)MDIOManageableDeviceAccessControl;
            case Register.MDIOManageableDeviceAcceddAddressData:
                return (ushort)MDIOManageableDeviceAcceddAddressData;
            case Register.ExtendedStatus:
                return (ushort)ExtendedStatus;
            case Register.VendorSpecific0:
                return (ushort)VendorSpecific0;
            case Register.VendorSpecific1:
                return (ushort)VendorSpecific1;
            case Register.VendorSpecific2:
                return (ushort)VendorSpecific2;
            case Register.VendorSpecific3:
                return (ushort)VendorSpecific3;
            case Register.VendorSpecific4:
                return (ushort)VendorSpecific4;
            case Register.VendorSpecific5:
                return (ushort)VendorSpecific5;
            case Register.VendorSpecific6:
                return (ushort)VendorSpecific6;
            case Register.VendorSpecific7:
                return (ushort)VendorSpecific7;
            case Register.VendorSpecific8:
                return (ushort)VendorSpecific8;
            case Register.VendorSpecific9:
                return (ushort)VendorSpecific9;
            case Register.VendorSpecific10:
                return (ushort)VendorSpecific10;
            case Register.VendorSpecific11:
                return (ushort)VendorSpecific11;
            case Register.VendorSpecific12:
                return (ushort)VendorSpecific12;
            case Register.VendorSpecific13:
                return (ushort)VendorSpecific13;
            case Register.VendorSpecific14:
                return (ushort)VendorSpecific14;
            case Register.VendorSpecific15:
                return (ushort)VendorSpecific15;
            default:
                this.LogUnhandledRead(addr);
                return 0;
            }
        }

        public void Write(ushort addr, ushort val)
        {
            this.LogUnhandledWrite(addr, val);
        }

        public void Reset()
        {
        }

        public int BasicControl { get; set; }
        public int BasicStatus { get; set; }
        public int Id1 { get; set; }
        public int Id2 { get; set; }
        public int AutoNegotiationAdvertisement { get; set; }
        public int AutoNegotiationLinkPartnerBasePageAbility { get; set; }
        public int AutoNegotiationExpansion { get; set; }
        public int AutoNegotiationNextPageTransmit { get; set; }
        public int AutoNegotiationLinkPartnerReceivedNextPage { get; set; }
        public int MasterSlaveControl { get; set; }
        public int MasterSlaveStatus { get; set; }
        public int PowerSourcingEquipmentControl { get; set; }
        public int PowerSourcingEquipmentStatus { get; set; }
        public int MDIOManageableDeviceAccessControl { get; set; }
        public int MDIOManageableDeviceAcceddAddressData { get; set; }
        public int ExtendedStatus { get; set; }
        public int VendorSpecific0 { get; set; }
        public int VendorSpecific1 { get; set; }
        public int VendorSpecific2 { get; set; }
        public int VendorSpecific3 { get; set; }
        public int VendorSpecific4 { get; set; }
        public int VendorSpecific5 { get; set; }
        public int VendorSpecific6 { get; set; }
        public int VendorSpecific7 { get; set; }
        public int VendorSpecific8 { get; set; }
        public int VendorSpecific9 { get; set; }
        public int VendorSpecific10 { get; set; }
        public int VendorSpecific11 { get; set; }
        public int VendorSpecific12 { get; set; }
        public int VendorSpecific13 { get; set; }
        public int VendorSpecific14 { get; set; }
        public int VendorSpecific15 { get; set; }

        protected enum Register
        {
            BasicControl = 0,
            BasicStatus = 1,
            Id1 = 2,
            Id2 = 3,
            AutoNegotiationAdvertisement = 4,
            AutoNegotiationLinkPartnerBasePageAbility = 5,
            AutoNegotiationExpansion = 6,
            AutoNegotiationNextPageTransmit = 7,
            AutoNegotiationLinkPartnerReceivedNextPage = 8,
            MasterSlaveControl = 9,
            MasterSlaveStatus = 10,
            PowerSourcingEquipmentControl = 11,
            PowerSourcingEquipmentStatus = 12,
            MDIOManageableDeviceAccessControl = 13,
            MDIOManageableDeviceAcceddAddressData = 14,
            ExtendedStatus = 15,
            VendorSpecific0 = 16,
            VendorSpecific1 = 17,
            VendorSpecific2 = 18,
            VendorSpecific3 = 19,
            VendorSpecific4 = 20,
            VendorSpecific5 = 21,
            VendorSpecific6 = 22,
            VendorSpecific7 = 23,
            VendorSpecific8 = 24,
            VendorSpecific9 = 25,
            VendorSpecific10 = 26,
            VendorSpecific11 = 27,
            VendorSpecific12 = 28,
            VendorSpecific13 = 29,
            VendorSpecific14 = 30,
            VendorSpecific15 = 31
        }
    }
}
