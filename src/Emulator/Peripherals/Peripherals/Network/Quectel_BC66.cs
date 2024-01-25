//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC66 : QuectelModem
    {
        public Quectel_BC66(IMachine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
            nameModemConfigDecoder = new Dictionary<string, ModemConfigBC66>(StringComparer.OrdinalIgnoreCase)
            {
                {"epco", ModemConfigBC66.ExtendedProtocolConfigurationOptions},
                {"combinedattach", ModemConfigBC66.CombinedAttach},
                {"up", ModemConfigBC66.UserPlane},
                {"upopt", ModemConfigBC66.UserPlaneOptimization},
                {"multidrb", ModemConfigBC66.MultiDRB},
                {"autopdn", ModemConfigBC66.AutoActivationPDN},
                {"ripin", ModemConfigBC66.DefaultOutputLevelForRIPin},
                {"initlocktime", ModemConfigBC66.InitialSleepLockDuration},
                {"dsevent", ModemConfigBC66.DeepSleepEvent},
                {"atlocktime", ModemConfigBC66.SleepLockDurationByATCommand},
                {"urc/ri/mask", ModemConfigBC66.TriggerRIPinOnURC},
                {"vbattimes", ModemConfigBC66.VoltageDetectionCycleForAtQVBATT},
                {"activetimer", ModemConfigBC66.ActiveTimer}
            };

            modemBasicConfig = new Dictionary<ModemConfigBC66, int>()
            {
                {ModemConfigBC66.ExtendedProtocolConfigurationOptions, 0},
                {ModemConfigBC66.CombinedAttach, 0},
                {ModemConfigBC66.UserPlane, 0},
                {ModemConfigBC66.UserPlaneOptimization, 0},
                {ModemConfigBC66.MultiDRB, 0},
                {ModemConfigBC66.AutoActivationPDN, 0},
                {ModemConfigBC66.DefaultOutputLevelForRIPin, 0},
                {ModemConfigBC66.InitialSleepLockDuration, 0},
                {ModemConfigBC66.DeepSleepEvent, 0},
                {ModemConfigBC66.SleepLockDurationByATCommand, 0},
                // RI is an output ringing prompt and this setting can take form of 0,<URC> or 1,<URC>
                // to configure its behavior per URC basis. For now let's assume it's enabled for all URCs - value 2.
                {ModemConfigBC66.TriggerRIPinOnURC, 2},
                {ModemConfigBC66.VoltageDetectionCycleForAtQVBATT, 0},
                {ModemConfigBC66.ActiveTimer, 0}
            };
        }

        // QBANDSL - Set the List of Preferred Bands to Be Searched
        [AtCommand("AT+QBANDSL", CommandType.Write)]
        protected virtual Response QbandslWrite(int mode, int bandCount = 0, params int[] bands)
        {
            if(mode < 0 || mode > 1 || bandCount < 0 || bandCount > 4 || bands.Length != bandCount)
            {
                return Error;
            }

            return Ok; // stub
        }

        // AT+QLEDMODE - Configure Network-status-indication Light
        [AtCommand("AT+QLEDMODE", CommandType.Write)]
        protected virtual Response QledmodeWrite(int ledMode)
        {
            return SetNetLightMode(ledMode);
        }

        [AtCommand("AT+QLEDMODE", CommandType.Read)]
        protected virtual Response QledmodeRead() => Ok.WithParameters($"+QLEDMODE: {netLightMode}");

        // QCCLK - Set and Get Current Date and UTC
        [AtCommand("AT+QCCLK", CommandType.Read)]
        protected virtual Response QcclkRead()
        {
            return Ok.WithParameters(machine.RealTimeClockDateTime.ToString("yy/MM/dd,HH:mm:sszz"));
        }

        // QCCLK - Set and Get Current Date and UTC
        [AtCommand("AT+QCCLK", CommandType.Write)]
        protected virtual Response QcclkWrite(string dateTime)
        {
            this.Log(LogLevel.Warning, "Ignoring attempt to set date/time to '{0}'", dateTime);
            return Ok; // stub
        }

        // CCLK - Set and Get Current Date and Time
        [AtCommand("AT+CCLK", CommandType.Read)]
        protected virtual Response CclkRead()
        {
            return Ok.WithParameters("+CCLK: " + machine.RealTimeClockDateTime.ToString("yyyy/MM/dd,HH:mm:ss'GMT'zz"));
        }

        // CREG - Network Registration
        [AtCommand("AT+CREG", CommandType.Write)]
        protected override Response CregWrite(NetworkRegistrationUrcType type)
        {
            this.Log(LogLevel.Warning, "Command not available for this modem: 'AT+CREG'");
            return Error;
        }

        [AtCommand("AT+CREG", CommandType.Read)]
        protected override Response CregRead()
        {
            this.Log(LogLevel.Warning, "Command not available for this modem: 'AT+CREG'");
            return Error;
        }

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        protected override Response Qcfg(string function, params int[] args)
        {
            if(!nameModemConfigDecoder.TryGetValue(function, out var modemFunction))
            {
                return base.Qcfg(function, args); // unrecognized function
            }

            if(modemBasicConfig.TryGetValue(modemFunction, out int value))
            {
                if(args.Length == 0)
                {
                    // If the optional parameter is omitted, query the current configuration.
                    var parameters = string.Format("+QCFG: \"{0}\",{1}", function, value);
                    return Ok.WithParameters(parameters);
                }
                else if(args.Length == 1)
                {
                    modemBasicConfig[modemFunction] = args[0];
                    // Handle functions with side effects
                    switch(modemFunction)
                    {
                        case ModemConfigBC66.DeepSleepEvent:
                            deepSleepEventEnabled = args[0] != 0;
                            break;
                    }
                }
                else
                {
                    return base.Qcfg(function, args);
                }

                return Ok;
            }
            return base.Qcfg(function, args);
        }

        [AtCommand("AT+QCFG", CommandType.Read)]
        protected virtual Response QcfgRead()
        {
            var quotedFunctions = nameModemConfigDecoder.Keys.Select(key => key.SurroundWith("\""));
            var currentConfigValues = nameModemConfigDecoder.Values.Select(key => modemBasicConfig[key]);
            var parameters = $"+QCFG: ({string.Join(",", quotedFunctions)}),({string.Join(",", currentConfigValues)})";
            return Ok.WithParameters(parameters);
        }

        // QCGDEFCONT - Set Default PSD Connection Settings
        [AtCommand("AT+QCGDEFCONT", CommandType.Write)]
        protected virtual Response Qcgdefcont(PdpType pdpType, string apn = "", string username = "", string password = "")
        {
            pdpContextApn = apn;
            return Ok; // stub
        }

        // QEMMTIMER - Enable/Disable URC Reporting for EMM Timer
        [AtCommand("AT+QEMMTIMER", CommandType.Write)]
        protected virtual Response Qemmtimer(int enable = 0)
        {
            return Ok; // stub
        }

        // QICFG - Configure Optional TCP/IP Parameters
        [AtCommand("AT+QICFG", CommandType.Write)]
        protected override Response Qicfg(string parameter, params int[] args)
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
                    return base.Qicfg(parameter, args);
            }
            return Ok;
        }

        // QRELLOCK - Release Sleep Lock of AT Commands
        [AtCommand("AT+QRELLOCK")]
        protected virtual Response Qrellock()
        {
            // This command is meant to release the 10-second sleep lock timer after each
            // AT command, not necessarily make the modem enter sleep mode immediately.
            // We use it as a signal that the software talking to the modem expects it to
            // enter sleep mode, so we enter sleep mode if this was requested with the
            // DeepsleepOnRellock property.
            if(DeepsleepOnRellock && !deepsleepTimer.Enabled)
            {
                // The signaling connection goes inactive before entering sleep mode.
                SendSignalingConnectionStatus(false);
                ExecuteWithDelay(EnterDeepsleep, 50);
            }
            return Ok;
        }

        protected override bool IsValidContextId(int id)
        {
            return id == 1;
        }

        protected override string Vendor => "Quectel_Ltd";
        protected override string ModelName => "Quectel_BC66";
        protected override string Revision => "Revision: MTK_2625";
        protected override string ManufacturerRevision => "BC66NBR01A01";
        protected override string SoftwareRevision => "01.002.01.002";

        private readonly Dictionary<string, ModemConfigBC66> nameModemConfigDecoder;
        private readonly Dictionary<ModemConfigBC66, int> modemBasicConfig;

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";

        private enum ModemConfigBC66
        {
            ExtendedProtocolConfigurationOptions,
            CombinedAttach,
            UserPlane,
            UserPlaneOptimization,
            MultiDRB,
            AutoActivationPDN,
            DefaultOutputLevelForRIPin,
            InitialSleepLockDuration,
            DeepSleepEvent,
            SleepLockDurationByATCommand,
            TriggerRIPinOnURC,
            VoltageDetectionCycleForAtQVBATT,
            ActiveTimer,
        }
    }
}
