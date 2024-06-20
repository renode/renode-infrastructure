//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.CPU;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class ZynqMP_PlatformManagementUnit : IGPIOReceiver
    {
        public ZynqMP_PlatformManagementUnit(ICPU apu0, ICPU apu1, ICPU apu2, ICPU apu3, ICPU rpu0, ICPU rpu1)
        {
            this.apu0 = apu0;
            this.apu1 = apu1;
            this.apu2 = apu2;
            this.apu3 = apu3;
            this.rpu0 = rpu0;
            this.rpu1 = rpu1;
            registeredPeripherals[apu0] = new HashSet<IPeripheral>();
            registeredPeripherals[apu1] = new HashSet<IPeripheral>();
            registeredPeripherals[apu2] = new HashSet<IPeripheral>();
            registeredPeripherals[apu3] = new HashSet<IPeripheral>();
            registeredPeripherals[rpu0] = new HashSet<IPeripheral>();
            registeredPeripherals[rpu1] = new HashSet<IPeripheral>();
            powerManagement = new PowerManagementModule(this);
        }

        public void OnGPIO(int number, bool value)
        {
            if(value)
            {
                HandleInterruptOnIpi((PmuIpiChannel)number);
            }
        }

        public void Reset()
        {
            powerManagement.Reset();
        }

        public void RegisterIPI(ZynqMP_IPI ipi)
        {
            this.ipi = ipi;
        }

        public void RegisterPeripheral(ICPU cpu, IPeripheral peripheral)
        {
            if(registeredPeripherals.ContainsKey(cpu))
            {
                registeredPeripherals[cpu].Add(peripheral);
            }
            else
            {
                throw new ConstructionException("Trying to register peripheral on invalid CPU.");
            }
        }

        private void HandleInterruptOnIpi(PmuIpiChannel channel)
        {
            // PMU is hardwired to channels 3-6
            switch(channel)
            {
                case PmuIpiChannel.PMU0:
                    ProcessInterruptsOnIpiChannel(ZynqMP_IPI.ChannelId.Channel3);
                    break;
                case PmuIpiChannel.PMU1:
                    ProcessInterruptsOnIpiChannel(ZynqMP_IPI.ChannelId.Channel4);
                    break;
                case PmuIpiChannel.PMU2:
                    ProcessInterruptsOnIpiChannel(ZynqMP_IPI.ChannelId.Channel5);
                    break;
                case PmuIpiChannel.PMU3:
                    ProcessInterruptsOnIpiChannel(ZynqMP_IPI.ChannelId.Channel6);
                    break;
                default:
                    this.Log(LogLevel.Error, "Trying to signal GPIO {0} which is out of range.", (int)channel);
                    break;
            }
        }

        private void ProcessInterruptsOnIpiChannel(ZynqMP_IPI.ChannelId targetChannelId)
        {
            var interrupts = GetInterruptsSourcesAndClearMaskedChannels(targetChannelId);
            foreach(ZynqMP_IPI.ChannelId sourceChannelId in Enum.GetValues(typeof(ZynqMP_IPI.ChannelId)))
            {
                if(sourceChannelId != ZynqMP_IPI.ChannelId.None && interrupts.HasFlag(sourceChannelId))
                {
                    ProcessSingleInterruptOnChannel(targetChannelId, sourceChannelId);
                }
            }
        }

        private ZynqMP_IPI.ChannelId GetInterruptsSourcesAndClearMaskedChannels(ZynqMP_IPI.ChannelId channelId)
        {
            // We don't need to handle exceptions as we use hardcoded values.
            // Exception would indicate error in PMU code rather than in emulated software.
            var imrOffset = ZynqMP_IPI.GetRegisterOffset(channelId, ZynqMP_IPI.RegisterOffset.Mask);
            var isrOffset = ZynqMP_IPI.GetRegisterOffset(channelId, ZynqMP_IPI.RegisterOffset.StatusAndClear);

            var imr = ipi.ReadDoubleWord(imrOffset);
            var isr = ipi.ReadDoubleWord(isrOffset);

            // We still should clear unhandled interrupts
            var maskedInterrupts = (ZynqMP_IPI.ChannelId)(imr & isr);
            ClearInterruptsOnChannel(channelId, maskedInterrupts);

            return (ZynqMP_IPI.ChannelId)(~imr & isr);
        }

        private void ClearInterruptsOnChannel(ZynqMP_IPI.ChannelId channelId, ZynqMP_IPI.ChannelId interrupts)
        {
            // We don't need to handle exceptions as we use hardcoded values.
            // Exception would indicate error in PMU code rather than in emulated software.
            var isrOffset = ZynqMP_IPI.GetRegisterOffset(channelId, ZynqMP_IPI.RegisterOffset.StatusAndClear);
            foreach(ZynqMP_IPI.ChannelId sourceChannelId in Enum.GetValues(typeof(ZynqMP_IPI.ChannelId)))
            {
                if(sourceChannelId != ZynqMP_IPI.ChannelId.None && interrupts.HasFlag(sourceChannelId))
                {
                    ipi.WriteDoubleWord(isrOffset, (uint)sourceChannelId);
                }
            }
        }

        private void ProcessSingleInterruptOnChannel(ZynqMP_IPI.ChannelId targetChannelId, ZynqMP_IPI.ChannelId sourceChannelId)
        {
            IpiMessage message = ReadMessageFromMailbox(sourceChannelId);
            IpiMessage response = ProcessIpiMessage(message);
            WriteMessageToMailbox(sourceChannelId, response);

            // We clear interrupt to notify source
            var isrOffset = ZynqMP_IPI.GetRegisterOffset(targetChannelId, ZynqMP_IPI.RegisterOffset.StatusAndClear);
            ipi.WriteDoubleWord(isrOffset, (uint)sourceChannelId);
        }

        private IpiMessage ProcessIpiMessage(IpiMessage message)
        {
            // 8 most significant bits of header indicate which PMU module should handle message
            this.Log(LogLevel.Debug, "Processing message with Header = 0x{0:X}", message.Header);
            var moduleId = (PmuModule)(message.Header >> 16);
            switch(moduleId)
            {
                case PmuModule.PowerManagement:
                    return powerManagement.HandleMessage(message);
                default:
                    this.Log(LogLevel.Warning, "Received call for PMU module with ID {0} which is not implemented.", (uint)moduleId);
                    // PMU don't handle messages with wrong module id, so we return empty message
                    return new IpiMessage();
            }
        }

        private IpiMessage ReadMessageFromMailbox(ZynqMP_IPI.ChannelId sourceId)
        {
            // We don't need to handle exceptions as we iterate over valid channel ids.
            // Exception would indicate error in PMU code rather than in emulated software.
            var sourceMailboxAddress = ZynqMP_IPI.GetMailboxOffset(sourceId);

            var message = new IpiMessage();
            message.Header = ipi.mailbox.ReadDoubleWord(sourceMailboxAddress + IpiMessage.HeaderOffset);
            for(var payloadIdx = 0; payloadIdx < IpiMessage.PayloadLen; ++payloadIdx)
            {
                var payloadAddress = sourceMailboxAddress + IpiMessage.PayloadOffset + IpiMessage.FieldSize * payloadIdx;
                message.Payload[payloadIdx] = ipi.mailbox.ReadDoubleWord(payloadAddress);
            }
            message.Reserved = ipi.mailbox.ReadDoubleWord(sourceMailboxAddress + IpiMessage.ReservedOffset);
            message.Checksum =  ipi.mailbox.ReadDoubleWord(sourceMailboxAddress + IpiMessage.ChecksumOffset);

            return message;
        }

        private void WriteMessageToMailbox(ZynqMP_IPI.ChannelId targetId, IpiMessage message)
        {
            // We don't need to handle exceptions as we iterate over valid channel ids.
            // Exception would indicate error in PMU code rather than in emulated software.
            var targetMailboxOffset = ZynqMP_IPI.GetMailboxOffset(targetId);
            targetMailboxOffset += MailboxLocalResponseOffset;

            ipi.mailbox.WriteDoubleWord(targetMailboxOffset + IpiMessage.HeaderOffset, message.Header);
            for(var payloadIdx = 0; payloadIdx < IpiMessage.PayloadLen; ++payloadIdx)
            {
                var payloadAddress = targetMailboxOffset + IpiMessage.PayloadOffset + IpiMessage.FieldSize * payloadIdx;
                ipi.mailbox.WriteDoubleWord(payloadAddress, message.Payload[payloadIdx]);
            }
            ipi.mailbox.WriteDoubleWord(targetMailboxOffset + IpiMessage.ReservedOffset, message.Reserved);
            ipi.mailbox.WriteDoubleWord(targetMailboxOffset + IpiMessage.ChecksumOffset, message.Checksum);
        }

        private readonly PowerManagementModule powerManagement;
        private readonly Dictionary<ICPU, ISet<IPeripheral>> registeredPeripherals = new Dictionary<ICPU, ISet<IPeripheral>>();

        private ZynqMP_IPI ipi;
        private ICPU apu0;
        private ICPU apu1;
        private ICPU apu2;
        private ICPU apu3;
        private ICPU rpu0;
        private ICPU rpu1;

        private const long MailboxLocalResponseOffset = 0x20;

        private class IpiMessage
        {
            public static IpiMessage CreateSuccessResponse()
            {
                var message = new IpiMessage();
                message.Header = (uint)IpiResponseHeader.Success;
                return message;
            }

            public static IpiMessage CreateInvalidParamResponse()
            {
                var response = new IpiMessage();
                response.Header = (uint)IpiResponseHeader.InvalidParam;
                return response;
            }

            public uint Header = 0;
            public uint[] Payload = new uint[PayloadLen];
            public uint Reserved = 0;
            public uint Checksum = 0;

            public const long HeaderOffset = 0x0;
            public const long PayloadOffset = 0x4;
            public const long ReservedOffset = 0x18;
            public const long ChecksumOffset = 0x1c;
            public const long FieldSize = 0x4;
            public const uint PayloadLen = 5;

            private enum IpiResponseHeader
            {
                Success = 0x0,
                InvalidParam = 0xf
            }
        }

        private class PowerManagementModule
        {
            public PowerManagementModule(ZynqMP_PlatformManagementUnit pmu)
            {
                this.pmu = pmu;
                resetStatus = new Dictionary<uint, uint>();
            }

            public void Reset()
            {
                resetStatus.Clear();
            }

            public IpiMessage HandleMessage(IpiMessage message)
            {
                var apiId = (PmApi)message.Header;
                switch(apiId)
                {
                    case PmApi.GetApiVersion:
                        return HandleGetApiVersion();
                    case PmApi.ForcePowerdown:
                        return HandleForcePowerdown(message);
                    case PmApi.RequestWakeup:
                        return HandleRequestWakeup(message);
                    case PmApi.ResetAssert:
                        return HandleResetAssert(message);
                    case PmApi.ResetGetStatus:
                        return HandleResetGetStatus(message);
                    case PmApi.ClockGetDivider:
                        return HandleClockGetDivider(message);
                    case PmApi.PllGetParameter:
                        return HandlePllGetParameter(message);
                    default:
                        return HandleDefault();
                }
            }

            private IpiMessage HandleGetApiVersion()
            {
                var response = IpiMessage.CreateSuccessResponse();
                response.Payload[0] = ApiVersion;
                return response;
            }

            private IpiMessage HandleForcePowerdown(IpiMessage message)
            {
                var node = (Node)message.Payload[0];
                var ack = (RequestAck)message.Payload[1];
                var response = IpiMessage.CreateSuccessResponse();
                var cpu = GetCpuFromNode(node);
                if (cpu != null)
                {
                    cpu.IsHalted = true;
                    cpu.Reset();
                    foreach(var peripheral in pmu.registeredPeripherals[cpu])
                    {
                        peripheral.Reset();
                    }

                    try
                    {
                        return CreateAckResponse(response, ack);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        pmu.Log(LogLevel.Warning, "Received invalid ack request with value {0}.", ack);
                        return new IpiMessage();
                    }
                }

                pmu.Log(LogLevel.Warning, "Received shutdown request for node with id {0} which is not implemented.", (uint)node);
                return IpiMessage.CreateInvalidParamResponse();
            }

            private IpiMessage CreateAckResponse(IpiMessage response, RequestAck ack)
            {
                switch(ack)
                {
                    case RequestAck.AckNo:
                        // AckNo means we shouldn't do anything so we return empty message
                        return new IpiMessage();
                    case RequestAck.AckBlocking:
                        return response;
                    case RequestAck.AckNonBlocking:
                        pmu.Log(LogLevel.Warning, "Requested non blocking ACK which is not implemented.");
                        return new IpiMessage();
                    default:
                        throw new ArgumentOutOfRangeException("RequestAck");
                }
            }

            private IpiMessage HandleRequestWakeup(IpiMessage message)
            {
                var node = (Node)message.Payload[0];
                var pc = ((ulong)message.Payload[2] << 32) | (ulong)message.Payload[1];
                var cpu = GetCpuFromNode(node);
                if (cpu != null)
                {
                    // First bit of new PC indicated whether we update PC
                    if ((pc & 1) == 1)
                    {
                        pc = pc & ~1UL;
                        pmu.Log(LogLevel.Debug, "Set PC of node {0} to 0x{1:X}.", node, pc);
                        cpu.PC = pc;
                    }
                    pmu.Log(LogLevel.Debug, "Unhalt node {0}.", node);
                    cpu.IsHalted = false;

                    return IpiMessage.CreateSuccessResponse();
                } 

                pmu.Log(LogLevel.Warning, "Received wakeup request for node with id {0} which is not implemented.", (uint)node);
                return IpiMessage.CreateInvalidParamResponse();
            }

            private ICPU GetCpuFromNode(Node node)
            {
                switch(node)
                {
                    case Node.APU0:
                        return pmu.apu0;
                    case Node.APU1:
                        return pmu.apu1;
                    case Node.APU2:
                        return pmu.apu2;
                    case Node.APU3:
                        return pmu.apu3;
                    case Node.RPU0:
                        return pmu.rpu0;
                    case Node.RPU1:
                        return pmu.rpu1;
                    default:
                        return null;
                }
            }

            private IpiMessage HandleResetAssert(IpiMessage message)
            {
                var key = message.Payload[0];
                var val = message.Payload[1];
                resetStatus[key] = val;
                return IpiMessage.CreateSuccessResponse();
            }

            private IpiMessage HandleResetGetStatus(IpiMessage message)
            {
                var key = message.Payload[0];
                var response = IpiMessage.CreateSuccessResponse();
                response.Payload[0] = 0;
                resetStatus.TryGetValue(key, out response.Payload[0]);
                return response;
            }

            private IpiMessage HandleClockGetDivider(IpiMessage message)
            {
                var clock = (Clock)message.Payload[0];
                var divider = (ClockDivider)message.Payload[1];

                var response = IpiMessage.CreateSuccessResponse();

                if(clock == Clock.Uart1Ref)
                {
                    if(divider == ClockDivider.Div0)
                    {
                        response.Payload[0] = (CrlApbUart1RefCtrl >> ClockDivider0Shift) & ClockDividerMask;
                    }
                    else if(divider == ClockDivider.Div1)
                    {
                        response.Payload[0] = (CrlApbUart1RefCtrl >> ClockDivider1Shift) & ClockDividerMask;
                    }
                    else
                    {
                        return IpiMessage.CreateInvalidParamResponse();
                    }
                }
                return response;
            }

            private IpiMessage HandlePllGetParameter(IpiMessage message)
            {
                var pllNode = (Node)message.Payload[0];
                var pllParam = (PllParameter)message.Payload[1];

                var resetValue = GetResetValueFromPllNode(pllNode);
                var shift = GetShiftFromPllParam(pllParam);
                var mask = GetMaskFromPllParam(pllParam);

                var response = IpiMessage.CreateSuccessResponse();
                response.Payload[0] = (resetValue >> shift) & mask;
                return response;
            }

            private uint GetResetValueFromPllNode(Node pllNode)
            {
               switch(pllNode)
               {
                    case Node.APll:
                        return 0x00012c09;
                    case Node.DPll:
                        return 0x00012809;
                    case Node.RPll:
                        return 0x00002c09;
                    case Node.VPll:
                        return 0x00012c09;
                    case Node.IOPll:
                        return 0x00012c09;
                    default:
                        return 0x0;
               }
            }

            private int GetShiftFromPllParam(PllParameter param)
            {
                switch(param)
                {
                    case PllParameter.Div2:
                        return 16;
                    case PllParameter.FbDiv:
                        return 8;
                    case PllParameter.PreSrc:
                        return 20;
                    case PllParameter.PostSrc:
                        return 24;
                    default:
                        return 0;
                }
            }

            private uint GetMaskFromPllParam(PllParameter param)
            {
                switch(param)
                {
                    case PllParameter.Div2:
                        return 0x1;
                    case PllParameter.FbDiv:
                        return 0x7f;
                    case PllParameter.PreSrc:
                        return 0x7;
                    case PllParameter.PostSrc:
                        return 0x7;
                    default:
                        return 0x0;
                }
            }

            private IpiMessage HandleDefault()
            {
                return IpiMessage.CreateSuccessResponse();
            }

            private readonly ZynqMP_PlatformManagementUnit pmu;
            private readonly Dictionary<uint, uint> resetStatus;

            private const uint ApiVersion = 0x10001;
            private const uint ClockDividerMask = 0x3f;
            private const int ClockDivider0Shift = 8;
            private const int ClockDivider1Shift = 16;
            private const uint CrlApbUart1RefCtrl = 0x1001800;

            // We only list clocks that we need.
            // This enum corresponds to XPmClock enum in PMU FW source code.
            private enum Clock
            {
                Uart1Ref = 0x39
            }

            private enum ClockDivider
            {
                Div0 = 0,
                Div1 = 1
            }

            // We only list nodes that we need.
            // This enum corresponds to XPmNodeId enum in PMU FW source code.
            private enum Node
            {
                APU0    = 0x2,
                APU1    = 0x3,
                APU2    = 0x4,
                APU3    = 0x5,
                RPU0    = 0x7,
                RPU1    = 0x8,
                APll    = 0x32,
                VPll    = 0x33,
                DPll    = 0x34,
                RPll    = 0x35,
                IOPll   = 0x36
            }

            // We only list PLL params that we need.
            // This enum corresponds to XPmPllParam enum in PMU FW source code.
            private enum PllParameter
            {
                Div2    = 0x0,
                FbDiv   = 0x1,
                PreSrc  = 0x3,
                PostSrc = 0x4
            }

            private enum RequestAck
            {
                AckNo = 1,
                AckBlocking = 2,
                AckNonBlocking = 3
            }

            // We only list API ids that we need.
            // This enum corresponds to XPm_ApiId enum in PMU FW source code.
            private enum PmApi
            {
                ApiMin          = 0x0,
                GetApiVersion   = 0x1,
                ForcePowerdown  = 0x8,
                RequestWakeup   = 0xa,
                ResetAssert     = 0x11,
                ResetGetStatus  = 0x12,
                ClockGetDivider = 0x28,
                PllGetParameter = 0x31,
                ApiMax          = 0x4a
            }
        };

        private enum PmuIpiChannel
        {
            PMU0 = 0,
            PMU1 = 1,
            PMU2 = 2,
            PMU3 = 3
        }

        // Right now we only need to handle calls to PM module
        private enum PmuModule
        {
            PowerManagement = 0x0
        }
    }
}
