// Copyright (c) 2010-2025 Silicon Labs
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using Antmicro.Renode;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Backends.Terminals;
using Antmicro.Renode.Peripherals.Wireless;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Miscellaneous.SiLabs
{
  public static class PacketTraceAdapterExtensions
  {
    public static void CreatePacketTraceDebugAdapter(this Emulation emulation, SiLabs_IPacketTraceSniffer sniff, BackendTerminal term, string name, bool format = false)
    {
      emulation.ExternalsManager.AddExternal(new PacketTraceDebugAdapter(sniff, term, format), name);
    }
  }
  public sealed class PacketTraceDebugAdapter : IExternal
  {
    // DCH Message Types
    enum DchMessageType : UInt16
    {
      SnifferPacket     = 0x000A, // PTI Sniffer Packet
      PtiPacket         = 0x0020, // PTI Packet
      EfrPtiTxPacket    = 0x0029, // EFR32 PTI Tx Packet
      EfrPtiRxPacket    = 0x002A, // EFR32 PTI Rx Packet
      EfrPtiOtherPacket = 0x002B, // EFR32 PTI Other Packet
    }

    private readonly bool format;
    private readonly BackendTerminal term;
    private readonly Queue<byte> ptiBuff;
    private readonly SiLabs_IPacketTraceSniffer sniffer;
    private UInt16 dchFrameCounter = 0;
    private DchMessageType dchMessageType = DchMessageType.EfrPtiOtherPacket;
    public PacketTraceDebugAdapter(SiLabs_IPacketTraceSniffer sniffer, BackendTerminal term, bool format)
    {
      this.format = format;
      this.term = term;
      this.ptiBuff = new Queue<byte>();
      this.sniffer = sniffer;

      sniffer.PtiFrameStart += t =>
      {
        switch(t)
        {
          case SiLabs_PacketTraceFrameType.Receive:
            this.dchMessageType = DchMessageType.EfrPtiRxPacket;
            break;
          case SiLabs_PacketTraceFrameType.Transmit:
            this.dchMessageType = DchMessageType.EfrPtiTxPacket;
            break;
          default:
            this.dchMessageType = DchMessageType.EfrPtiOtherPacket;
            break;
        }
      };
      sniffer.PtiDataOut += p =>
      {
        foreach (var b in p)
        {
          this.ptiBuff.Enqueue(b);
        }
      };
      sniffer.PtiFrameComplete += PtiSendDchFrame;
    }

    //  Byte        Description
    //  -----       -----------------------------------------------------------------------------
    //   0          Initial framing: '['
    //   1          Length  \ LSB, includes everything except the framing '[' and ']' characters.
    //   2          Length  / MSB
    //   length-2   Individual debug message, dependent on the version and type.
    //   N          End framing:  ']'

    //  Byte        Description
    //  -----       -----------------------------------------------------------------------------
    //   0   Version number  \ LSB  ( contains 3 in version 3.0)
    //   1   Version number  / MSB  ( contains 0 in version 3.0)
    //   2   Timestamp byte 0  \ LSB, nanosecond tics
    //   3   Timestamp byte 1  |
    //   4   Timestamp byte 2  |
    //   5   Timestamp byte 3  |
    //   6   Timestamp byte 4  |
    //   7   Timestamp byte 5  |
    //   8   Timestamp byte 6  |
    //   9   Timestamp byte 7  / MSB
    //  10   Debug Message Type \ LSB (coresponds to single byte message type.
    //  11   Debug Message Type / MSB
    //  12   Flags \  LSB  (Reserved for future use, contains arbitrary values.)
    //  13   Flags |
    //  14   Flags |
    //  15   Flags /  MSB
    //  16   Sequence number \ LSB
    //  17   Sequence number / MSB
    //  ...  Debug-message-type-specific payload.
    private void PtiSendDchFrame()
    {
      // initial frame
      this.term.WriteChar(Convert.ToByte('['));
      // DCH Header
      using (var dchFrame = new MemoryStream(DCH_HEADER_LEN))
      {
        // length
        DchWrite(dchFrame, DchFrameLength());
        // version
        // NOTE version == 3.0, first byte is 3, second is 0
        DchWrite(dchFrame, new byte[] { 3, 0 });
        // timestamp
        DchWrite(dchFrame, this.sniffer.PacketTraceRadioTimestamp());
        // message type
        // NOTE 0x000A == 'Sniffer Packet'
        DchWrite(dchFrame, (UInt16)this.dchMessageType);
        // flags
        DchWrite(dchFrame, (UInt32)0);
        // sequence number
        DchWrite(dchFrame, this.dchFrameCounter++);
        this.Log(LogLevel.Noisy, "DCH Header: {0}", FormatHex(dchFrame.GetBuffer()));
        DchSendData(dchFrame.GetBuffer());
      }
      // PTI Data
      this.Log(LogLevel.Noisy, "PTI Data: {0}", FormatHex(this.ptiBuff));
      if (this.format)
      {
        this.term.WriteChar(Convert.ToByte('|'));
      }
      DchSendData(this.ptiBuff);
      // end frame
      this.term.WriteChar(Convert.ToByte(']'));
      // clear states
      this.ptiBuff.Clear();
      this.dchMessageType = DchMessageType.EfrPtiOtherPacket; // reset to default
    }

    private static void DchWrite(MemoryStream frame, IEnumerable<byte> bs) { foreach (byte b in bs) frame.WriteByte(b); }
    private static void DchWrite(MemoryStream frame, UInt16 s) => DchWrite(frame, DchEncodeBytes(BitConverter.GetBytes(s)));
    private static void DchWrite(MemoryStream frame, UInt32 i) => DchWrite(frame, DchEncodeBytes(BitConverter.GetBytes(i)));
    private static void DchWrite(MemoryStream frame, UInt64 l) => DchWrite(frame, DchEncodeBytes(BitConverter.GetBytes(l)));

    private static string FormatHex(IEnumerable<byte> data)
    {
      var hex = new StringBuilder(data.Count() * 2);
      foreach (byte b in data) hex.AppendFormat("{0:X2}", b);
      return hex.ToString();
    }

    private void DchSendData(IEnumerable<byte> data)
    {
      if (this.format)
      {
        foreach (char c in FormatHex(data)) this.term.WriteChar(Convert.ToByte(c));
      }
      else
      {
        foreach (byte b in data) this.term.WriteChar(b);
      }
    }

    static private byte[] DchEncodeBytes(byte[] bytes)
    {
      if (!BitConverter.IsLittleEndian)
      {
        Array.Reverse(bytes);
      }
      return bytes;
    }

    private const UInt16 DCH_HEADER_LEN =
      sizeof(UInt16)    // 2-bytes of length
      + sizeof(UInt16)  // 2-bytes of length
      + sizeof(UInt64)  // 8-bytes of timestamp
      + sizeof(UInt16)  // 2-bytes of message type
      + sizeof(UInt32)  // 4-bytes of flags
      + sizeof(UInt16); // 2-bytes of sequence number

    private UInt16 DchFrameLength()
    {
      return (UInt16)(DCH_HEADER_LEN + this.ptiBuff.Count);
    }
  }
}
