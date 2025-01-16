//
// Copyright (c) 2010-2025 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Network;
using Antmicro.Renode.Utilities.Packets;
using MiscUtil.Conversion;
using PacketDotNet;

using IPProtocolType = Antmicro.Renode.Network.IPProtocolType;

namespace Antmicro.Renode.Peripherals.Network
{
    public partial class SynopsysDWCEthernetQualityOfService
    {
        private static readonly EtherType[] checksumOffloadEngineIpHeaderTypes =
        {
            EtherType.IpV4,
        };

        private static readonly IPProtocolType[] checksumOffloadEnginePseudoHeaderTypes =
        {
            IPProtocolType.TCP,
            IPProtocolType.UDP,
            IPProtocolType.ICMP,
        };

        private class FrameAssembler
        {
            public FrameAssembler(IEmulationElement parent, CRCPadOperation crcPadControl, ChecksumOperation checksumControl, Action<EthernetFrame> frameReady)
                : this(parent, null, crcPadControl, checksumControl, 0, frameReady, null)
            {
            }

            public FrameAssembler(IEmulationElement parent, byte[] header, uint defaultMaximumSegmentSize, TxDescriptor.ContextDescriptor? context, bool enableChecksumOffload, Action<EthernetFrame> frameReady, MACAddress? sourceMACAddress)
                : this(parent, header, CRCPadOperation.InsetCRCAndPad, enableChecksumOffload ? ChecksumOperation.InsertHeaderPayloadAndPseudoHeaderChecksum : ChecksumOperation.None,
                    context.HasValue && context.Value.oneStepTimestampCorrectionInputOrMaximumSegmentSizeValid ? context.Value.maximumSegmentSize : defaultMaximumSegmentSize, frameReady, sourceMACAddress)
            {
                if(header.Length < EthernetFields.HeaderLength)
                {
                    parent.Log(LogLevel.Error, "TCP Segmentation Offload: Header is too small, the result may be unpredictable.");
                    header = null;
                    return;
                }
                etherType = (EtherType)EndianBitConverter.Big.ToInt16(header, EthernetFields.TypePosition);
                if(etherType != EtherType.IpV4 && etherType != EtherType.IpV6)
                {
                    parent.Log(LogLevel.Error, "TCP Segmentation Offload: Invalid Ethernet Type, the result may be unpredictable.");
                    return;
                }
                if(header.Length < EthernetFields.HeaderLength + (etherType == EtherType.IpV4 ? IPv4Fields.HeaderLength : IPv6Fields.HeaderLength))
                {
                    parent.Log(LogLevel.Error, "TCP Segmentation Offload: Header is too small, the result may be unpredictable.");
                    header = null;
                    return;
                }
            }

            public void PushPayload(byte[] payloadSegment)
            {
                if(payloadSegment.Length == 0)
                {
                    return;
                }
                totalPayloadLength += (uint)payloadSegment.Length;
                if(!SegmentationActive || totalPayloadLength < maximumSegmentSize || maximumSegmentSize == 0)
                {
                    payloadSegments.Enqueue(payloadSegment);
                    return;
                }
                totalPayloadLength %= maximumSegmentSize;
                var saveLast = totalPayloadLength != 0;

                // Divide payload into MSS segments + optional reminding segment
                var i = 0;
                var segments = payloadSegments
                    .SelectMany(x => x) // flatten
                    .Concat(payloadSegment)
                    .GroupBy(_ => i++ / maximumSegmentSize) // group into MSS chunks
                    .ToArray();

                payloadSegments.Clear();

                if(saveLast)
                {
                    payloadSegments.Enqueue(segments.Last().ToArray());
                }

                foreach(var segment in segments.Take(segments.Length + (saveLast ? -1 : 0)))
                {
                    FinalizeSegment(segment, maximumSegmentSize);
                }
            }

            public void FinalizeAssembly()
            {
                if(totalPayloadLength == 0)
                {
                    return;
                }
                FinalizeSegment(payloadSegments.SelectMany(x => x), totalPayloadLength, true);
            }

            private FrameAssembler(IEmulationElement parent, byte[] header, CRCPadOperation crcPadControl, ChecksumOperation checksumControl, uint maximumSegmentSize, Action<EthernetFrame> frameReady, MACAddress? sourceMACAddress)
            {
                this.parent = parent;
                tcpHeader = header;
                this.sourceMACAddress = sourceMACAddress;

                padEthernetFrame = false;

                switch(crcPadControl)
                {
                    case CRCPadOperation.ReplaceCRC:
                        crcMode = CRCMode.Replace;
                        break;
                    case CRCPadOperation.None:
                        crcMode = CRCMode.Keep;
                        break;
                    case CRCPadOperation.InsetCRCAndPad:
                        padEthernetFrame = true;
                        goto case CRCPadOperation.InsertCRC;
                    case CRCPadOperation.InsertCRC:
                        crcMode = CRCMode.Add;
                        break;
                    default:
                        throw new Exception("Unreachable");
                }

                switch(checksumControl)
                {
                    case ChecksumOperation.None:
                        break;
                    case ChecksumOperation.InsertHeaderChecksum:
                        checksumTypes = null;
                        break;
                    case ChecksumOperation.InsertHeaderAndPayloadChecksum:
                        parent.Log(LogLevel.Warning, "Checksum Insertion Control: Calculating checksum with precalculated pseudo-header (0b10) is not supported. Falling back to calculating checksum with pseduo-header (0b11).");
                        checksumControl = ChecksumOperation.InsertHeaderAndPayloadChecksum;
                        goto case ChecksumOperation.InsertHeaderPayloadAndPseudoHeaderChecksum;
                    case ChecksumOperation.InsertHeaderPayloadAndPseudoHeaderChecksum:
                        checksumTypes = checksumOffloadEnginePseudoHeaderTypes;
                        break;
                    default:
                        throw new Exception("Unreachable");
                }
                checksumOp = checksumControl;

                if(SegmentationActive && maximumSegmentSize == 0)
                {
                    parent.Log(LogLevel.Error, "TCP Segmentation Offload: Ignoring invalid Maximum Segment Size value: {0}", maximumSegmentSize);
                }
                this.maximumSegmentSize = maximumSegmentSize;

                this.frameReady = frameReady;
                payloadSegments = new Queue<byte[]>();
            }

            private void FinalizeSegment(IEnumerable<byte> frame, uint length, bool isLast = false)
            {
                if(TryCreateEthernetFrame(tcpHeader?.Concat(frame) ?? frame, length + (uint?)tcpHeader?.Length ?? 0, out var builtFrame, isLast))
                {
                    frameReady(builtFrame);
                    packetsFinalized += 1;
                }
                else
                {
                    parent.Log(LogLevel.Error, "Failed to create EthernetFrame");
                }
            }

            private bool TryCreateEthernetFrame(IEnumerable<byte> frame, uint length, out EthernetFrame builtFrame, bool isLast)
            {
                if(padEthernetFrame && length < MinimalLength)
                {
                    frame = frame.Concat(Enumerable.Repeat<byte>(0, MinimalLength - (int)length));
                }
                var frameArray = frame.ToArray();
                if(SegmentationActive)
                {
                    // Update length field before packet creation to workaround Packet.Net asserts
                    var ipLength = length - EthernetFields.HeaderLength;
                    if(etherType == EtherType.IpV4)
                    {
                        EndianBitConverter.Big.CopyBytes((ushort)ipLength, frameArray, EthernetFields.HeaderLength + IPv4Fields.TotalLengthPosition);
                    }
                    else if(etherType == EtherType.IpV6)
                    {
                        var ipv6length = length - EthernetFields.HeaderLength;
                        EndianBitConverter.Big.CopyBytes((ushort)ipLength, frameArray, EthernetFields.HeaderLength + IPv6Fields.PayloadLengthPosition);
                    }
                }
                try
                {
                    if(!EthernetFrame.TryCreateEthernetFrame(frameArray, crcMode, out builtFrame))
                    {
                        return false;
                    }
                    if(SegmentationActive && builtFrame.UnderlyingPacket.PayloadPacket.PayloadPacket is TcpPacket tcpPacket)
                    {
                        var isFirst = packetsFinalized == 0;
                        if(!isFirst && etherType == EtherType.IpV4)
                        {
                            ((IPv4Packet)builtFrame.UnderlyingPacket.PayloadPacket).Id += (ushort)packetsFinalized;
                        }
                        if(!isFirst)
                        {
                            tcpPacket.SequenceNumber += packetsFinalized * maximumSegmentSize;
                        }
                        if(!isLast)
                        {
                            tcpPacket.Fin = false;
                            tcpPacket.Psh = false;
                        }
                    }
                    if(checksumOp != ChecksumOperation.None)
                    {
                        builtFrame.FillWithChecksums(checksumOffloadEngineIpHeaderTypes, checksumTypes, crcMode != CRCMode.Keep);
                    }
                    if(sourceMACAddress.HasValue)
                    {
                        builtFrame.UnderlyingPacket.SourceHwAddress = (PhysicalAddress)sourceMACAddress.Value;
                    }
                    return true;
                }
                catch(Exception e)
                {
                    builtFrame = null;
                    parent.Log(LogLevel.Error, "Underlying packet processing framework failed to create Ethernet frame: {0}", e);
                }
                return false;
            }

            private bool SegmentationActive => tcpHeader != null;

            private uint totalPayloadLength;
            private uint packetsFinalized;

            private readonly IEmulationElement parent;
            private readonly MACAddress? sourceMACAddress;
            private readonly byte[] tcpHeader;
            private readonly CRCMode crcMode;
            private readonly bool padEthernetFrame;
            private readonly ChecksumOperation checksumOp;
            private readonly IPProtocolType[] checksumTypes;
            private readonly uint maximumSegmentSize;
            private readonly Action<EthernetFrame> frameReady;
            private readonly Queue<byte[]> payloadSegments;
            private readonly EtherType etherType;

            private const int MinimalLength = 60;
        }
    }
}
