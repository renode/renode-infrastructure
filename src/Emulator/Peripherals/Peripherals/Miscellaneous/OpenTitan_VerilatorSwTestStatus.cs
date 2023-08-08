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
        public OpenTitan_VerilatorSwTestStatus(IMachine machine) : base(machine)
        {
            DefineRegisters();
        }

        public long Size =>  0x8;

        private void DefineRegisters()
        {
            Registers.SoftwareTestStatus.Define(this, 0x0)
                .WithValueField(0, 16, name: "software test status", writeCallback: (_, value) =>
                {
                    this.Log(LogLevel.Info, "Opentitan Software test status set to 0x{0:x}", value);
                    switch((SoftwareTestStatusCode)value)
                    {
                        case SoftwareTestStatusCode.InBootRom: 
                            this.Log(LogLevel.Info, "Opentitan in boot ROM");
                            break;
                        case SoftwareTestStatusCode.InTest:
                            this.Log(LogLevel.Info, "Opentitan in test");
                            break;
                        case SoftwareTestStatusCode.InWfi:
                            this.Log(LogLevel.Info, "Opentitan in WFI");
                            break;
                        case SoftwareTestStatusCode.Passed:
                            this.Log(LogLevel.Info, "Opentitan PASSED Test");
                            break;
                        case SoftwareTestStatusCode.Failed:
                            this.Log(LogLevel.Info, "Opentitan FAILED Test");
                            break;
                    }
                })
                .WithIgnoredBits(16,16)
            ;
        }

        private enum SoftwareTestStatusCode : uint
        {
            Default = 0x0000,
            InBootRom = 0xb090,  // 'bogo', BOotrom GO
            InTest = 0x4354,  // 'test'
            InWfi = 0x1d1e,  // 'idle'
            Passed = 0x900d,  // 'good'
            Failed = 0xbaad  // 'baad'
        }

        private enum Registers
        {
            SoftwareTestStatus = 0x0
        }
    }
}
