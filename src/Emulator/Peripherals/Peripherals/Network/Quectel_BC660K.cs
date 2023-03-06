//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC660K : AtCommandModem, IGPIOReceiver, INumberedGPIOOutput
    {
        public Quectel_BC660K(Machine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine)
        {
            this.imeiNumber = imeiNumber;
            this.softwareVersionNumber = softwareVersionNumber;
            this.serialNumber = serialNumber;
            Connections = new Dictionary<int, IGPIO>
            {
                {0, vddExt},
            };

            Reset();
        }

        public override void Reset()
        {
            base.Reset();
            mtResultCodeMode = MobileTerminationResultCodeMode.Disabled;
            dataBuffer = new MemoryStream();
            dataBytesRemaining = null;
            dataCallback = null;
            inReset = false;
            echoInDataMode = false;
            Enabled = false;
            vddExt.Unset();
        }

        public override void PassthroughWriteChar(byte value)
        {
            if(echoInDataMode)
            {
                SendChar((char)value);
            }

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
                    // Pulling down the Power Key pin means to turn on the modem.
                    // The modem cannot be turned on while it is in reset.
                    if(!value && !inReset)
                    {
                        EnableModem();
                    }
                    break;
                case GPIOInput.Reset:
                    // We assume the reset completes immediately, and the modem is held in reset
                    // as long as the reset pin is low.
                    inReset = !value;
                    if(inReset)
                    {
                        Reset();
                    }
                    else
                    {
                        EnableModem();
                    }
                    break;
                default:
                    this.Log(LogLevel.Error, "Got GPIO state {0} for unknown input {1}", value, number);
                    break;
            }
        }

        public IReadOnlyDictionary<int, IGPIO> Connections { get; }

        public string IccidNumber { get; set; } = "00000000000000000000";
        public string TrackingAreaCode { get; set; } = "0000";
        public string NetworkLocationArea { get; set; } = "00000";
        public string CellId { get; set; } = "0000";
        public int CellPhysicalId { get; set; } = 0;
        public int CellEarfcn { get; set; } = 0;
        public int CellEarfcnOffset { get; set; } = 0;
        public string ActiveTime { get; set; } = "00100100";
        public string PeriodicTau { get; set; } = "01000111";
        public string NetworkIp { get; set; } = "0.0.0.0";
        public int BitErrorRate { get; set; } = 0;
        public int Rsrp { get; set; } = 0;
        public decimal Rsrq { get; set; } = 0m;
        public int Rssi { get; set; } = 0;
        public int Sinr { get; set; } = 0;
        public int Rscp { get; set; } = 0;
        public decimal Ecno { get; set; } = 0m;
        public int Band { get; set; } = 0;
        public int EnhancedCoverageLevel { get; set; } = 0;
        public int TransmitPower { get; set; } = 0;
        public NetworkRegistrationStates NetworkRegistrationState { get; set; } = NetworkRegistrationStates.NotRegisteredNotSearching;

        public int SignalStrength => (int?)Misc.RemapNumber(Rssi, -113m, -51m, 0, 31) ?? 0;

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

        private void EnableModem()
        {
            Enabled = true;
            // Notify the DTE that the modem is ready
            SendString(ModemReady);
            vddExt.Set();
        }

        // CSQ - Signal Quality Report
        [AtCommand("AT+CSQ")]
        private Response Csq() => Ok.WithParameters($"+CSQ: {SignalStrength},{BitErrorRate}");

        // CGMI - Request Manufacturer Identification
        [AtCommand("AT+CGMI")]
        private Response Cgmi() => Ok.WithParameters(Vendor, ModelName, Revision);

        // CGMM - Request Model Identification
        [AtCommand("AT+CGMM")]
        private Response Cgmm() => Ok.WithParameters(ModelName);

        // CGMR - Request Manufacturer Revision
        [AtCommand("AT+CGMR")]
        private Response Cgmr() => Ok.WithParameters($"Revision: {ManufacturerRevision}");

        // CGSN - Request Product Serial Number
        [AtCommand("AT+CGSN")]
        private Response Cgsn() => CgsnWrite();

        [AtCommand("AT+CGSN", CommandType.Write)]
        private Response CgsnWrite(SerialNumberType serialNumberType = SerialNumberType.Device)
        {
            string result;
            switch(serialNumberType)
            {
                case SerialNumberType.Device:
                    result = serialNumber;
                    break;
                case SerialNumberType.Imei:
                    result = imeiNumber;
                    break;
                case SerialNumberType.ImeiSv:
                    result = imeiNumber.Substring(0, imeiNumber.Length - 1) + softwareVersionNumber;
                    break;
                case SerialNumberType.SoftwareVersionNumber:
                    result = softwareVersionNumber;
                    break;
                default:
                    return Error; // unreachable
            }
            return Ok.WithParameters($"+CGSN: {result}");
        }

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
                    SendString(DataModePrompt);
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
        private bool inReset;
        private bool echoInDataMode;

        private readonly string imeiNumber;
        private readonly string softwareVersionNumber;
        private readonly string serialNumber;
        private readonly IGPIO vddExt = new GPIO();
        private const string Vendor = "Quectel_Ltd";
        private const string ModelName = "Quectel_BC660K-GL";
        private const string Revision = "Revision: QCX212";
        private const string ManufacturerRevision = "BC660KGLAAR01A0";
        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
        private const string DataModePrompt = ">";
        private const string SendOk = "SEND OK";
        private const string ModemReady = "RDY";

        public enum NetworkRegistrationStates
        {
            NotRegisteredNotSearching,
            RegisteredHomeNetwork,
            NotRegisteredSearching,
            RegistrationDenied,
            Unknown,
            RegisteredRoaming,
        }

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
            Reset,
        }

        private enum SerialNumberType
        {
            Device,
            Imei,
            ImeiSv,
            SoftwareVersionNumber,
        }
    }
}
