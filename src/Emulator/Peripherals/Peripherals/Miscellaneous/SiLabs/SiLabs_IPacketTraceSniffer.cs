
// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
  // ref an1087-using-packet-trace-system.pdf
  // NOTE these enumerations are the 8-bit representation
  // the full 9-bit codes include an additional bit which is set for these commands
  // meaning the 9-bit representations can be determined with `0x100 | <8-bit-repr>`
  public enum PacketTraceFrameDelimiters : byte
  {
      RxStart = 0xF8,
      RxEndSuccess = 0xF9,
      RxEndAbort = 0xFA,
      TxStart = 0xFC,
      TxEndSuccess = 0xFD,
      TxEndAbort = 0xFE,
      SnifferOverflow = 0xFF,
  }

  public enum SiLabs_PacketTraceFrameType
  {
    Receive,
    Transmit,
    None
  }

  public interface SiLabs_IPacketTraceSniffer : IPeripheral
  {
    event Action<SiLabs_PacketTraceFrameType> PtiFrameStart;
    event Action<byte[]> PtiDataOut;
    event Action PtiFrameComplete;
    UInt64 PacketTraceRadioTimestamp();
  }
}
