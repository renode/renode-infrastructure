//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC660K : QuectelModem
    {
        public Quectel_BC660K(IMachine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
            dataOutputSurrounding = "\"";

            nameModemConfigDecoder = new Dictionary<string, ModemConfigBC660K>(StringComparer.OrdinalIgnoreCase)
            {
                {"EPCO", ModemConfigBC660K.ExtendedProtocolConfigurationOptions},
                {"DataInactTimer", ModemConfigBC660K.DataInactivityTimer},
                {"OOSScheme", ModemConfigBC660K.NetworkSearchingMechanismInOOS},
                {"logbaudrate", ModemConfigBC660K.LogBaudRate},
                {"slplocktimes", ModemConfigBC660K.InitialSleepLockDuration},
                {"dsevent", ModemConfigBC660K.DeepSleepEvent},
                {"statisr", ModemConfigBC660K.ReportIntervalOfStatisticsURC},
                {"MacRAI", ModemConfigBC660K.MacRAI},
                {"relversion", ModemConfigBC660K.ProtocolVersionSupported},
                {"NBcategory", ModemConfigBC660K.NBCategory},
                {"wakeupRXD", ModemConfigBC660K.WakeUpByRXD},
                {"faultaction", ModemConfigBC660K.ActionOnError},
                {"GPIO", ModemConfigBC660K.GPIOStatusConfiguration},
                {"NcellMeas", ModemConfigBC660K.NeighborCellMeasurement},
                {"SimBip", ModemConfigBC660K.SIMBIP},
                {"activetimer", ModemConfigBC660K.ActiveTimer}
            };

            modemBasicConfig = new Dictionary<ModemConfigBC660K, int>()
            {
                {ModemConfigBC660K.ExtendedProtocolConfigurationOptions, 1},
                {ModemConfigBC660K.DataInactivityTimer, 60},
                {ModemConfigBC660K.NetworkSearchingMechanismInOOS, 1},
                {ModemConfigBC660K.LogBaudRate, 6000000},
                {ModemConfigBC660K.InitialSleepLockDuration, 10},
                {ModemConfigBC660K.DeepSleepEvent, 1},
                {ModemConfigBC660K.ReportIntervalOfStatisticsURC, 0},
                {ModemConfigBC660K.MacRAI, 0},
                {ModemConfigBC660K.ProtocolVersionSupported, 13},
                {ModemConfigBC660K.NBCategory, 1},
                {ModemConfigBC660K.WakeUpByRXD, 1},
                {ModemConfigBC660K.ActionOnError, 4},
                // ModemConfigBC660K.GPIOStatusConfiguration // GPIO configuration is stored separately
                {ModemConfigBC660K.NeighborCellMeasurement, 1},
                {ModemConfigBC660K.SIMBIP, 1},
                {ModemConfigBC660K.ActiveTimer, 0}
            };

            gpioConfig = new Dictionary<int, GPIOStatusConfiguration>()
            {
                {1, new GPIOStatusConfiguration()},
                {2, new GPIOStatusConfiguration()},
                {3, new GPIOStatusConfiguration()},
                {4, new GPIOStatusConfiguration()}
            };

            deepSleepEventEnabled = true;
        }

        // AT+QR14FEATURE - Query Status of R14 Features
        [AtCommand("AT+QR14FEATURE", CommandType.Execution)]
        protected virtual Response Qr14feature()
        {
            var parameters = new string[]
            {
                "+QR14FEATURE: 14,1",      // UE supports R14 protocol, MAC RAI is enabled.
                "+QR14FEATURE: 0",         // None of the features listed below is enabled by the network.
                // The options below have the following meaning:
                // MAC RAI status is disabled.
                // 2-HARQ is disabled.
                // Random access on non-anchor carrier is not supported.
                // Paging on non-anchor carrier is not supported.
                // Re-establishing with CP-CIOT is not supported.
                "+QR14FEATURE: 0,0,0,0,0"
            };
            return Ok.WithParameters(parameters); // stub
        }

        // AT+QLEDMODE - Configure Network-status-indication Light
        [AtCommand("AT+QLEDMODE", CommandType.Write)]
        protected virtual Response QledmodeWrite(int ledMode)
        {
            return SetNetLightMode(ledMode);
        }

        [AtCommand("AT+QLEDMODE", CommandType.Read)]
        protected virtual Response QledmodeRead() => Ok.WithParameters($"+QLEDMODE: {netLightMode}");

        // CCLK - Set and Get Current Date and Time
        [AtCommand("AT+CCLK", CommandType.Read)]
        protected virtual Response CclkRead()
        {
            return Ok.WithParameters("+CCLK: " + machine.RealTimeClockDateTime.ToString("yy/MM/dd,HH:mm:sszz"));
        }

        // CGPADDR - Show PDP Addresses
        [AtCommand("AT+CGPADDR", CommandType.Read)]
        protected override Response Cgpaddr() => Ok.WithParameters($"+CGPADDR: 1,\"{NetworkIp}\""); // stub

        // CREG - Network Registration
        [AtCommand("AT+CREG", CommandType.Write)]
        protected override Response CregWrite(NetworkRegistrationUrcType type)
        {
            if(type > NetworkRegistrationUrcType.StatLocationEmmCause)
            {
                this.Log(LogLevel.Warning, "AT+CREG: Argument <n> set to {0}, not supported by this modem", (int)type);
                return Error;
            }
            return base.CregWrite(type);
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
                        case ModemConfigBC660K.DeepSleepEvent:
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

            // Handle functions not covered by basic config
            switch(modemFunction)
            {
                case ModemConfigBC660K.GPIOStatusConfiguration:
                    var isOk = ConfigureGPIOStatus(out var parameters, args);
                    if(isOk)
                    {
                        return Ok.WithParameters(parameters);
                    }
                    break;
            }

            return base.Qcfg(function, args);
        }

        [AtCommand("AT+QCFG", CommandType.Read)]
        protected virtual Response QcfgRead()
        {
            var parameters = new List<string>();
            foreach(string function in nameModemConfigDecoder.Keys)
            {
                var modemFunction = nameModemConfigDecoder[function];
                if(modemBasicConfig.TryGetValue(modemFunction, out int value))
                {
                    parameters.Add(string.Format("+QCFG: \"{0}\",{1}", function, value));
                }
                else
                {
                    switch(modemFunction)
                    {
                        case ModemConfigBC660K.GPIOStatusConfiguration:
                            parameters.Add(GetQcfgGPIOStatus());
                            break;
                    }
                }
            }

            return Ok.WithParameters(parameters.ToArray());
        }

        // QCGDEFCONT - Set Default PSD Connection Settings
        [AtCommand("AT+QCGDEFCONT", CommandType.Write)]
        protected virtual Response Qcgdefcont(PdpType pdpType, string apn = "", string username = "",
            string password = "", AuthenticationType authenticationType = AuthenticationType.None)
        {
            pdpContextApn = apn;
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
                case "showRA": // display the address of the remote end while displaying received data
                    this.Log(LogLevel.Warning, "TCP/IP config value '{0}' set to {1}, not implemented", parameter, args.Stringify());
                    break;
                default:
                    return base.Qicfg(parameter, args);
            }
            return Ok;
        }

        // QNBIOTRAI - NB-IoT Release Assistance Indication
        [AtCommand("AT+QNBIOTRAI", CommandType.Write)]
        protected override Response Qnbiotrai(int raiMode = 0)
        {
            if(raiMode > 1)
            {
                return Error;
            }

            return base.Qnbiotrai(raiMode);
        }

        // QOOSAIND - Enable or Disable OOSA URC
        [AtCommand("AT+QOOSAIND", CommandType.Write)]
        protected virtual Response QoosaindWrite(int oosaUrcEnabled)
        {
            if(oosaUrcEnabled < 0 || oosaUrcEnabled > 1)
            {
                this.Log(LogLevel.Warning, "AT+QOOSAIND: Parameter QOOSAIND set to {0}, not supported by this modem", oosaUrcEnabled);
                return Error;
            }

            outOfServiceAreaUrcEnabled = oosaUrcEnabled == 1;
            return Ok;
        }

        [AtCommand("AT+QOOSAIND", CommandType.Read)]
        protected virtual Response QoosaindRead() => Ok.WithParameters($"+QOOSAIND: {(outOfServiceAreaUrcEnabled ? 1 : 0)}");

        // QPLMNS - Search PLMN
        [AtCommand("AT+QPLMNS", CommandType.Execution)]
        protected virtual Response QplmnsExec()
        {
            if(ModemPLMNState != PublicLandMobileNetworkSearchingState.OutOfService)
            {
                const int errorCode = 111; // errorCode: PLMN not allowed
                return new Response($"+CME ERROR: {errorCode}");
            }
            ModemPLMNState = PublicLandMobileNetworkSearchingState.Searching;
            return Ok;
        }

        [AtCommand("AT+QPLMNS", CommandType.Read)]
        protected virtual Response QplmnsRead()
        {
            var fragments = new List<string>
            {
                ((int)ModemPLMNState).ToString()
            };

            if(ModemPLMNState == PublicLandMobileNetworkSearchingState.OutOfService)
            {
                fragments.Add(PLMNSearchTime.ToString());
            }

            return Ok.WithParameters("+QPLMNS: " + string.Join(",", fragments));
        }

        // QPSMS - Power Saving Mode Setting
        [AtCommand("AT+QPSMS", CommandType.Read)]
        protected virtual Response QpsmsRead() => Ok.WithParameters($"+QPSMS: {ActiveTimeSeconds},{PeriodicTauSeconds}");

        [AtCommand("AT+QPSMS", CommandType.Write)]
        protected virtual Response QpsmsWrite(int t1, int t2)
        {
            SetPowerSavingMode(true);
            ActiveTime = ConvertSecondsToEncodedString(t1, ModemTimerType.ActiveTimeT3324);
            PeriodicTau = ConvertSecondsToEncodedString(t2, ModemTimerType.PeriodicTauT3412);

            return Ok;
        }

        // QPSC - Power Saving Control
        [AtCommand("AT+QPSC", CommandType.Read)]
        protected virtual Response QpscRead() => Ok.WithParameters($"+QPSC: \"minT3324\",{minimumT3324},\"minT3412\",{minimumT3412},\"minTeDRX\",{minimumTeDRX}");

        [AtCommand("AT+QPSC", CommandType.Write)]
        protected virtual Response QpscWrite(int minT3324, int minT3412, int minTeDRX)
        {
            minimumT3324 = minT3324;
            minimumT3412 = minT3412;
            minimumTeDRX = minTeDRX;

            return Ok;
        }

        [AtCommand("AT+QPSC", CommandType.Execution)]
        protected virtual Response QpscExec()
        {
            minimumT3324 = 0;
            minimumT3412 = 0;
            minimumTeDRX = 0;

            return Ok;
        }

        protected override bool IsValidContextId(int id)
        {
            return id == 0;
        }

        private bool ConfigureGPIOStatus(out string parameters, params int[] args)
        {
            parameters = string.Empty;

            if(args.Length == 0)
            {
                // If the optional parameter is omitted, query the current configuration
                parameters = GetQcfgGPIOStatus();
                return true;
            }

            switch((GPIOStatusOperation)args[0])
            {
                case GPIOStatusOperation.Initialize:
                {
                    // no parameter shall be omitted
                    if(args.Length != 5)
                    {
                        break;
                    }

                    var pin = args[1];
                    if(gpioConfig.TryGetValue(pin, out var gpioStatus))
                    {
                        gpioStatus.Direction = args[2];
                        gpioStatus.PullTypeSelection = args[3];
                        gpioStatus.LogicLevel = args[4];
                        return true;
                    }

                    break;
                }
                case GPIOStatusOperation.Query:
                {
                    // query the current configuration
                    parameters = GetQcfgGPIOStatus();
                    return true;
                }
                case GPIOStatusOperation.Configure:
                {
                    if(args.Length != 3)
                    {
                        break;
                    }

                    var pin = args[1];
                    if(gpioConfig.TryGetValue(pin, out var gpioStatus))
                    {
                        gpioStatus.LogicLevel = args[2];
                        return true;
                    }

                    break;
                }
            }

            return false;
        }

        private string GetQcfgGPIOStatus()
        {
            const string function = "GPIO";
            var gpioStatus = string.Join(",", gpioConfig.Values.Select(gpio => gpio.LogicLevel));
            return string.Format("+QCFG: \"{0}\",{1}", function, gpioStatus);
        }

        protected override string Vendor => "Quectel_Ltd";
        protected override string ModelName => "Quectel_BC660K-GL";
        protected override string Revision => "Revision: QCX212";
        protected override string ManufacturerRevision => "BC660KGLAAR01A03";
        protected override string SoftwareRevision => "01.002.01.002";

        private int minimumT3324;
        private int minimumT3412;
        private int minimumTeDRX;
        private readonly Dictionary<string, ModemConfigBC660K> nameModemConfigDecoder;
        private readonly Dictionary<ModemConfigBC660K, int> modemBasicConfig;
        private readonly Dictionary<int, GPIOStatusConfiguration> gpioConfig;

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";

        private enum ModemConfigBC660K
        {
            ExtendedProtocolConfigurationOptions,
            DataInactivityTimer,
            NetworkSearchingMechanismInOOS,
            LogBaudRate,
            InitialSleepLockDuration,
            DeepSleepEvent,
            ReportIntervalOfStatisticsURC,
            MacRAI,
            ProtocolVersionSupported,
            NBCategory,
            WakeUpByRXD,
            ActionOnError,
            GPIOStatusConfiguration,
            NeighborCellMeasurement,
            SIMBIP,
            ActiveTimer
        }

        private enum GPIOStatusOperation
        {
            Initialize = 1,
            Query = 2,
            Configure = 3
        }

        private sealed class GPIOStatusConfiguration
        {
            public int Direction { get; set; }
            public int PullTypeSelection { get; set; }
            public int LogicLevel { get; set; }
        }
    }
}
