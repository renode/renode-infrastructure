//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
            for(int i = 0; i < sockets.Length; i++)
            {
                sockets[i] = null;
            }
            inReset = false;
            echoInDataMode = false;
            networkRegistrationUrcType = NetworkRegistrationUrcType.Disabled;
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
        public ulong DeepsleepOnRellock { get; set; } = 0;

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

        private void EnterDeepsleep()
        {
            this.Log(LogLevel.Debug, "Entering deep sleep mode");
            if(deepSleepEventEnabled)
            {
                SendString("+QNBIOTEVENT: \"ENTER DEEPSLEEP\"");
            }
            // Entering deep sleep is equivalent to a power off, so we do a reset here.
            // NVRAM values will be preserved.
            Reset();
        }

        private void EnableModem()
        {
            Enabled = true;
            // Notify the DTE that the modem is ready
            SendString(ModemReady);
            vddExt.Set();
        }

        private void BytesReceived(int connectionId, int byteCount)
        {
            // We do both of the sends here after a delay to accomodate software
            // which might not expect the "instant" reply which would otherwise happen.
            // Notify that the signaling connection is active
            ExecuteWithDelay(() => SendString("+CSCON: 1"), 1000);
            // Send the data received notification
            ExecuteWithDelay(() =>
            {
                var recvUrc = $"+QIURC: \"recv\",{connectionId}";
                if(showLength)
                {
                    recvUrc += $",{byteCount}";
                }
                SendString(recvUrc);
            }, 1500);
        }

        private bool IsValidConnectionId(int connectionId, [CallerMemberName] string caller = "")
        {
            if(connectionId < 0 || connectionId >= sockets.Length)
            {
                this.Log(LogLevel.Warning, "Connection ID {0} is invalid in {1}", connectionId, caller);
                return false;
            }
            return true;
        }

        // ATI - Display Product Identification Information
        [AtCommand("ATI")]
        private Response Ati() => Ok.WithParameters(Vendor, ModelName, Revision);

        // AT&W - Save Current Parameters to NVRAM
        [AtCommand("AT&W")]
        private Response Atw()
        {
            var d = 200UL;
            ExecuteWithDelay(() => SendString("+CSCON: 1"), d += 2000); // CSCON: 1 -> signaling connection active
            ExecuteWithDelay(() => SendString(CeregContent(true)), d += 100);
            ExecuteWithDelay(() => SendString($"+IP: {NetworkIp}"), d += 1000); // IP URC means successfully registered
            return Ok; // stub
        }

        // CEDRXS - eDRX Setting
        [AtCommand("AT+CEDRXS", CommandType.Write)]
        private Response Cedrxs(int mode = 1, int accessTechnology = 5, string requestedEdrxValue = "0010")
        {
            return Ok; // stub
        }

        private string CeregContent(bool urc = false)
        {
            var fragments = new List<string>();
            // The URC form of CEREG does not report the type, the command response form does.
            if(!urc)
            {
                fragments.Add(((int)networkRegistrationUrcType).ToString());
            }

            if(networkRegistrationUrcType >= NetworkRegistrationUrcType.StatOnly)
            {
                fragments.Add(((int)NetworkRegistrationState).ToString());
            }
            if(networkRegistrationUrcType >= NetworkRegistrationUrcType.StatLocation)
            {
                fragments.Add(TrackingAreaCode.SurroundWith("\""));
                fragments.Add(CellId.PadLeft(8, '0').SurroundWith("\""));
                fragments.Add("9"); // access technology: E-UTRAN (NB-S1 mode)
            }
            if(networkRegistrationUrcType >= NetworkRegistrationUrcType.StatLocationEmmCause)
            {
                if(networkRegistrationUrcType == NetworkRegistrationUrcType.StatLocationPsm)
                {
                    fragments.Add("");
                    fragments.Add("");
                }
                else
                {
                    fragments.Add("0"); // reject cause type
                    fragments.Add("0"); // reject cause
                }
            }
            if(networkRegistrationUrcType == NetworkRegistrationUrcType.StatLocationPsm ||
                networkRegistrationUrcType == NetworkRegistrationUrcType.StatLocationEmmCausePsm)
            {
                fragments.Add(ActiveTime.SurroundWith("\""));
                fragments.Add(PeriodicTau.SurroundWith("\""));
            }

            return "+CEREG: " + string.Join(",", fragments);
        }

        // CEREG - EPS Network Registration Status
        [AtCommand("AT+CEREG", CommandType.Write)]
        private Response CeregWrite(NetworkRegistrationUrcType type)
        {
            networkRegistrationUrcType = type;
            // Queue a CEREG URC in response to the write
            ExecuteWithDelay(() => SendString(CeregContent(true)), 1000);
            return Ok; // stub, should disable or enable network registration URC
        }

        [AtCommand("AT+CEREG", CommandType.Read)]
        private Response CeregRead() => Ok.WithParameters(CeregContent());

        // CESQ - Extended Signal Quality
        [AtCommand("AT+CESQ")]
        private Response Cesq()
        {
            var rscp = (int?)Misc.RemapNumber(Rscp, -120m, -25m, 0, 96) ?? 255;
            var ecno = (int?)Misc.RemapNumber(Ecno, -24m, 0m, 0, 49) ?? 255;
            var rsrq = (int?)Misc.RemapNumber(Rsrq, -19.5m, -3m, 0, 34) ?? 255;
            var rsrp = (int?)Misc.RemapNumber(Rsrp, -140m, -44m, 0, 97) ?? 255;
            return Ok.WithParameters($"+CESQ: {SignalStrength},{BitErrorRate},{rscp},{ecno},{rsrq},{rsrp}");
        }

        // CFUN - Set UE Functionality
        [AtCommand("AT+CFUN", CommandType.Write)]
        private Response Cfun(FunctionalityLevel functionalityLevel = FunctionalityLevel.Full, int reset = 0)
        {
            return Ok; // stub
        }

        // CGDCONT - Define PDP Context
        [AtCommand("AT+CGDCONT", CommandType.Read)]
        private Response Cgdcont() => Ok.WithParameters($"+CGDCONT: 1,\"IP\",\"{pdpContextApn}\",\"{NetworkIp}\",0,0,0,,,,0,,0,,0,0"); // stub

        // CPIN - Enter PIN
        [AtCommand("AT+CPIN", CommandType.Read)]
        private Response Cpin()
        {
            return Ok.WithParameters("+CPIN: READY"); // stub
        }

        // CPSMS - Power Saving Mode Setting
        [AtCommand("AT+CPSMS", CommandType.Write)]
        private Response CpsmsWrite(int mode = 1, int reserved1 = 0, int reserved2 = 0,
            string requestedPeriodicTau = "", string requestedActiveTime = "")
        {
            return Ok; // stub
        }

        // CSCON - Signaling Connection Status
        [AtCommand("AT+CSCON", CommandType.Write)]
        private Response Cscon(int enable = 0)
        {
            return Ok; // stub, enables +CSCON: <mode> URC
        }

        // CSQ - Signal Quality Report
        [AtCommand("AT+CSQ")]
        private Response Csq() => Ok.WithParameters($"+CSQ: {SignalStrength},{BitErrorRate}");

        // CGACT - PDP Context Activate/Deactivate
        [AtCommand("AT+CGACT", CommandType.Read)]
        private Response Cgact() => Ok.WithParameters("+CGACT: 1,1"); // stub

        // CGMI - Request Manufacturer Identification
        [AtCommand("AT+CGMI")]
        private Response Cgmi() => Ok.WithParameters(Vendor, ModelName, Revision);

        // CGMM - Request Model Identification
        [AtCommand("AT+CGMM")]
        private Response Cgmm() => Ok.WithParameters(ModelName);

        // CGMR - Request Manufacturer Revision
        [AtCommand("AT+CGMR")]
        private Response Cgmr() => Ok.WithParameters($"Revision: {ManufacturerRevision}");

        // CGPADDR - Show PDP Addresses
        [AtCommand("AT+CGPADDR", CommandType.Read)]
        private Response Cgpaddr() => Ok.WithParameters($"+CGPADDR: 1,{NetworkIp}"); // stub

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

        // COPS - Operator Selection
        [AtCommand("AT+COPS", CommandType.Write)]
        private Response CopsWrite(int mode = 0, int operFormat = 0, string oper = "", int accessTechnology = 9)
        {
            return Ok; // stub
        }

        [AtCommand("AT+COPS", CommandType.Read)]
        private Response CopsRead() => Ok.WithParameters($"+COPS: 0,2,\"{NetworkLocationArea}\",9"); // stub

        // IPR - Set TE-TA Local Rate
        [AtCommand("AT+IPR", CommandType.Write)]
        private Response IprWrite(uint rate = 115200)
        {
            BaudRate = rate;
            return Ok;
        }

        [AtCommand("AT+IPR", CommandType.Read)]
        private Response IprRead() => Ok.WithParameters($"+IPR: {BaudRate}");

        // QBAND - Get and Set Mobile Operation Band
        [AtCommand("AT+QBAND", CommandType.Write)]
        private Response Qband(int numberOfBands, params int[] bands)
        {
            if(bands.Length != numberOfBands)
            {
                return Error;
            }
            return Ok; // stub
        }

        // QCCID - USIM Card Identification
        [AtCommand("AT+QCCID")]
        private Response Qccid() => Ok.WithParameters($"+QCCID: {IccidNumber}");

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        private Response Qcfg(string function, int value)
        {
            switch(function)
            {
                case "dsevent":
                    deepSleepEventEnabled = value != 0;
                    break;
                case "initlocktime": // initial Sleep Lock duration
                case "atlocktime":  // Sleep Lock duration after AT commands
                default:
                    this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, unsupported", function, value);
                    break;
            }
            return Ok;
        }

        // QCGDEFCONT - Set Default PSD Connection Settings
        [AtCommand("AT+QCGDEFCONT", CommandType.Write)]
        private Response Qcgdefcont(PdpType pdpType, string apn = "", string username = "",
            string password = "", int authenticationType = 0)
        {
            pdpContextApn = apn;
            return Ok; // stub
        }

        // QEMMTIMER - Enable/Disable URC Reporting for EMM Timer
        [AtCommand("AT+QEMMTIMER", CommandType.Write)]
        private Response Qemmtimer(int enable = 0)
        {
            return Ok; // stub
        }

        // QENG - Engineering Mode
        [AtCommand("AT+QENG", CommandType.Write)]
        private Response Qeng(int mode)
        {
            return Ok.WithParameters($"+QENG: 0,{CellEarfcn},{CellEarfcnOffset},{CellPhysicalId},\"{CellId}\",{Rsrp},{(int)Rsrq},{Rssi},{Sinr},{Band},\"{TrackingAreaCode}\",{EnhancedCoverageLevel},{TransmitPower},2");
        }

        // QGMR - Request Modem and Application Firmware Versions
        [AtCommand("AT+QGMR")]
        private Response Qgmr() => Ok.WithParameters($"{ManufacturerRevision}_{SoftwareRevision}");

        // QICFG - Configure Optional TCP/IP Parameters
        [AtCommand("AT+QICFG", CommandType.Write)]
        private Response Qicfg(string parameter, params int[] args)
        {
            if(args.Length < 1)
            {
                return Error;
            }

            switch(parameter)
            {
                case "echomode":
                    echoInDataMode = args[0] != 0;
                    break;
                case "dataformat":
                    if(args.Length < 2)
                    {
                        return Error;
                    }
                    // sendDataFormat only applies to sending in non-data mode, which is not
                    // currently implemented
                    sendDataFormat = args[0] != 0 ? DataFormat.Hex : DataFormat.Text;
                    receiveDataFormat = args[1] != 0 ? DataFormat.Hex : DataFormat.Text;
                    break;
                case "showlength":
                    showLength = args[0] != 0;
                    break;
                case "viewmode":
                    dataOutputSeparator = args[0] != 0 ? "," : CrLf;
                    break;
                default:
                    this.Log(LogLevel.Warning, "TCP/IP config value '{0}' set to {1}, unsupported", parameter, args.Stringify());
                    break;
            }
            return Ok;
        }

        // QNBIOTEVENT - Enable/Disable NB-IoT Related Event Report
        [AtCommand("AT+QNBIOTEVENT", CommandType.Write)]
        private Response Qnbiotevent(int enable = 0, int eventType = 1)
        {
            return Ok; // stub
        }

        // QNBIOTRAI - NB-IoT Release Assistance Indication
        [AtCommand("AT+QNBIOTRAI", CommandType.Write)]
        private Response Qnbiotrai(int enable = 0, int eventType = 1)
        {
            this.Log(LogLevel.Debug, "NB-IoT Release Assistance Indication set to {0}", enable);
            return Ok; // stub
        }

        // QPOWD - Power Off
        [AtCommand("AT+QPOWD", CommandType.Write)]
        private Response QpowdWrite(PowerOffType type = PowerOffType.Normal)
        {
            Reset();
            ExecuteWithDelay(EnableModem);
            return Ok;
        }

        // QRELLOCK - Release Sleep Lock of AT Commands
        [AtCommand("AT+QRELLOCK")]
        private Response Qrellock()
        {
            // This command is meant to release the 10-second sleep lock timer after each
            // AT command, not necessarily make the modem enter sleep mode immediately.
            // We use it as a signal that the software talking to the modem expects it to
            // enter sleep mode, so we enter sleep mode for a while and then leave it again
            // if this was requested with the DeepsleepOnRellock property.
            if(DeepsleepOnRellock != 0)
            {
                ExecuteWithDelay(EnterDeepsleep, 50);
                ExecuteWithDelay(EnableModem, 50 + DeepsleepOnRellock);
            }
            return Ok;
        }

        // QRST - Module Reset
        [AtCommand("AT+QRST", CommandType.Write)]
        private Response QrstWrite(int mode = 1)
        {
            return Ok; // stub
        }

        // QSCLK - Configure Sleep Mode
        [AtCommand("AT+QSCLK", CommandType.Write)]
        private Response QsclkWrite(int mode = 1)
        {
            return Ok; // stub
        }

        // QIOPEN - Open a Socket Service
        [AtCommand("AT+QIOPEN", CommandType.Write)]
        private Response Qiopen(int contextId, int connectionId, ServiceType serviceType, string host, ushort remotePort, ushort localPort = 0, int accessMode = 1)
        {
            if(!IsValidConnectionId(connectionId) || sockets[connectionId] != null)
            {
                return Error;
            }

            // The BC660K only supports contextId 0, the BC66 and BG9X only support contextId 1.
            // We allow both for now.
            if(contextId != 0 && contextId != 1)
            {
                return Error;
            }

            this.Log(LogLevel.Debug, "Context {0} connectionId {1} requested {2} connection open to {3}:{4}",
                contextId, connectionId, serviceType, host, remotePort);

            // We can't just send Ok.WithTrailer because the driver won't see the URC.
            ExecuteWithDelay(() =>
            {
                var service = SocketService.Open(this, connectionId, serviceType, host, remotePort);
                if(service == null)
                {
                    this.Log(LogLevel.Warning, "Failed to open connection {0}", connectionId);
                }
                else
                {
                    this.Log(LogLevel.Debug, "Connection {0} opened successfully", connectionId);
                }
                sockets[connectionId] = service;
                SendString($"+QIOPEN: {connectionId},{(service == null ? 1 : 0)}");
            });
            return Ok;
        }

        [AtCommand("AT+QICLOSE", CommandType.Write)]
        private Response Qiclose(int connectionId)
        {
            if(!IsValidConnectionId(connectionId))
            {
                return Error;
            }

            // AT+QICLOSE succeeds even if the socket was already closed.
            sockets[connectionId]?.Dispose();
            sockets[connectionId] = null;
            return Ok.WithTrailer("CLOSE OK");
        }

        // QIRD - Retrieve the Received TCP/IP Data
        [AtCommand("AT+QIRD", CommandType.Write)]
        private Response Qird(int connectionId, int readLength)
        {
            if(!IsValidConnectionId(connectionId) || sockets[connectionId] == null)
            {
                return Error;
            }

            var readBytes = sockets[connectionId].Receive(readLength);
            var qirdResponseHeader = $"+QIRD: {readBytes.Length}";
            if(showLength)
            {
                qirdResponseHeader += $",{sockets[connectionId].BytesAvailable}";
            }
            qirdResponseHeader += dataOutputSeparator;

            switch(receiveDataFormat)
            {
                case DataFormat.Hex:
                    var hexBytes = BitConverter.ToString(readBytes).Replace("-", "");
                    return Ok.WithParameters(qirdResponseHeader + hexBytes);
                case DataFormat.Text:
                    return Ok.WithParameters(StringEncoding.GetBytes(qirdResponseHeader).Concat(readBytes).ToArray());
                default:
                    throw new InvalidOperationException($"Invalid {nameof(receiveDataFormat)}");
            }
        }

        // QISEND - Send Hex/Text String Data
        [AtCommand("AT+QISEND", CommandType.Write)]
        private Response Qisend(int connectionId, int? sendLength = null, string data = null)
        {
            if(!IsValidConnectionId(connectionId) || sockets[connectionId] == null)
            {
                return Error;
            }

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
                return Ok.WithTrailer(SendOk);
            }
            else // Send data (fixed or variable-length) in data mode
            {
                // We need to wait a while before sending the data mode prompt.
                ExecuteWithDelay(() =>
                {
                    SendString(DataModePrompt);
                    EnterDataMode(sendLength, bytes =>
                    {
                        this.Log(LogLevel.Debug, "ConnectionId {0} requested send of '{1}' in data mode",
                            connectionId, BitConverter.ToString(bytes));

                        string sendResponse;
                        if(sockets[connectionId].Send(bytes))
                        {
                            sendResponse = SendOk;
                        }
                        else
                        {
                            sendResponse = SendFailed;
                            this.Log(LogLevel.Warning, "Failed to send data to connection {0}", connectionId);
                        }
                        // We can send the OK (the return value of this command) immediately,
                        // but we have to wait before SEND OK/SEND FAIL if the network is too fast
                        ExecuteWithDelay(() => SendString(sendResponse));
                    });
                });
                return null;
            }
        }

        [AtCommand("AT+QISTATE", CommandType.Read)]
        private Response QistateRead() => Ok.WithParameters(
            sockets.Where(s => s != null).Select(s => s.Qistate).ToArray());

        // When this is set to Numeric or Verbose, MT-related errors are reported with "+CME ERROR: "
        // instead of the plain "ERROR". This does not apply to syntax errors, invalid parameter
        // errors or Terminal Adapter functionality.
        private MobileTerminationResultCodeMode mtResultCodeMode;
        private MemoryStream dataBuffer;
        private int? dataBytesRemaining;
        private Action<byte[]> dataCallback;
        private bool inReset;
        private bool echoInDataMode;
        private NetworkRegistrationUrcType networkRegistrationUrcType;

        // These fields are not affected by resets because they are automatically saved to NVRAM.
        private string pdpContextApn = "";
        private bool showLength = false;
        private DataFormat sendDataFormat = DataFormat.Text;
        private DataFormat receiveDataFormat = DataFormat.Text;
        private string dataOutputSeparator = CrLf;
        private bool deepSleepEventEnabled = false;

        private readonly string imeiNumber;
        private readonly string softwareVersionNumber;
        private readonly string serialNumber;
        private readonly IGPIO vddExt = new GPIO();
        private readonly SocketService[] sockets = new SocketService[NumberOfConnections];
        private const string Vendor = "Quectel_Ltd";
        private const string ModelName = "Quectel_BC660K-GL";
        private const string Revision = "Revision: QCX212";
        private const string ManufacturerRevision = "BC660KGLAAR01A0";
        private const string SoftwareRevision = "01.002.01.002";
        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
        private const string DataModePrompt = ">";
        private const string SendOk = "SEND OK";
        private const string SendFailed = "SEND FAIL";
        private const string ModemReady = "RDY";
        private const int NumberOfConnections = 4;

        public enum NetworkRegistrationStates
        {
            NotRegisteredNotSearching,
            RegisteredHomeNetwork,
            NotRegisteredSearching,
            RegistrationDenied,
            Unknown,
            RegisteredRoaming,
        }

        // One SocketService corresponds to one connectionId
        private class SocketService : IDisposable
        {
            public static SocketService Open(Quectel_BC660K owner, int connectionId, ServiceType type, string remoteHost, ushort remotePort)
            {
                var emulatedServices = EmulationManager.Instance.CurrentEmulation.ExternalsManager.GetExternalsOfType<IEmulatedNetworkService>();
                var service = emulatedServices.FirstOrDefault(s => s.Host == remoteHost && s.Port == remotePort);
                if(service == null)
                {
                    owner.Log(LogLevel.Warning, "No external service found for {0}:{1}", remoteHost, remotePort);
                    return null;
                }

                return new SocketService(owner, connectionId, type, service, remoteHost, remotePort);
            }

            public bool Send(byte[] data) => connectedService.Send(data);

            public byte[] Receive(int bytes) => connectedService.Receive(bytes);

            public void Dispose()
            {
                connectedService.BytesReceived -= BytesReceived;
                connectedService.Dispose();
                Owner.SendString($"+QIURC: \"closed\",{ConnectionId}");
            }

            public int BytesAvailable => connectedService.BytesAvailable;

            public string Qistate => $"+QISTATE: {ConnectionId},\"{Type.ToString().ToUpper()}\",\"{RemoteHost}\",{RemotePort}";

            public ServiceType Type { get; }
            public Quectel_BC660K Owner { get; }
            public int ConnectionId { get; }
            public string RemoteHost { get; }
            public ushort RemotePort { get; }

            private void BytesReceived(int byteCount) => Owner.BytesReceived(ConnectionId, byteCount);

            private readonly IEmulatedNetworkService connectedService;

            private SocketService(Quectel_BC660K owner, int connectionId, ServiceType type, IEmulatedNetworkService conn, string remoteHost, ushort remotePort)
            {
                Owner = owner;
                ConnectionId = connectionId;
                Type = type;
                RemoteHost = remoteHost;
                RemotePort = remotePort;
                connectedService = conn;
                connectedService.BytesReceived += BytesReceived;
            }
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

        private enum PowerOffType
        {
            Immediate,
            Normal,
        }

        private enum SerialNumberType
        {
            Device,
            Imei,
            ImeiSv,
            SoftwareVersionNumber,
        }

        private enum FunctionalityLevel
        {
            Minimum,
            Full,
            RfTransmitReceiveDisabled = 4,
        }

        private enum NetworkRegistrationUrcType
        {
            Disabled,
            StatOnly,
            StatLocation,
            StatLocationEmmCause,
            StatLocationPsm,
            StatLocationEmmCausePsm,
        }

        private enum DataFormat
        {
            Text,
            Hex,
        }

        private enum PdpType
        {
            Ip,
            IpV6,
            IpV4V6,
            NonIp,
        }
    }
}
