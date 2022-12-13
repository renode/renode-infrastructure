//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC660K : AtCommandModem, IGPIOReceiver
    {
        public Quectel_BC660K(Machine machine, string imeiNumber = DefaultImeiNumber) : base(machine)
        {
            this.imeiNumber = imeiNumber;

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            mtResultCodeMode = MobileTerminationResultCodeMode.Disabled;
            dataBuffer = new MemoryStream();
            Enabled = false;
        }

        public override void PassthroughWriteChar(byte value)
        {
            // Variable-length data mode - ^Z confirms, Esc cancels
            if(dataBytesRemaining == null)
            {
                if(value == ControlZ)
                {
                    ExitDataMode(true);
                }
                else if(value == Escape)
                {
                    ExitDataMode(false);
                    // Send OK manually because we don't call the data callback
                    SendResponse(Ok);
                }
                else
                {
                    dataBuffer.WriteByte(value);
                }
            }
            else // Fixed-length data mode, no special character handling
            {
                dataBuffer.WriteByte(value);
                if(--dataBytesRemaining == 0)
                {
                    ExitDataMode(true);
                }
            }
        }

        public void OnGPIO(int number, bool value)
        {
            this.Log(LogLevel.Debug, "GPIO {0} -> {1}", (GPIOInput)number, value);
            switch((GPIOInput)number)
            {
                case GPIOInput.Power:
                    // We treat turning off the power as equivalent to a reset
                    // The power input is active-low, that is false means enabled
                    if(value)
                    {
                        Reset(); // This model is disabled by a reset
                    }
                    else
                    {
                        Enabled = true;
                        // Notify the DTE that the modem is ready
                        SendResponse(new Response(ModemReady));
                    }
                    break;
                default:
                    this.Log(LogLevel.Error, "Got GPIO state {0} for unknown input {1}", value, number);
                    break;
            }
        }

        private Response MobileTerminationError(int errorCode)
        {
            switch(mtResultCodeMode)
            {
                case MobileTerminationResultCodeMode.Disabled:
                    return Error;
                case MobileTerminationResultCodeMode.Numeric:
                    return new Response($"+CME ERROR: {errorCode}");
                case MobileTerminationResultCodeMode.Verbose:
                    this.Log(LogLevel.Warning, "Verbose MT error reporting is not implemented");
                    goto case MobileTerminationResultCodeMode.Numeric;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void EnterDataMode(int? byteCount, Action<byte[]> callback)
        {
            if(byteCount == 0)
            {
                this.Log(LogLevel.Warning, "Tried to enter data mode with a fixed length of 0 bytes, ignoring");
                return;
            }

            PassthroughMode = true;
            dataBytesRemaining = byteCount;
            dataCallback = callback;
            SendString(DataModePrompt);
        }

        private void ExitDataMode(bool callCallback)
        {
            if(callCallback)
            {
                dataCallback(dataBuffer.ToArray());
            }
            dataBuffer.SetLength(0);
            PassthroughMode = false;
        }

        // CSQ - Signal Quality Report
        [AtCommand("AT+CSQ")]
        private Response Csq() => Ok.WithParameters("+CSQ: 22,0");

        // CGMI - Request Manufacturer Identification
        [AtCommand("AT+CGMI")]
        private Response Cgmi() => Ok.WithParameters(Vendor, ModelName, Revision);

        // CGMM - Request Model Identification
        [AtCommand("AT+CGMM")]
        private Response Cgmm() => Ok.WithParameters(ModelName);

        // CGMR - Request Manufacturer Revision
        [AtCommand("AT+CGMR")]
        private Response Cgmr() => Ok.WithParameters(ManufacturerRevision);

        // CGSN - Request Product Serial Number
        [AtCommand("AT+CGSN")]
        private Response Cgsn() => Ok.WithParameters(imeiNumber);

        // CMEE - Report Mobile Termination Error
        [AtCommand("AT+CMEE", CommandType.Write)]
        private Response Cmee(MobileTerminationResultCodeMode mode)
        {
            mtResultCodeMode = mode;
            this.Log(LogLevel.Debug, "CMEE result mode set to {0}", mode);
            return Ok;
        }

        [AtCommand("AT+CMEE", CommandType.Read)]
        private Response CmeeRead() => Ok.WithParameters($"+CMEE: {((int)mtResultCodeMode)}");

        // QIOPEN - Open a Socket Service
        [AtCommand("AT+QIOPEN", CommandType.Write)]
        private Response Qiopen(int contextId, int connectionId, ServiceType serviceType, string host, ushort remotePort, ushort localPort = 0, int accessMode = 1)
        {
            this.Log(LogLevel.Warning, "Context {0} connectionId {1} requested {2} connection open to {3}:{4}, not implemented",
                contextId, connectionId, serviceType, host, remotePort);

            // We can't just send Ok.WithTrailer because the driver won't see the URC.
            ExecuteWithDelay(() => SendResponse(new Response($"+QIOPEN: {connectionId},0")));
            return Ok;
        }

        // QISEND - Send Hex/Text String Data
        [AtCommand("AT+QISEND", CommandType.Write)]
        private Response Qisend(int connectionId, int? sendLength = null, string data = null)
        {
            // Check the total lengths of data sent, acknowledged and not acknowledged
            if(sendLength == 0)
            {
                this.Log(LogLevel.Warning, "Sent data counters not implemented, returning 0");
                return Ok.WithParameters("+QISEND: 0,0,0");
            }
            else if(data != null) // Send data in non-data mode
            {
                // Non-data mode is only supported with a fixed length
                if(sendLength == null || data.Length != sendLength)
                {
                    return Error;
                }
                this.Log(LogLevel.Warning, "ConnectionId {0} requested send of '{1}' in non-data mode, not implemented",
                    connectionId, data);
            }
            else // Send data (fixed or variable-length) in data mode
            {
                // We need to wait a while before sending the data mode prompt.
                ExecuteWithDelay(() =>
                {
                    // We throw away the sent bytes and just return SEND OK
                    EnterDataMode(sendLength, bytes =>
                    {
                        this.Log(LogLevel.Warning, "ConnectionId {0} requested send of '{1}' in data mode, not implemented",
                            connectionId, BitConverter.ToString(bytes));
                        SendResponse(Ok.WithTrailer(SendOk));
                    });
                });
            }
            return Ok;
        }

        // When this is set to Numeric or Verbose, MT-related errors are reported with "+CME ERROR: "
        // instead of the plain "ERROR". This does not apply to syntax errors, invalid parameter
        // errors or Terminal Adapter functionality.
        private MobileTerminationResultCodeMode mtResultCodeMode;
        private MemoryStream dataBuffer;
        private int? dataBytesRemaining;
        private Action<byte[]> dataCallback;
        private readonly string imeiNumber;

        private const string Vendor = "Quectel_Ltd";
        private const string ModelName = "Quectel_BC660K-GL";
        private const string Revision = "Revision: QCX212";
        private const string ManufacturerRevision = "Revision: BC660KGLAAR01A0";
        private const string DefaultImeiNumber = "866818039921444";
        private const string DataModePrompt = ">";
        private const string SendOk = "SEND OK";
        private const string ModemReady = "RDY";

        private enum MobileTerminationResultCodeMode
        {
            Disabled,
            Numeric,
            Verbose,
        }

        private enum ServiceType
        {
            Tcp,
            Udp,
            TcpListener,
            UdpService,
        }

        private enum GPIOInput
        {
            Power,
        }
    }
}
