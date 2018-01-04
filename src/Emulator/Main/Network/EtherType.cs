//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Network
{
    public enum EtherType : ushort
    {
        IpV4 = 0x0800,
        Arp = 0x0806,
        ReverseArp = 0x8035,
        WakeOnLan = 0x0842,
        AppleTalk = 0x809B,
        AppleTalkArp = 0x80F3,
        VLanTaggedFrame = 0x8100,
        NovellInternetworkPacketExchange = 0x8137,
        Novell = 0x8138,
        IpV6 = 0x86DD,
        MacControl = 0x8808,
        CobraNet = 0x8819,
        MultiprotocolLabelSwitchingUnicast = 0x8847,
        MultiprotocolLabelSwitchingMulticast = 0x8848,
        PointToPointProtocolOverEthernetDiscoveryStage = 0x8863,
        PointToPointProtocolOverEthernetSessionStage = 0x8864,
        ExtensibleAuthenticationProtocolOverLan = 0x888E,
        HyperScsi = 0x889A,
        AtaOverEthernet = 0x88A2,
        EtherCatProtocol = 0x88A4,
        ProviderBridging = 0x88A8,
        AvbTransportProtocol = 0x88B5,
        LLDP = 0x88CC,
        SerialRealTimeCommunicationSystemIii = 0x88CD,
        CircuitEmulationServicesOverEthernet = 0x88D8,
        HomePlug = 0x88E1,
        MacSecurity = 0x88E5,
        PrecisionTimeProtocol = 0x88f7,
        ConnectivityFaultManagementOrOperationsAdministrationManagement = 0x8902,
        FibreChannelOverEthernet = 0x8906,
        FibreChannelOverEthernetInitializationProtocol = 0x8914,
        QInQ = 0x9100,
        VeritasLowLatencyTransport = 0xCAFE,
        Loop = 0x0060,
        Echo = 0x0200
    }
}
