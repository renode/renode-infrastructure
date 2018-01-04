//
// Copyright (c) 2010-2018 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.Wireless.CC2538
{
    internal enum InterruptSource
    {
        StartOfFrameDelimiter,
        FifoP,
        SrcMatchDone,
        SrcMatchFound,
        FrameAccepted,
        RxPktDone,
        RxMaskZero,

        TxAckDone,
        TxDone,
        RfIdle,
        CommandStrobeProcessorManualInterrupt,
        CommandStrobeProcessorStop,
        CommandStrobeProcessorWait
    }
}
