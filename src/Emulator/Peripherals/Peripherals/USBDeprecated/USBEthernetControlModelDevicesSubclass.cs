//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Utilities;


namespace Antmicro.Renode.Peripherals.USBDeprecated
{
    public class USBEthernetControlModelDevicesSubclass : USBCommunicationClass
    {
        public USBEthernetControlModelDevicesSubclass ()
        {
            //base.deviceDescriptor.DeviceSubClass = DeviceSubclassCode;
        }
        protected const byte DeviceSubclassCode = (byte)SubclassCode.EthernetNetworkingControlModel;
        protected const byte ProtocolSubclassCode = (byte)ProtocolCode.NoClassSpecific;

        [Flags]
        protected enum EthernetStatisticsCapability : uint
        {
            XmitOK = 1 << 0,
            RvcOK = 1 << 1,
            XmitError = 1 << 2,
            RvcError = 1 << 3,
            RvcNoBuffer = 1 << 4,
            DirectedBytesXmit = 1 << 5,
            DirectedFramesXmit = 1 << 6,
            MulticastBytesXmit = 1 << 7,
            MulticastFramesXmit = 1 << 8,
            BroadcastBytesXmit = 1 << 9,
            BroadcastFramesXmit = 1 << 10,
            DirectedBytesRcv = 1 << 11,
            DirectedFramesRcv = 1 << 12,
            MulticastBytesRcv = 1 << 13,
            MulticastFramesRcv = 1 << 14,
            BroadcastBytesRcv = 1 << 15,
            BroadcastFramesRcv = 1 << 16,
            RcvCRCError = 1 << 17,
            TransmitQueueLength = 1 << 18,
            RcvErrorAlignment = 1 << 19,
            XmitOneCollision  = 1 << 20,
            XmitMoreCollision  = 1 << 21,
            XmitDeferred  = 1 << 22,
            XmitMaxCollisions = 1 << 23,
            RcvOverrun = 1 << 24,
            XmitUnderrun = 1 << 25,
            XmitHeartbeatFailure = 1 << 26,
            XmitTimesCrsLost = 1 << 27,
            XmitLateCollisions = 1 << 28
        }

        protected enum SubclassSpecificRequestCode
        {
            SetEthernetMulticastFilters = 0x40,
            SetEthernetPowerManagementPatternFilter = 0x41,
            GetEthernetPowerManagementPatternFilter = 0x42,
            SetEthernetPacketFilter = 0x43,
            GetEthernetStatistic = 0x44

        }

        protected enum SubclassSpecificNotificationCode
        {
            NetworkConnection = 0x00,
            ResponseAvaliable = 0x01,
            ConnectionSpeedChange = 0x2A
        }

        protected enum EthernetStatisticsFeatureSelectorCode : byte
        {
            XmitOK = 0x01,
            RvcOK = 0x02,
            XmitError = 0x03,
            RvcError = 0x04,
            RvcNoBuffer = 0x05,
            DirectedBytesXmit = 0x06,
            DirectedFramesXmit = 0x07,
            MulticastBytesXmit = 0x08,
            MulticastFramesXmit = 0x09,
            BroadcastBytesXmit = 0x0A,
            BroadcastFramesXmit = 0x0B,
            DirectedBytesRcv = 0x0C,
            DirectedFramesRcv = 0x0D,
            MulticastBytesRcv = 0x0F,
            MulticastFramesRcv = 0x10,
            BroadcastBytesRcv = 0x11,
            BroadcastFramesRcv = 0x12,
            RcvCRCError = 0x13,
            TransmitQueueLength = 0x14,
            RcvErrorAlignment = 0x15,
            XmitOneCollision  = 0x16,
            XmitMoreCollision  = 0x17,
            XmitDeferred  = 0x18,
            XmitMaxCollisions = 0x19,
            RcvOverrun = 0x1A,
            XmitUnderrun = 0x1B,
            XmitHeartbeatFailure = 0x1C,
            XmitTimesCrsLost = 0x1D,
            XmitLateCollisions = 0x1D
        }

        protected struct EthernetStatistics
        {
            public uint XmitOK;
            public uint RvcOK;
            public uint XmitError;
            public uint RvcError;
            public uint RvcNoBuffer;
            public uint DirectedBytesXmit;
            public uint DirectedFramesXmit;
            public uint MulticastBytesXmit;
            public uint MulticastFramesXmit;
            public uint BroadcastBytesXmit;
            public uint BroadcastFramesXmit;
            public uint DirectedBytesRcv;
            public uint DirectedFramesRcv;
            public uint MulticastBytesRcv;
            public uint MulticastFramesRcv;
            public uint BroadcastBytesRcv;
            public uint BroadcastFramesRcv;
            public uint RcvCRCError;
            public uint TransmitQueueLength;
            public uint RcvErrorAlignment;
            public uint XmitOneCollision;
            public uint XmitMoreCollision;
            public uint XmitDeferred;
            public uint XmitMaxCollisions;
            public uint RcvOverrun;
            public uint XmitUnderrun;
            public uint XmitHeartbeatFailure;
            public uint XmitTimesCrsLost;
            public uint XmitLateCollisions;
        }

        protected class EthernetNetworkingFuncionalDescriptor : USBDescriptor
        {
            public EthernetNetworkingFuncionalDescriptor ()
            {
                base.Length = 0x0D;
                base.Type = (DescriptorType) CommunicationClassDescriptorType.Interface;
            }

            public const byte DescriptorSubtype = DeviceSubclassCode;
            public byte MacAddressIndex;
            public uint EthernetStatistics;
            public ushort MaxSegmentSize;
            public ushort MulticastFiltersNumber;
            public byte PowerFiltersNumber;


            public override byte[] ToArray ()
            {
                var arr = base.ToArray ();
                arr[0x02] = DescriptorSubtype;
                arr[0x03] = MacAddressIndex;
                BitConverter.GetBytes(EthernetStatistics).CopyTo(arr,0x04);
                BitConverter.GetBytes(MaxSegmentSize).CopyTo(arr,0x08);
                BitConverter.GetBytes(MulticastFiltersNumber).CopyTo(arr,0x0A);
                arr[0x0C] = PowerFiltersNumber;
                return arr;
            }

        }


    }
}

