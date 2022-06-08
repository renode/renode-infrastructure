//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public enum OpenTitan_LifeCycleState
    {
#pragma warning disable format
        Raw             = 0x00,  // Raw life cycle state after fabrication where all functions are disabled.
        TestUnlocked0   = 0x01,  // Unlocked test state where debug functions are enabled.
        TestLocked0     = 0x02,  // Locked test state where where all functions are disabled.
        TestUnlocked1   = 0x03,  // Unlocked test state where debug functions are enabled.
        TestLocked1     = 0x04,  // Locked test state where where all functions are disabled.
        TestUnlocked2   = 0x05,  // Unlocked test state where debug functions are enabled.
        TestLocked2     = 0x06,  // Locked test state where debug all functions are disabled.
        TestUnlocked3   = 0x07,  // Unlocked test state where debug functions are enabled.
        TestLocked3     = 0x08,  // Locked test state where debug all functions are disabled.
        TestUnlocked4   = 0x09,  // Unlocked test state where debug functions are enabled.
        TestLocked4     = 0x0a,  // Locked test state where debug all functions are disabled.
        TestUnlocked5   = 0x0b,  // Unlocked test state where debug functions are enabled.
        TestLocked5     = 0x0c,  // Locked test state where debug all functions are disabled.
        TestUnlocked6   = 0x0d,  // Unlocked test state where debug functions are enabled.
        TestLocked6     = 0x0e,  // Locked test state where debug all functions are disabled.
        TestUnlocked7   = 0x0f,  // Unlocked test state where debug functions are enabled.
        Dev             = 0x10,  // Development life cycle state where limited debug functionality is available.
        Prod            = 0x11,  // Production life cycle state.
        Prod_end        = 0x12,  // Same as PROD, but transition into RMA is not possible from this state.
        Rma             = 0x13,  // RMA life cycle state.
        Scrap           = 0x14,  // SCRAP life cycle state where all functions are disabled.
        Post_transition = 0x15,  // This state is temporary and behaves the same way as SCRAP.
        Escalate        = 0x16,  // This state is temporary and behaves the same way as SCRAP.
        Invalid         = 0x17,  // This state is reported when the life cycle state encoding is invalid. This state is temporary and behaves the same way as SCRAP.
#pragma warning restore format
    }
}
