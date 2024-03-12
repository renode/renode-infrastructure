//
// Copyright (c) 2010-2024 Antmicro
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
using Antmicro.Renode.Peripherals.Timers;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public abstract class QuectelModem : AtCommandModem, IGPIOReceiver, INumberedGPIOOutput
    {
        public QuectelModem(IMachine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine)
        {
            this.imeiNumber = imeiNumber;
            this.softwareVersionNumber = softwareVersionNumber;
            this.serialNumber = serialNumber;
            deepsleepTimer = new LimitTimer(machine.ClockSource, 1, this, "T3324", direction: Direction.Ascending, workMode: WorkMode.OneShot, eventEnabled: true);
            deepsleepTimer.LimitReached += () =>
            {
                this.Log(LogLevel.Noisy, "T3324 timer timeout");
                SendSignalingConnectionStatus(false);
                EnterDeepsleep();
            };
            Connections = new Dictionary<int, IGPIO>
            {
                {0, vddExt},
                {1, netLight},
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
                sockets[i]?.Dispose();
                sockets[i] = null;
            }
            inReset = false;
            signalingConnectionActive = false;
            powerSavingModeActive = false;
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
                case GPIOInput.PsmEint:
                    // If we are sleeping (and not held in reset) we should wake up on a falling edge.
                    if(!Enabled && !inReset && !value)
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
        public PublicLandMobileNetworkSearchingState ModemPLMNState { get; set; } = PublicLandMobileNetworkSearchingState.Selected;
        public int PLMNSearchTime { get; set; } = 0;
        public bool DeepsleepOnRellock { get; set; } = false;
        // The delay before sending the +CSCON URC in virtual milliseconds
        public ulong CsconDelay { get; set; } = 500;
        // The delay before sending the +CEREG URC in virtual milliseconds
        public ulong CeregDelay { get; set; } = 500;
        // The delay before sending the +IP URC in virtual milliseconds
        public ulong IpDelay { get; set; } = 1000;
        // These timers should in theory be automatically updated when we enter deep sleep,
        // receive or transmit something. This is a basic implementation that only supports
        // setting them manually.
        public decimal SleepDuration { get; set; } = 0m;
        public decimal RxTime { get; set; } = 0m;
        public decimal TxTime { get; set; } = 0m;
        // These parameters should be set according to expectations of network behavior.
        public ulong ClosePortInactivityDisconnectDelay { get; set; }
        public ulong SendDataInactivityDisconnectDelay { get; set; }
        public ulong SendDataActivityConnectDelay { get; set; } = 550;
        public bool SendCSCONOnChangeOnly { get; set; } = true;

        public int SignalStrength => (int?)Misc.RemapNumber(Rssi, -113m, -51m, 0, 31) ?? 0;
        public int ActiveTimeSeconds => ConvertEncodedStringToSeconds(ActiveTime, ModemTimerType.ActiveTimeT3324);
        public int PeriodicTauSeconds => ConvertEncodedStringToSeconds(PeriodicTau, ModemTimerType.PeriodicTauT3412);

        protected override Response HandleCommand(string command)
        {
            if(deepsleepTimer.Enabled)
            {
                // Restart deep sleep timer after receiving any AT command.
                deepsleepTimer.Value = 0;
                this.Log(LogLevel.Noisy, "'{0}' command received: Defer deepsleep by {1} seconds", command, ActiveTimeSeconds);
            }
            return base.HandleCommand(command);
        }

        // ATI - Display Product Identification Information
        [AtCommand("ATI")]
        protected virtual Response Ati() => Ok.WithParameters(Vendor, ModelName, Revision);

        // AT&W - Save Current Parameters to NVRAM
        [AtCommand("AT&W")]
        protected override Response Atw()
        {
            var d = 0UL;
            ExecuteWithDelay(() => SendSignalingConnectionStatus(true), d += CsconDelay);
            ExecuteWithDelay(() => SendString($"+IP: {NetworkIp}"), d += IpDelay); // IP URC means successfully registered
            return base.Atw();
        }

        // CEDRXS - eDRX Setting
        [AtCommand("AT+CEDRXS", CommandType.Write)]
        protected virtual Response Cedrxs(int mode = 1, int accessTechnology = 5, string requestedEdrxValue = "0010")
        {
            return Ok; // stub
        }

        protected virtual string CeregContent(bool urc = false, string prefix = "+CEREG")
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

            return prefix + ": " + string.Join(",", fragments);
        }

        protected virtual string CregContent(bool urc = false)
        {
            // Signature of +CREG message is the same as of +CEREG.
            return CeregContent(urc, "+CREG");
        }

        // CEREG - EPS Network Registration Status
        [AtCommand("AT+CEREG", CommandType.Write)]
        protected virtual Response CeregWrite(NetworkRegistrationUrcType type)
        {
            networkRegistrationUrcType = type;
            // Queue a CEREG URC in response to the write
            ExecuteWithDelay(() => SendString(CeregContent(true)), CeregDelay);
            return Ok; // stub, should disable or enable network registration URC
        }

        [AtCommand("AT+CEREG", CommandType.Read)]
        protected virtual Response CeregRead() => Ok.WithParameters(CeregContent());

        // CREG - Network Registration
        [AtCommand("AT+CREG", CommandType.Write)]
        protected virtual Response CregWrite(NetworkRegistrationUrcType type)
        {
            networkRegistrationUrcType = type;
            // Queue a CREG URC in response to the write, use the same delay as for CEREG URC
            ExecuteWithDelay(() => SendString(CregContent(true)), CeregDelay);
            return Ok; // stub, should disable or enable network registration URC
        }

        [AtCommand("AT+CREG", CommandType.Read)]
        protected virtual Response CregRead() => Ok.WithParameters(CregContent());

        // CESQ - Extended Signal Quality
        [AtCommand("AT+CESQ")]
        protected virtual Response Cesq()
        {
            var rscp = (int?)Misc.RemapNumber(Rscp, -120m, -25m, 0, 96) ?? 255;
            var ecno = (int?)Misc.RemapNumber(Ecno, -24m, 0m, 0, 49) ?? 255;
            var rsrq = (int?)Misc.RemapNumber(Rsrq, -19.5m, -3m, 0, 34) ?? 255;
            var rsrp = (int?)Misc.RemapNumber(Rsrp, -140m, -44m, 0, 97) ?? 255;
            return Ok.WithParameters($"+CESQ: {SignalStrength},{BitErrorRate},{rscp},{ecno},{rsrq},{rsrp}");
        }

        // CFUN - Set UE Functionality
        [AtCommand("AT+CFUN", CommandType.Write)]
        protected virtual Response Cfun(FunctionalityLevel functionalityLevel = FunctionalityLevel.Full, int reset = 0)
        {
            // Reset option isn't taken into account yet, so the new functionality level
            // is always activated immediately and remains valid after deep sleep wakeup.
            this.functionalityLevel = functionalityLevel;

            if(signalingConnectionStatusReportingEnabled && functionalityLevel == FunctionalityLevel.Full)
            {
                var d = 0UL;
                // We do both of the sends here after a delay to accomodate software
                // which might not expect the "instant" reply which would otherwise happen.
                ExecuteWithDelay(() => SendSignalingConnectionStatus(true), d += CsconDelay);
                ExecuteWithDelay(() => SendString($"+IP: {NetworkIp}"), d += IpDelay); // IP URC means successfully registered
            }

            // Notify the DTE about the registration status to emulate the behavior
            // of a real modem where it might change in response to the functionality
            // level being changed.
            ExecuteWithDelay(() => SendString(CeregContent(true)), CeregDelay);
            return Ok; // stub
        }

        [AtCommand("AT+CFUN", CommandType.Read)]
        protected virtual Response Cfun() => Ok.WithParameters($"+CFUN: {(int)functionalityLevel}");

        // CGDCONT - Define PDP Context
        [AtCommand("AT+CGDCONT", CommandType.Read)]
        protected virtual Response Cgdcont() => Ok.WithParameters($"+CGDCONT: 1,\"IP\",\"{pdpContextApn}\",\"{NetworkIp}\",0,0,0,,,,0,,0,,0,0"); // stub

        // CPIN - Enter PIN
        [AtCommand("AT+CPIN", CommandType.Read)]
        protected virtual Response Cpin()
        {
            return Ok.WithParameters("+CPIN: READY"); // stub
        }

        // CPSMS - Power Saving Mode Setting
        [AtCommand("AT+CPSMS", CommandType.Write)]
        protected virtual Response CpsmsWrite(int mode = 1, int reserved1 = 0, int reserved2 = 0,
            string requestedPeriodicTau = "", string requestedActiveTime = "")
        {
            return Ok; // stub
        }

        protected virtual string CsconContent(bool urc = false)
        {
            var reportingEnabled = signalingConnectionStatusReportingEnabled ? 1 : 0;
            var active = signalingConnectionActive ? 1 : 0;

            if(urc)
            {
                return $"+CSCON: {active}";
            }
            return $"+CSCON: {reportingEnabled},{active}";
        }

        // CSCON - Signaling Connection Status
        [AtCommand("AT+CSCON", CommandType.Read)]
        protected virtual Response Cscon()
        {
            return Ok.WithParameters(CsconContent());
        }

        // CSCON - Signaling Connection Status
        [AtCommand("AT+CSCON", CommandType.Write)]
        protected virtual Response Cscon(int enable = 0)
        {
            signalingConnectionStatusReportingEnabled = enable == 1;
            return Ok;
        }

        // CSQ - Signal Quality Report
        [AtCommand("AT+CSQ")]
        protected virtual Response Csq() => Ok.WithParameters($"+CSQ: {SignalStrength},{BitErrorRate}");

        // CGACT - PDP Context Activate/Deactivate
        [AtCommand("AT+CGACT", CommandType.Read)]
        protected virtual Response Cgact() => Ok.WithParameters("+CGACT: 1,1"); // stub

        // CGMI - Request Manufacturer Identification
        [AtCommand("AT+CGMI")]
        protected virtual Response Cgmi() => Ok.WithParameters(Vendor, ModelName, Revision);

        // CGMM - Request Model Identification
        [AtCommand("AT+CGMM")]
        protected virtual Response Cgmm() => Ok.WithParameters(ModelName);

        // CGMR - Request Manufacturer Revision
        [AtCommand("AT+CGMR")]
        protected virtual Response Cgmr() => Ok.WithParameters($"Revision: {ManufacturerRevision}");

        // CGPADDR - Show PDP Addresses
        [AtCommand("AT+CGPADDR", CommandType.Read)]
        protected virtual Response Cgpaddr() => Ok.WithParameters($"+CGPADDR: 1,{NetworkIp}"); // stub

        // CGSN - Request Product Serial Number
        [AtCommand("AT+CGSN")]
        protected virtual Response Cgsn() => CgsnWrite();

        [AtCommand("AT+CGSN", CommandType.Write)]
        protected virtual Response CgsnWrite(SerialNumberType serialNumberType = SerialNumberType.Device)
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
        protected virtual Response Cmee(MobileTerminationResultCodeMode mode = MobileTerminationResultCodeMode.Disabled)
        {
            mtResultCodeMode = mode;
            this.Log(LogLevel.Debug, "CMEE result mode set to {0}", mode);
            return Ok;
        }

        [AtCommand("AT+CMEE", CommandType.Read)]
        protected virtual Response CmeeRead() => Ok.WithParameters($"+CMEE: {((int)mtResultCodeMode)}");

        // COPS - Operator Selection
        [AtCommand("AT+COPS", CommandType.Write)]
        protected virtual Response CopsWrite(int mode = 0, int operFormat = 0, string oper = "", int accessTechnology = 9)
        {
            return Ok; // stub
        }

        [AtCommand("AT+COPS", CommandType.Read)]
        protected virtual Response CopsRead() => Ok.WithParameters($"+COPS: 0,2,\"{NetworkLocationArea}\",9"); // stub

        // IPR - Set TE-TA Local Rate
        [AtCommand("AT+IPR", CommandType.Write)]
        protected virtual Response IprWrite(uint rate = 115200)
        {
            BaudRate = rate;
            return Ok;
        }

        [AtCommand("AT+IPR", CommandType.Read)]
        protected virtual Response IprRead() => Ok.WithParameters($"+IPR: {BaudRate}");

        // QBAND - Get and Set Mobile Operation Band
        [AtCommand("AT+QBAND", CommandType.Write)]
        protected virtual Response Qband(int numberOfBands, params int[] bands)
        {
            if(bands.Length != numberOfBands)
            {
                return Error;
            }
            return Ok; // stub
        }

        // QCCID - USIM Card Identification
        [AtCommand("AT+QCCID")]
        protected virtual Response Qccid() => Ok.WithParameters($"+QCCID: {IccidNumber}");

        // CCLK - Set and Get Current Date and Time
        [AtCommand("AT+CCLK", CommandType.Write)]
        protected virtual Response CclkWrite(string dateTime)
        {
            this.Log(LogLevel.Warning, "Ignoring attempt to set date/time to '{0}'", dateTime);
            return Ok; // stub
        }

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        protected virtual Response Qcfg(string function, params int[] args)
        {
            this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, not supported by this modem", function, string.Join(", ", args));
            return Error;
        }

        // QENG - Engineering Mode
        [AtCommand("AT+QENG", CommandType.Write)]
        protected virtual Response Qeng(int mode)
        {
            // Only modes 0 and 2 are implemented.
            switch(mode)
            {
                case 0:
                    return Ok.WithParameters($"+QENG: 0,{CellEarfcn},{CellEarfcnOffset},{CellPhysicalId},\"{CellId}\",{Rsrp},{(int)Rsrq},{Rssi},{Sinr},{Band},\"{TrackingAreaCode}\",{EnhancedCoverageLevel},{TransmitPower},2");
                case 2:
                    // The 3 here is not a typo, it matches real modem output and the AT command manual
                    return Ok.WithParameters($"+QENG: 3,{SleepDuration * 10:0},{RxTime * 10:0},{TxTime * 10:0}");
                default:
                    return Error;
            }
        }

        // QGMR - Request Modem and Application Firmware Versions
        [AtCommand("AT+QGMR")]
        protected virtual Response Qgmr() => Ok.WithParameters($"{ManufacturerRevision}_{SoftwareRevision}");

        // QICFG - Configure Optional TCP/IP Parameters
        [AtCommand("AT+QICFG", CommandType.Write)]
        protected virtual Response Qicfg(string parameter, params int[] args)
        {
            this.Log(LogLevel.Warning, "TCP/IP config value '{0}' set to {1}, not supported by this modem",
                parameter, args.Stringify());
            return Error;
        }

        // QNBIOTEVENT - Enable/Disable NB-IoT Related Event Report
        [AtCommand("AT+QNBIOTEVENT", CommandType.Write)]
        protected virtual Response Qnbiotevent(int enable = 0, int eventType = 1)
        {
            // Only event type 1 (PSM state) is supported
            if(eventType != 1)
            {
                return Error;
            }
            powerSavingModeEventEnabled = enable != 0;
            return Ok;
        }

        [AtCommand("AT+QNBIOTEVENT", CommandType.Read)]
        protected virtual Response QnbioteventRead()
        {
            return Ok.WithParameters($"+QNBIOTEVENT: {(powerSavingModeEventEnabled ? 1 : 0)},1");
        }

        // QNBIOTRAI - NB-IoT Release Assistance Indication
        [AtCommand("AT+QNBIOTRAI", CommandType.Write)]
        protected virtual Response Qnbiotrai(int raiMode = 0)
        {
            if(raiMode < 0 || raiMode > 2)
            {
                return Error;
            }

            this.Log(LogLevel.Debug, "NB-IoT Release Assistance Indication set to {0}", raiMode);
            return Ok; // stub
        }

        // QPOWD - Power Off
        [AtCommand("AT+QPOWD", CommandType.Write)]
        protected virtual Response QpowdWrite(PowerOffType type = PowerOffType.Normal)
        {
            Reset();
            ExecuteWithDelay(EnableModem);
            return Ok;
        }

        // QRST - Module Reset
        [AtCommand("AT+QRST", CommandType.Write)]
        protected virtual Response QrstWrite(int mode = 1)
        {
            return Ok; // stub
        }

        // QSCLK - Configure Sleep Mode
        [AtCommand("AT+QSCLK", CommandType.Write)]
        protected virtual Response QsclkWrite(int mode = 1)
        {
            switch(mode)
            {
                case 0: // Disable sleep modes.
                {
                    deepsleepTimer.Enabled = false;
                    break;
                }
                case 1: // Enable deep sleep mode.
                {
                    if(ActiveTimeSeconds != 0)
                    {
                        deepsleepTimer.Value = 0;
                        deepsleepTimer.Limit = (ulong)ActiveTimeSeconds;
                        deepsleepTimer.Enabled = true;
                        // Defer entering deep sleep until timer timeouts.
                        this.Log(LogLevel.Noisy, "Defer deepsleep by {0} seconds", ActiveTimeSeconds);
                        break;
                    }
                    // The signaling connection goes inactive when sleep mode is enabled.
                    ExecuteWithDelay(() =>
                    {
                        SendSignalingConnectionStatus(false);

                        // Also, if we are configured to enter deep sleep when the sleep
                        // lock is released, we also use sleep mode being enabled as our
                        // cue to enter it.
                        if(DeepsleepOnRellock)
                        {
                            EnterDeepsleep();
                        }
                    }, CsconDelay);
                    break;
                }
                case 2: // Enable light sleep mode.
                {
                    // Light sleep mode is not implemented.
                    break;
                }
                default:
                {
                    return Error;
                }
            }
            return Ok;
        }

        // QIOPEN - Open a Socket Service
        [AtCommand("AT+QIOPEN", CommandType.Write)]
        protected virtual Response Qiopen(int contextId, int connectionId, ServiceType serviceType, string host, ushort remotePort, ushort localPort = 0, int accessMode = 1)
        {
            if(!IsValidConnectionId(connectionId) || sockets[connectionId] != null)
            {
                return Error;
            }

            if(!IsValidContextId(contextId))
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

                    if(serviceType == ServiceType.Tcp)
                    {
                        ExecuteWithDelay(() => SendSignalingConnectionStatus(true), CsconDelay + 50);
                    }
                }
                sockets[connectionId] = service;
                SendString($"+QIOPEN: {connectionId},{(service == null ? 1 : 0)}");
            });
            return Ok;
        }

        // QICLOSE - Close a Socket Service
        [AtCommand("AT+QICLOSE", CommandType.Write)]
        protected virtual Response Qiclose(int connectionId)
        {
            if(!IsValidConnectionId(connectionId))
            {
                return Error;
            }

            // AT+QICLOSE succeeds even if the socket was already closed.
            sockets[connectionId]?.Dispose();
            sockets[connectionId] = null;
            // If all sockets are closed the signaling connection goes inactive.
            // After the port is closed, modem disconnects after a period of inactivity.
            ExecuteWithDelay(() =>
            {
                if(sockets.All(s => s == null))
                {
                    SendSignalingConnectionStatus(false);
                }
            }, ClosePortInactivityDisconnectDelay);
            return Ok.WithTrailer("CLOSE OK");
        }

        // QIRD - Retrieve the Received TCP/IP Data
        [AtCommand("AT+QIRD", CommandType.Write)]
        protected virtual Response Qird(int connectionId, int readLength)
        {
            if(!IsValidConnectionId(connectionId) || sockets[connectionId] == null)
            {
                return Error;
            }

            // Query the length of buffered received data
            if(readLength == 0)
            {
                return Ok.WithParameters($"+QIRD: {sockets[connectionId].BytesAvailable}");
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
                    return Ok.WithParameters(qirdResponseHeader + hexBytes.SurroundWith(dataOutputSurrounding));
                case DataFormat.Text:
                    var dataOutputSurroundingBytes = StringEncoding.GetBytes(dataOutputSurrounding);
                    return Ok.WithParameters(StringEncoding.GetBytes(qirdResponseHeader)
                                                        .Concat(dataOutputSurroundingBytes)
                                                        .Concat(readBytes)
                                                        .Concat(dataOutputSurroundingBytes)
                                                        .ToArray());
                default:
                    throw new InvalidOperationException($"Invalid {nameof(receiveDataFormat)}");
            }
        }

        // QISEND - Send Hex/Text String Data
        [AtCommand("AT+QISEND", CommandType.Write)]
        protected virtual Response Qisend(int connectionId, int? sendLength = null, string data = null, int? raiMode = null)
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
                byte[] bytes;
                switch(sendDataFormat)
                {
                    case DataFormat.Hex:
                        bytes = Misc.HexStringToByteArray(data);
                        break;
                    case DataFormat.Text:
                        bytes = StringEncoding.GetBytes(data);
                        break;
                    default:
                        throw new InvalidOperationException($"Invalid {nameof(sendDataFormat)}");
                }
                // Non-data mode is only supported with a fixed length
                if(sendLength == null || bytes.Length != sendLength)
                {
                    return Error;
                }
                else if(raiMode.HasValue)
                {
                    if(raiMode.Value < 0 || raiMode.Value > 2)
                    {
                        return Error;
                    }
                    this.Log(LogLevel.Debug, "QISEND: NB-IoT Release Assistance Indication set to {0}", raiMode);
                }
                this.Log(LogLevel.Debug, "ConnectionId {0} requested send of '{1}' in non-data mode",
                    connectionId, BitConverter.ToString(bytes));

                SendSocketData(bytes, connectionId);
                // After data is sent, modem disconnects after a period of inactivity.
                ExecuteWithDelay(() =>
                {
                    SendSignalingConnectionStatus(false);
                }, SendDataInactivityDisconnectDelay);
                return null;
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

                        SendSocketData(bytes, connectionId);
                    });
                });
                return null;
            }
        }

        [AtCommand("AT+QISTATE", CommandType.Read)]
        protected virtual Response QistateRead() => Ok.WithParameters(
            sockets.Where(s => s != null).Select(s => s.Qistate).ToArray());

        protected virtual bool IsValidContextId(int id)
        {
            return id >= 1 && id <= 16;
        }

        protected void SendSocketData(byte[] bytes, int connectionId)
        {
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
            SendResponse(Ok);
            // We can send the OK (the return value of this command) immediately,
            // but we have to wait before SEND OK/SEND FAIL if the network is too fast
            ExecuteWithDelay(() => SendString(sendResponse), 50);
            // A successful send means the signaling connection became active, but this
            // happens after the actual send notification hence the additional delay.
            ExecuteWithDelay(() => SendSignalingConnectionStatus(true), SendDataActivityConnectDelay);
        }

        protected void EnterDeepsleep()
        {
            if(!Enabled)
            {
                return;
            }

            this.Log(LogLevel.Debug, "Entering deep sleep mode");
            // Entering deep sleep mode also enables power saving mode
            SetPowerSavingMode(true);
            ReportNbiotEvent(NbiotEvent.EnterDeepsleep);
            // Entering deep sleep is equivalent to a power off, so we do a reset here.
            // NVRAM values will be preserved.
            Reset();
        }

        protected void EnableModem()
        {
            Enabled = true;
            // Notify the DTE that the modem is ready
            SendString(ModemReady);
            vddExt.Set();
        }

        protected void SetPowerSavingMode(bool enable)
        {
            if(powerSavingModeActive == enable)
            {
                return;
            }

            ReportNbiotEvent(enable ? NbiotEvent.EnterPowerSavingMode : NbiotEvent.ExitPowerSavingMode);
            powerSavingModeActive = enable;
        }

        protected void SendSignalingConnectionStatus(bool active)
        {
            if(SendCSCONOnChangeOnly && signalingConnectionActive == active)
            {
                return;
            }

            // When the signaling connection becomes active, we leave PSM and vice versa
            SetPowerSavingMode(!active);
            signalingConnectionActive = active;

            if(!signalingConnectionStatusReportingEnabled)
            {
                return;
            }

            SendString(CsconContent(true));
        }

        protected void ReportNbiotEvent(NbiotEvent kind)
        {
            string eventDescription = null;
            switch(kind)
            {
                case NbiotEvent.EnterDeepsleep:
                    if(deepSleepEventEnabled)
                    {
                        eventDescription = "ENTER DEEPSLEEP";
                    }
                    break;
                case NbiotEvent.EnterPowerSavingMode:
                    if(powerSavingModeEventEnabled)
                    {
                        eventDescription = "ENTER PSM";
                    }
                    break;
                case NbiotEvent.ExitPowerSavingMode:
                    if(powerSavingModeEventEnabled)
                    {
                        eventDescription = "EXIT PSM";
                    }
                    break;
            }
            if(eventDescription != null)
            {
                SendString($"+QNBIOTEVENT: \"{eventDescription}\"");
            }
        }

        // NETLIGHT pin is controlled by AT+QLEDMODE (BC660K, BC66) or AT+QCFG="ledmode" (BG96)
        // You can implement exact AT commands for particular models using this helper.
        protected Response SetNetLightMode(int ledMode)
        {
            if(ledMode < 0 || ledMode > 1)
            {
                this.Log(LogLevel.Warning, "Invalid mode for NETLIGHT pin: {0}", ledMode);
                return Error;
            }
            netLightMode = ledMode;
            return Ok;
        }

        protected abstract string Vendor { get; }
        protected abstract string ModelName { get; }
        protected abstract string Revision { get; }
        protected abstract string ManufacturerRevision { get; }
        protected abstract string SoftwareRevision { get; }

        protected bool echoInDataMode;

        // These fields are not affected by resets because they are automatically saved to NVRAM.
        protected string pdpContextApn = "";
        protected bool showLength = false;
        protected DataFormat sendDataFormat = DataFormat.Text;
        protected DataFormat receiveDataFormat = DataFormat.Text;
        protected string dataOutputSeparator = CrLf;
        protected string dataOutputSurrounding = ""; // for specific models it can be overriden in the constructor
        protected bool deepSleepEventEnabled = false;
        protected bool powerSavingModeEventEnabled;
        protected bool signalingConnectionStatusReportingEnabled;
        protected bool outOfServiceAreaUrcEnabled;
        protected NetworkRegistrationUrcType networkRegistrationUrcType;
        protected int netLightMode;
        protected FunctionalityLevel functionalityLevel;

        protected readonly string imeiNumber;
        protected readonly LimitTimer deepsleepTimer;

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

        private void BytesReceived(int connectionId, int byteCount)
        {
            // We do both of the sends here after a delay to accomodate software
            // which might not expect the "instant" reply which would otherwise happen.
            // Notify that the signaling connection is active
            ExecuteWithDelay(() => SendSignalingConnectionStatus(true), CsconDelay);
            // Send the data received notification. This needs to happen after the signaling
            // connection active notification, so we add a delay on top of CsconDelay.
            ExecuteWithDelay(() =>
            {
                var recvUrc = $"+QIURC: \"recv\",{connectionId}";
                if(showLength)
                {
                    recvUrc += $",{byteCount}";
                }
                SendString(recvUrc);
            }, CsconDelay + 500);
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

        // Return encoded string that represents the greatest available value
        // that is not greater than the requested one.
        protected string ConvertSecondsToEncodedString(int t, ModemTimerType timerType)
        {
            IReadOnlyDictionary<byte, int> keyMap;
            switch(timerType)
            {
                case ModemTimerType.ActiveTimeT3324:
                {
                    keyMap = activeTimeUnitToSecondsMultiplier;
                    break;
                }
                case ModemTimerType.PeriodicTauT3412:
                {
                    keyMap = periodicTauTimeUnitToSecondsMultiplier;
                    break;
                }
                default:
                {
                    throw new ArgumentException("Invalid timer type");
                }
            }

            byte selectedKey = 0b111;
            int selectedValue = 0;

            foreach(var entry in keyMap.OrderByDescending(x => x.Value))
            {
                if(t >= entry.Value)
                {
                    selectedKey = entry.Key;
                    selectedValue = entry.Value;
                    break;
                }
            }

            selectedValue = selectedValue == 0 ? 0 : t / selectedValue;

            const int timerValueWidth = 5;
            const int maxTimerValue = (1 << timerValueWidth) - 1; // 5 bits for coding timer value
            var timeByteCoded = selectedKey << timerValueWidth | (selectedValue & maxTimerValue);

            return Convert.ToString(timeByteCoded, 2).PadLeft(8, '0');
        }

        protected int ConvertEncodedStringToSeconds(string encoded, ModemTimerType timerType)
        {
            IReadOnlyDictionary<byte, int> keyMap;
            int defaultMultiplier;
            switch(timerType)
            {
                case ModemTimerType.ActiveTimeT3324:
                {
                    keyMap = activeTimeUnitToSecondsMultiplier;
                    defaultMultiplier = ActiveTimeDefaultUnitMultiplier;
                    break;
                }
                case ModemTimerType.PeriodicTauT3412:
                {
                    keyMap = periodicTauTimeUnitToSecondsMultiplier;
                    defaultMultiplier = PeriodicTauDefaultUnitMultiplier;
                    break;
                }
                default:
                {
                    throw new ArgumentException("Invalid timer type");
                }
            }

            if(!Misc.TryParseBitPattern(encoded, out var parsed, out _))
            {
                this.Log(LogLevel.Warning, "Unable to decode time code '{0}' - should be a bit-string", encoded);
                return 0;
            }
            var timerValue = BitHelper.GetValue((byte)parsed, 0, 5);
            var unit = BitHelper.GetValue((byte)parsed, 5, 3);

            if(keyMap.TryGetValue(unit, out var multiplier))
            {
                return multiplier * timerValue;
            }
            return defaultMultiplier * timerValue;
        }

        // When this is set to Numeric or Verbose, MT-related errors are reported with "+CME ERROR: "
        // instead of the plain "ERROR". This does not apply to syntax errors, invalid parameter
        // errors or Terminal Adapter functionality.
        private MobileTerminationResultCodeMode mtResultCodeMode;
        private MemoryStream dataBuffer;
        private int? dataBytesRemaining;
        private Action<byte[]> dataCallback;
        private bool inReset;
        private bool signalingConnectionActive;
        private bool powerSavingModeActive;

        private readonly string softwareVersionNumber;
        private readonly string serialNumber;
        private readonly IGPIO vddExt = new GPIO();
        private readonly IGPIO netLight = new GPIO();
        private readonly SocketService[] sockets = new SocketService[NumberOfConnections];

        private readonly IReadOnlyDictionary<byte, int> activeTimeUnitToSecondsMultiplier = new Dictionary<byte, int>
        {
            { 0b111, 0 },
            { 0b000, 2 },
            { 0b001, 60 },
            { 0b010, 6 * 60 }
        };

        private readonly IReadOnlyDictionary<byte, int> periodicTauTimeUnitToSecondsMultiplier = new Dictionary<byte, int>
        {
            { 0b111, 0 },
            { 0b011, 2 },
            { 0b100, 30 },
            { 0b101, 60 },
            { 0b000, 10 * 60 },
            { 0b001, 60 * 60 },
            { 0b010, 10 * 60 * 60 },
            { 0b110, 320 * 60 * 60 }
        };

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
        private const string DataModePrompt = ">";
        private const string SendOk = "SEND OK";
        private const string SendFailed = "SEND FAIL";
        private const string ModemReady = "RDY";
        private const int NumberOfConnections = 4;
        private const int ActiveTimeDefaultUnitMultiplier = 60;
        private const int PeriodicTauDefaultUnitMultiplier = 60 * 60;

        public enum NetworkRegistrationStates
        {
            NotRegisteredNotSearching,
            RegisteredHomeNetwork,
            NotRegisteredSearching,
            RegistrationDenied,
            Unknown,
            RegisteredRoaming,
        }

        public enum PublicLandMobileNetworkSearchingState
        {
            SearchingInactive,
            Searching,
            Selected,
            OutOfService
        }

        protected enum ModemTimerType
        {
            ActiveTimeT3324,
            PeriodicTauT3412
        }

        protected enum MobileTerminationResultCodeMode
        {
            Disabled,
            Numeric,
            Verbose,
        }

        protected enum ServiceType
        {
            Tcp,
            Udp,
            TcpListener,
            UdpService,
        }

        protected enum GPIOInput
        {
            Power,
            Reset,
            PsmEint,
        }

        protected enum PowerOffType
        {
            Normal,
            Immediate,
            Reset,
        }

        protected enum SerialNumberType
        {
            Device,
            Imei,
            ImeiSv,
            SoftwareVersionNumber,
        }

        protected enum FunctionalityLevel
        {
            Minimum,
            Full,
            RfTransmitReceiveDisabled = 4,
            UsimDisabled = 7,
        }

        protected enum NetworkRegistrationUrcType
        {
            Disabled,
            StatOnly,
            StatLocation,
            StatLocationEmmCause,
            StatLocationPsm,
            StatLocationEmmCausePsm,
        }

        protected enum DataFormat
        {
            Text,
            Hex,
        }

        protected enum PdpType
        {
            Ip,
            IpV6,
            IpV4V6,
            NonIp,
        }

        protected enum AuthenticationType
        {
            None,
            Pap,
            Chap,
        }

        protected enum NbiotEvent
        {
            EnterPowerSavingMode,
            ExitPowerSavingMode,
            EnterDeepsleep,
        }

        // One SocketService corresponds to one connectionId
        private class SocketService : IDisposable
        {
            public static SocketService Open(QuectelModem owner, int connectionId, ServiceType type, string remoteHost, ushort remotePort)
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
                connectedService.Disconnect();
            }

            public int BytesAvailable => connectedService.BytesAvailable;

            public string Qistate => $"+QISTATE: {ConnectionId},\"{Type.ToString().ToUpper()}\",\"{RemoteHost}\",{RemotePort}";

            public ServiceType Type { get; }
            public QuectelModem Owner { get; }
            public int ConnectionId { get; }
            public string RemoteHost { get; }
            public ushort RemotePort { get; }

            private void BytesReceived(int byteCount) => Owner.BytesReceived(ConnectionId, byteCount);

            private readonly IEmulatedNetworkService connectedService;

            private SocketService(QuectelModem owner, int connectionId, ServiceType type, IEmulatedNetworkService conn, string remoteHost, ushort remotePort)
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
    }
}
