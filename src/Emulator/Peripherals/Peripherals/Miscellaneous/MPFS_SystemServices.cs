//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Extensions;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    [AllowedTranslations(AllowedTranslation.ByteToDoubleWord)]
    public class MPFS_SystemServices : IDoubleWordPeripheral, IKnownSize, IProvidesRegisterCollection<DoubleWordRegisterCollection>
    {
        public MPFS_SystemServices(IMachine machine, IMultibyteWritePeripheral flashMemory, IQuadWordPeripheral mailboxMemory)
        {
            sysbus = machine.GetSystemBus(this);
            flash = flashMemory;
            mailbox = mailboxMemory;
            registers = new DoubleWordRegisterCollection(this);
            IRQ = new GPIO();
            DefineRegisters();
        }

        public uint ReadDoubleWord(long offset)
        {
            return registers.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value)
        {
            registers.Write(offset, value);
        }

        public void Reset()
        {
            registers.Reset();
        }

        public GPIO IRQ { get; }

        public long Size => 0x100;

        public ulong SerialNumberLower { get; set; }

        public ulong SerialNumberUpper { get; set; }

        public DoubleWordRegisterCollection RegistersCollection => registers;

        private void DefineRegisters()
        {
            Registers.ServicesCR.Define(this)
                .WithFlag(0, out controlRequest)
                .WithTag("CONTROL_BUSY", 1, 1)
                .WithTag("CONTROL_ABORT", 2, 1)
                .WithTag("CONTROL_NOTIFY", 3, 1)
                .WithReservedBits(4, 12)
                .WithValueField(16, 7, out command)
                .WithValueField(23, 9, out commandOffset)
                .WithWriteCallback((_, __) =>
                    {
                        if(!controlRequest.Value)
                        {
                            return;
                        }
                        HandleRequest((Commands)command.Value, (uint)commandOffset.Value);
                        IRQ.Blink();
                    })
            ;

            Registers.ServicesSR.Define(this)
                .WithTag("STATUS_REQUEST", 0, 1)
                .WithTag("STATUS_BUSY", 1, 1)
                .WithTag("STATUS_ABORT", 2, 1)
                .WithTag("STATUS_NOTIFY", 3, 1)
                .WithReservedBits(4, 12)
                .WithEnumField<DoubleWordRegister, RequestResult>(16, 16, FieldMode.Read, valueProviderCallback: (_) => status)
            ;
        }

        private void HandleRequest(Commands opcode, uint offset)
        {
            controlRequest.Value = false;
            status = RequestResult.Success;
            switch(opcode)
            {
                case Commands.SerialNumberService:
                    GenerateSerialNumber(offset);
                    break;
                case Commands.SPICopy:
                    CopyData(offset);
                    break;
                default:
                    this.Log(LogLevel.Warning, "Unknown request: {0}", opcode);
                    status = RequestResult.Error;
                    break;
            }
        }

        private void GenerateSerialNumber(uint offset)
        {
            // the Serial Number is a 128bit value
            mailbox.WriteQuadWord(offset, SerialNumberLower);
            mailbox.WriteQuadWord(offset + 8, SerialNumberUpper);
        }

        private void CopyData(uint offset)
        {
            var destAddr = mailbox.ReadQuadWord(offset);
            var srcAddr = mailbox.ReadDoubleWordUsingQuadWord(offset + 8);
            var nBytes = mailbox.ReadDoubleWordUsingQuadWord(offset + 12);
            var bytes = flash.ReadBytes(srcAddr, (int)nBytes);
            sysbus.WriteBytes(bytes, destAddr);
        }

        private RequestResult status;
        private IFlagRegisterField controlRequest;
        private IValueRegisterField command;
        private IValueRegisterField commandOffset;

        private readonly IMultibyteWritePeripheral flash;
        private readonly IQuadWordPeripheral mailbox;
        private readonly IBusController sysbus;
        private readonly DoubleWordRegisterCollection registers;

        private enum Registers
        {
            SoftReset = 0x0,
            VDetector = 0x4,
            TVSControl = 0x8,
            TVSTempA = 0xC,
            TVSTempB = 0x10,
            TVSTempC = 0x14,
            TVSVoltA = 0x18,
            TVSVoltB = 0x1C,
            TVSVoltC = 0x20,
            TVSOutput0 = 0x24,
            TVSOutput1 = 0x28,
            TVSTrigger = 0x2C,
            TrimVDET1P05 = 0x30,
            TrimVDET1P8 = 0x34,
            TrimVDET2P5 = 0x38,
            TrimTVS = 0x3C,
            TrimGDET1P05 = 0x40,
            // RESERVED0 = 0x44,
            // RESERVED1 = 0x48,
            // RESERVED2 = 0x4C,
            ServicesCR = 0x50,
            ServicesSR = 0x54,
            UserDetectorSR = 0x58,
            UserDetectorCR = 0x5C,
            MSSSPICR = 0x60,
        }

        private enum Commands
        {
            SerialNumberService = 0x0,
            UsercodeService = 0x1,
            DesignInformationService = 0x2,
            DesignCertificateService = 0x3,
            ReadDigestsService = 0x4,
            QuerySecurityService = 0x5,
            ReadDebugInformationService = 0x6,
            ReadENVMParametersService = 0x7,
            // gap intended
            NonAuthenticatedPlaintextService = 0x10,
            AuthenticatedPlaintextService = 0x11,
            AuthenticatedCiphertextService = 0x12,
            // gap intended
            SecureNVMReadService = 0x18,
            DigitalSignatureRawFormatService = 0x19,
            DigitalSignatureDERFormatService = 0x1A,
            // gap intended
            PUFEmulationService = 0x20,
            NonceService = 0x21,
            IAPImageAuthenticationService = 0x22,
            BitstreamAuthenticationService = 0x23,
            // gap intended
            IAPProgramRequestByImageIndex = 0x42,
            IAPProgramRequestByImageAddress = 0x43,
            IAPVerifyRequestByImageIndex = 0x44,
            IAPVerifyRequestByImageAddress = 0x45,
            AutoUpdateService = 0x46,
            DigestCheckService = 0x47,
            // gap intended
            SPICopy = 0x50,
            // gap intended
            ProbeReadDebugService = 0x70,
            ProbeWriteDebugService = 0x71,
            LiveProbeChannelAService = 0x72,
            LiveProbeChannelBService = 0x73,
            MEMSelectDebugService = 0x74,
            MEMReadDebugService = 0x75,
            MEMWriteDebugService = 0x76,
            APBReadDebugService = 0x77,
            APBWriteDebugService = 0x78,
            DebugSnapshotService = 0x79,
            GenerateOTP = 0x7A,
            MatchOTP = 0x7B,
            UnlockDebugPasscodeService = 0x7C,
            OneWayPasscodeService = 0x7D,
            TerminateDebug = 0x7E
        }

        private enum RequestResult
        {
            Success = 0,
            Busy = 0xEF,
            Error = 0xFF
        }
    }
}
