//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Peripherals.SPI;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Core;
using Antmicro.Renode.Peripherals.Wireless.IEEE802_15_4;
using System.Linq;

namespace Antmicro.Renode.Peripherals.Wireless
{
    public class AT86RF233 : ISPIPeripheral, IRadio, IGPIOReceiver
    {
        public AT86RF233()
        {
            IRQ = new GPIO();
            Reset();
        }

        public void ReceiveFrame(byte[] frame, IRadio sender)
        {
            this.DebugLog("Frame of length {0} received.", frame.Length);
            if(frame.Length == 0)
            {
                // according to the documentation:
                // Received frames with a frame length field set to zero (invalid PHR) are not signaled to the microcontroller.
                this.DebugLog("Ignoring an empty frame.");
                return;
            }

            if(operatingMode == OperatingMode.RxAackOn || operatingMode == OperatingMode.RxOn)
            {
                HandleFrame(frame);
            }
            else
            {
                deferredFrameBuffer = frame;
                this.DebugLog("Radio is not listening right now - this frame is being deffered.");
            }
        }

        public void Reset()
        {
            autoCrc = true;
            accessMode = AccessMode.Command;
            frameBuffer = new byte[0];
            deferredFrameBuffer = null;
        }

        public void OnGPIO(int number, bool value)
        {
            if(number != 0)
            {
                this.Log(LogLevel.Warning, "Unexpected GPIO: {0}", number);
                return;
            }

            this.DebugLog("Chip select set to: {0}", value);
            if(value && accessMode == AccessMode.FrameBufferAccess && accessType == AccessType.ReadAccess)
            {
                accessMode = AccessMode.Command;
            }
        }

        public byte Transmit(byte data)
        {
            this.DebugLog("Byte received: 0x{0:X}", data);
            switch(accessMode)
            {
            case AccessMode.Command:
                HandleCommandByte(data);
                break;
            case AccessMode.RegisterAccess:
                var result = (byte)0;
                if(accessType == AccessType.ReadAccess)
                {
                    result = HandleRegisterRead((Registers)context);
                }
                else
                {
                    HandleRegisterWrite((Registers)context, data);
                }
                accessMode = AccessMode.Command;
                return result;
            case AccessMode.FrameBufferAccess:
                if(accessType == AccessType.ReadAccess)
                {
                    return HandleFrameBufferRead(context++);
                }
                else
                {
                    HandleFrameBufferWrite(context++, data);
                }
                break;
            default:
                this.Log(LogLevel.Warning, "Unsupported access mode: {0}", accessMode);
                break;
            }
            return 0;
        }

        public void FinishTransmission()
        {
        }

        public int Channel { get; set; }

        public event Action<IRadio, byte[]> FrameSent;

        public GPIO IRQ { get; private set; }

        private void HandleCommandByte(byte data)
        {
            // according to the documentation 'data' byte should be interpreted as follows:
            // 1 0 A A A A A A - Register read from address A A A A A A
            // 1 1 A A A A A A - Register write to address A A A A A A
            // 0 0 1 x x x x x - Frame buffer read
            // 0 1 0 x x x x x - Frame buffer write
            // 0 0 0 x x x x x - SRAM read
            // 0 1 0 x x x x x - SRAM write

            if((data & (1 << 7)) == 0)
            {
                accessMode = ((data >> 5) & 1) == 0 ? AccessMode.SramAccess : AccessMode.FrameBufferAccess;
            }
            else
            {
                accessMode = AccessMode.RegisterAccess;
            }
            accessType = (data & (1 << 6)) == 0 ? AccessType.ReadAccess : AccessType.WriteAccess;
            if(accessMode == AccessMode.RegisterAccess)
            {
                context = (byte)(data & 0x3F);
                this.DebugLog("Register 0x{0:X} {1} request", context, accessType == AccessType.ReadAccess ? "read" : "write");
            }
            else
            {
                context = -1;
                this.DebugLog("Command received: {0} {1}", accessMode.ToString(), accessType.ToString());
            }
        }

        private void HandleFrame(byte[] frame)
        {
            // fcs (crc) check
            if(frame.Length >= 2)
            {
                var crc = Frame.CalculateCRC(frame.Take(frame.Length - 2));
                if(frame[frame.Length - 2] != crc[0] || frame[frame.Length - 1] != crc[1])
                {
                    this.Log(LogLevel.Warning, "A frame with wrong CRC received - dropping it.");
                    return;
                }
            }
            else
            {
                this.Log(LogLevel.Warning, "Short frame (length {0}) received - CRC is not checked.", frame.Length);
            }

            if(operatingMode == OperatingMode.RxAackOn && Frame.IsAcknowledgeRequested(frame))
            {
                var frameSent = FrameSent;
                if(frameSent != null)
                {
                    var ackFrame = Frame.CreateAckForFrame(frame);
                    this.DebugLog("Sending automatic ACK for frame sequence number: {0}", ackFrame.DataSequenceNumber);
                    frameSent(this, ackFrame.Bytes);
                }
            }

            frameBuffer = frame;
            this.DebugLog("Setting IRQ");
            IRQ.Set();
        }

        private byte HandleRegisterRead(Registers register)
        {
            this.Log(LogLevel.Noisy, "Reading register {0}.", register);
            switch(register)
            {
            case Registers.TrxStatus:
                return (byte)((deferredFrameBuffer == null ? 0xC0 : 0x80) + (byte)operatingMode); // CCA_DONE + CCA_STATUS
            case Registers.TrxState:
                return (byte)operatingMode;
            case Registers.VersionNum:
                return 0x1; // Revision A
            case Registers.ManId0:
                return 0x1F; // Atmel JEDEC manufacturer ID
            case Registers.ManId1:
                return 0x0; // Atmel JEDEC manufacturer ID
            case Registers.TrxCtrl0:
                return 0x9; // CLKDM_SHA_SEL + CLKM_CTRL (1Mhz)
            case Registers.TrxCtrl2:
                return 0x20; // OQPSK_SCRAM_EN
            case Registers.IrqStatus:
                IRQ.Unset();
                // TODO: this probably should not be hardcoded
                return 0x8 + 0x20; // TRX_END + AMI
            case Registers.PhyCcCca:
                return (byte)(0x20 + Channel); // CCA_MODE + Channel
            case Registers.PhyEdLevel:
                return 0x53; // maximum ED level value
            case Registers.PhyRssi:
                return 0x9c; // RxCrcValid + Maximum RSSI value
            default:
                this.Log(LogLevel.Warning, "Read from unexpected register: 0x{0:X}", context);
                return 0;
            }
        }

        private void HandleRegisterWrite(Registers register, byte data)
        {
            this.Log(LogLevel.Noisy, "Writing register {0} with data {1:X}.", register, data);
            switch(register)
            {
            case Registers.TrxState:
                data &= 0x1f;
                if(!Enum.IsDefined(typeof(OperatingMode), data))
                {
                    this.Log(LogLevel.Warning, "Unknown mode: 0x{0:1}", data);
                    return;
                }
                operatingMode = (OperatingMode)data;
                this.Log(LogLevel.Info, "Entering {0} mode", operatingMode.ToString());
                if(operatingMode == OperatingMode.ForceTrxOff)
                {
                    operatingMode = OperatingMode.TrxOff;
                    this.Log(LogLevel.Info, "Entering {0} mode", operatingMode.ToString());
                }
                if((operatingMode == OperatingMode.RxOn || operatingMode == OperatingMode.RxAackOn) && deferredFrameBuffer != null)
                {
                    HandleFrame(deferredFrameBuffer);
                    deferredFrameBuffer = null;
                }
                break;
            case Registers.PhyCcCca:
                Channel = data & 0x1F;
                this.Log(LogLevel.Info, "Setting channel {0}", Channel);
                break;
            case Registers.TrxCtrl1:
                autoCrc = ((data & 0x20) != 0);
                this.Log(LogLevel.Info, "Auto CRC turned {0}", autoCrc ? "on" : "off");
                break;
            default:
                this.Log(LogLevel.Warning, "Write value 0x{0:X} to unexpected register: 0x{1:X}", data, context);
                break;
            }
        }

        private byte HandleFrameBufferRead(int index)
        {
            if(index == -1)
            {
                // this means we have to send PHR byte indicating frame length
                return (byte)frameBuffer.Length;
            }
            if(context < frameBuffer.Length)
            {
                return frameBuffer[index];
            }
            if(index == frameBuffer.Length)
            {
                // send LQI
                return 0xFF; // the best Link Quality Indication
            }
            if(index == frameBuffer.Length + 1)
            {
                // send ED
                return 0x53; // maximum Energy Detection level
            }
            if(index == frameBuffer.Length + 2)
            {
                accessMode = AccessMode.Command;
                // send RX_STATUS
                return 0x80; // CRC ok
            }
            return 0;
        }

        private void HandleFrameBufferWrite(int index, byte data)
        {
            if(index == -1)
            {
                // this is PHR
                frameBuffer = new byte[data];
            }
            else
            {
                frameBuffer[index] = data;
            }
            if(index == frameBuffer.Length - 1)
            {
                SendPacket();
                accessMode = AccessMode.Command;
            }
        }

        private void SendPacket()
        {
            var frameSent = FrameSent;
            if(frameSent == null)
            {
                this.NoisyLog("Could not sent packet as there is no frame handler attached.");
                return;
            }

            if(autoCrc)
            {
                if(frameBuffer.Length >= 2)
                {
                    // replace buffer's last two bytes with calculated CRC
                    var frameDataLenght = frameBuffer.Length - 2;
                    var crc = Frame.CalculateCRC(frameBuffer.Take(frameDataLenght));
                    Array.Copy(crc, 0, frameBuffer, frameDataLenght, 2);
                }
                else
                {
                    this.Log(LogLevel.Warning, "Sending short frame (length {0}) - CRC is not calculated.", frameBuffer.Length);
                }
            }

            frameSent(this, frameBuffer);
            this.DebugLog("Frame of length {0} sent.", frameBuffer.Length);
        }

        private OperatingMode operatingMode;
        private bool autoCrc;
        private int context;
        private AccessMode accessMode;
        private AccessType accessType;
        private byte[] frameBuffer;
        // this is used to hold the last frame received when radio was in TrxOff mode;
        // conserving energy can lead to disabling radio (going into TrxOff mode) and
        // turning it on only periodically; during those short period network acticity
        // is assessed by monitoring channel state (CCA); in the emulator network communication
        // takes no time so it is problematic to decide if CCA should return true or false;
        // here comes the trick: if there was any frame received during TrxOff mode CCA
        // returns channel busy state and only when the radio switches to RxOn mode the frame
        // is actualy received
        private byte[] deferredFrameBuffer;

        private enum OperatingMode : byte
        {
            ForceTrxOff = 0x3,
            RxOn = 0x6,
            TrxOff = 0x8,
            PllOn = 0x9,
            RxAackOn = 0x16,
            TxAretOn = 0x19
        }

        private enum AccessMode
        {
            Command,
            RegisterAccess,
            FrameBufferAccess,
            SramAccess
        }

        private enum AccessType
        {
            ReadAccess,
            WriteAccess
        }

        private enum Registers
        {
            TrxStatus = 0x1,
            TrxState = 0x2,
            TrxCtrl0 = 0x3,
            TrxCtrl1 = 0x4,
            PhyRssi = 0x6,
            PhyEdLevel = 0x7,
            PhyCcCca = 0x8,
            TrxCtrl2 = 0xC,
            IrqStatus = 0xF,
            VersionNum = 0x1D,
            ManId0 = 0x1E,
            ManId1 = 0x1F
        }
    }
}
