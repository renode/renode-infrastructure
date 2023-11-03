//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Peripherals.Network
{
    public class Quectel_BC66 : QuectelModem
    {
        public Quectel_BC66(IMachine machine, string imeiNumber = DefaultImeiNumber,
            string softwareVersionNumber = DefaultSoftwareVersionNumber,
            string serialNumber = DefaultSerialNumber) : base(machine, imeiNumber, softwareVersionNumber, serialNumber)
        {
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

        // QCFG - System Configuration
        [AtCommand("AT+QCFG", CommandType.Write)]
        protected override Response Qcfg(string function, int value)
        {
            switch(function)
            {
                case "dsevent":
                    deepSleepEventEnabled = value != 0;
                    break;
                case "activetimer": // whether to use the value of active timer or not
                case "atlocktime": // Sleep Lock duration after AT command
                case "autopdn": // PDN auto activation option
                case "combinedattach": // combined attach
                case "epco": // extended protocol configuration options
                case "initlocktime": // initial Sleep Lock duration after reboot or deep sleep wake up
                case "multidrb": // enable multi-DRB
                case "ripin": // default output level for the RI pin
                case "up": // enable user plane function
                case "upopt": // enable user plane optimization
                case "urc/ri/mask": // whether to trigger RI pin behavior when URC is reported
                case "vbattimes": // voltage detection cycle for the AT+QVBATT command
                    this.Log(LogLevel.Warning, "Config value '{0}' set to {1}, not implemented", function, value);
                    break;
                default:
                    return base.Qcfg(function, value);
            }
            return Ok;
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
            if(DeepsleepOnRellock)
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

        private const string DefaultImeiNumber = "866818039921444";
        private const string DefaultSoftwareVersionNumber = "31";
        private const string DefaultSerialNumber = "<serial number>";
    }
}
