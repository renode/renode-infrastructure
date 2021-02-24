//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;


namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_VerilatorSwTestStatus : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_VerilatorSwTestStatus(Machine machine) : base(machine)
        {
                DefineRegisters();
        }

        public long Size =>  0x8;

        private enum SoftwareTestStatusCode : uint
        {
                kTestStatusDefault = 0x0000,
                kTestStatusInBootRom = 0xb090,  // 'bogo', BOotrom GO
                kTestStatusInTest = 0x4354,  // 'test'
                kTestStatusInWfi = 0x1d1e,  // 'idle'
                kTestStatusPassed = 0x900d,  // 'good'
                kTestStatusFailed = 0xbaad  // 'baad'
        }

        private void DefineRegisters()
        {
                Registers.SoftwareTestStatus.Define(this, 0x0)
                    .WithValueField(0, 16, name: "software test status", writeCallback: (_, value) =>
                    {
                        this.Log(LogLevel.Info, "Opentitan Software test status set to 0x{0:x}", value);
                        bool enumTestStatisIsDefined = System.Enum.IsDefined(typeof(SoftwareTestStatusCode), value);
                        if (enumTestStatisIsDefined) {
                                SoftwareTestStatusCode statusCode = (SoftwareTestStatusCode)value;
                                switch (statusCode) {
                                        case SoftwareTestStatusCode.kTestStatusInBootRom: 
                                                this.Log(LogLevel.Info, "Opentitan in boot ROM");
                                                break;
                                        case SoftwareTestStatusCode.kTestStatusInTest:
                                                this.Log(LogLevel.Info, "Opentitan in test");
                                                break;
                                        case SoftwareTestStatusCode.kTestStatusInWfi:
                                                this.Log(LogLevel.Info, "Opentitan in WFI");
                                                break;
                                        case SoftwareTestStatusCode.kTestStatusPassed:
                                                this.Log(LogLevel.Info, "Opentitan PASSED Test");
                                                break;
                                        case SoftwareTestStatusCode.kTestStatusFailed:
                                                this.Log(LogLevel.Info, "Opentitan FAILED Test");
                                                break;
                                }
                        }
                    })
                    .WithIgnoredBits(16,16)
                ;
        }

        private enum Registers
        {
            SoftwareTestStatus = 0x0
        }

    }
}
