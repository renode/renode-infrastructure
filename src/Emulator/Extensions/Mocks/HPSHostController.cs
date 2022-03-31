//
// Copyright (c) 2010-2022 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Time;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Extensions.Mocks
{
    public static class HPSHostControllerExtensions
    {
        public static void AddHPSHostController(this Emulation emulation, string name = "HPSHostController")
        {
            emulation.HostMachine.AddHostMachineElement(new HPSHostController(), name);
        }
    }

    public class HPSHostController : IHostMachineElement, IConnectable<II2CPeripheral>
    {
        public void AttachTo(II2CPeripheral obj)
        {
            currentSlave = obj;
        }

        public void DetachFrom(II2CPeripheral obj)
        {
            currentSlave = null;
        }

        public void FlashMCU(ReadFilePath path)
        {
            var address = 0;
            var data = new byte[256 + 5];
            data[0] = ((byte)Commands.WriteMemory << 6) | (byte)MemoryBanks.MCUFlash;

            var bytes = File.ReadAllBytes(path);
            var left = bytes.Length;

            while(left > 0)
            {
                var batchSize = Math.Min(left, 256);
                Array.Copy(bytes, address, data, 5, batchSize);
                data[1] = (byte)(address >> 24);
                data[2] = (byte)(address >> 16);
                data[3] = (byte)(address >> 8);
                data[4] = (byte)address;
                IssueCommand(data);

                currentSlave.Read(0);
                // The software needs time to handle the data
                Thread.Sleep(200);

                address += 256;
                left -= batchSize;
            }
        }

        public void LaunchStage1()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CommonSystemCommands,
                0x0,
                (byte)CommonSystemCommands.Launch,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void IssueReset()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CommonSystemCommands,
                0x0,
                (byte)CommonSystemCommands.Reset,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public byte[] ReadMagicNumber(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.MagicNumber});
            return GetBytesFromSlave(2, timeInterval);
        }

        public byte[] ReadHardwareVersion(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.HardwareVersion });
            return GetBytesFromSlave(2, timeInterval);
        }

        [UiAccessible]
        public string[,] ReadCommonSystemStatus(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CommonSystemStatus });
            return FormatCommonSystemStatus(GetBytesFromSlave(2, timeInterval));
        }

        public byte[] ReadApplicationVersion(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.ApplicationVersion });
            return GetBytesFromSlave(2, timeInterval);
        }

        [UiAccessible]
        public string[,] ReadCommonErrorStatus(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CommonErrorStatus });
            return FormatCommonErrorStatus(GetBytesFromSlave(2, timeInterval));
        }

        public byte[] ReadFeature1StatusBits(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Feature1StatusBits });
            return GetBytesFromSlave(2, timeInterval);
        }

        public byte[] ReadFeature2StatusBits(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Feature2StatusBits });
            return GetBytesFromSlave(2, timeInterval);
        }

        public byte[] ReadFirmwareVersionHigh(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FirmwareVersionHIGH });
            return GetBytesFromSlave(2, timeInterval);
        }

        public byte[] ReadFirmwareVersionLow(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FirmwareVersionLOW });
            return GetBytesFromSlave(2, timeInterval);
        }

        private byte[] GetBytesFromSlave(int count, TimeInterval timeInterval)
        {
            if(currentSlave == null)
            {
                throw new RecoverableException("Cannot read data because no slave is connected.");
            }
            var result = new byte[count];
            var sw = new Stopwatch();
            sw.Start();
            do
            {
                result = currentSlave.Read(count);
                if(result.Length == count)
                {
                    break;
                }
                if(sw.Elapsed > timeInterval.ToTimeSpan())
                {
                    currentSlave.Read(0);
                    result = new byte[count];
                    break;
                }
            } while(result.Length == 0);
            return result;
        }

        private void IssueCommand(byte[] data)
        {
            if(currentSlave == null)
            {
                throw new RecoverableException("Cannot issue command because no slave is connected.");
            }
            currentSlave.Write(data);
        }

        private string[,] FormatCommonSystemStatus(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from System Status register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow(string.Empty, "15", string.Empty, string.Empty);
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow(string.Empty, "13", string.Empty, string.Empty);
            table.AddRow(string.Empty, "12", string.Empty, string.Empty);
            table.AddRow((data[0] & 0x8) >> 3 == 1 ? "1" : "0", "11", "SPINOTVER", "FPGA SPI flash has failed verification");
            table.AddRow((data[0] & 0x4) >> 2 == 1 ? "1" : "0", "10", "SPIVERIFY", "FPGA SPI flash is verified");
            table.AddRow((data[0] & 0x2) >> 1 == 1 ? "1" : "0", "9", "APPLREADY", "Application is running, and features may be enabled");
            table.AddRow((data[0] & 0x1) == 1 ? "1" : "0", "8", "APPLRUN", "Stage 1 has been launched, and is now running");
            table.AddRow(string.Empty, "7", string.Empty, string.Empty);
            table.AddRow(string.Empty, "6", string.Empty, string.Empty);
            table.AddRow((data[1] & 0x20) >> 5 == 1 ? "1" : "0", "5", "WPOFF", "Write protect pin off");
            table.AddRow((data[1] & 0x10) >> 4 == 1 ? "1" : "0", "4", "WPON", "Write protect pin on");
            table.AddRow((data[1] & 0x8) >> 3 == 1 ? "1" : "0", "3", "ANONVER", "The stage 1 image has failed to verify");
            table.AddRow((data[1] & 0x4) >> 2 == 1 ? "1" : "0", "2", "AVERIFY", "The stage 1 image is verified");
            table.AddRow((data[1] & 0x2) >> 1 == 1 ? "1" : "0", "1", "FAULT", "System has an unrecoverable fault");
            table.AddRow((data[1] & 0x1) == 1 ? "1" : "0", "0", "OK", "System is operational");
            return table.ToArray();
        }

        private string[,] FormatCommonErrorStatus(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from Error Status register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow(string.Empty, "15", string.Empty, string.Empty);
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow(string.Empty, "13", string.Empty, string.Empty);
            table.AddRow(string.Empty, "12", string.Empty, string.Empty);
            table.AddRow(string.Empty, "11", string.Empty, string.Empty);
            table.AddRow(string.Empty, "10", string.Empty, string.Empty);
            table.AddRow((data[0] & 0x2) >> 1 == 1 ? "1" : "0", "9", "BUFORUN", "Buffer overrun");
            table.AddRow((data[0] & 0x1) == 1 ? "1" : "0", "8", "BUFNAVAIL", "Buffer not available");
            table.AddRow((data[1] & 0x80) >> 7 == 1 ? "1" : "0", "7", "I2CBADREQ", "A bad I2C request was made");
            table.AddRow((data[1] & 0x40) >> 6 == 1 ? "1" : "0", "6", "SPIFLASH", "SPI flash access failed");
            table.AddRow((data[1] & 0x20) >> 5 == 1 ? "1" : "0", "5", "CAMERA", "Camera not functional");
            table.AddRow((data[1] & 0x10) >> 4 == 1 ? "1" : "0", "4", "I2CORUN", "I2C overrun");
            table.AddRow((data[1] & 0x8) >> 3 == 1 ? "1" : "0", "3", "I2CBERR", "I2C bus error");
            table.AddRow((data[1] & 0x4) >> 2 == 1 ? "1" : "0", "2", "PANIC", "A panic occurred");
            table.AddRow((data[1] & 0x2) >> 1 == 1 ? "1" : "0", "1", "MCUFLASH", "Error writing to MCU flash");
            table.AddRow((data[1] & 0x1) == 1 ? "1" : "0", "0", "I2CURUN", "I2C underrun");
            return table.ToArray();
        }

        private II2CPeripheral currentSlave;

        private enum Commands
        {
            WriteMemory     = 0,
            Unused          = 1,
            RegisterAccess  = 2
        }

        private enum MemoryBanks
        {
            MCUFlash = 0,
            SPIFlash = 1
        }

        private enum RegisterAccessType
        {
            MagicNumber             = 0,
            HardwareVersion         = 1,
            CommonSystemStatus      = 2,
            CommonSystemCommands    = 3,
            ApplicationVersion      = 4,
            MemoryBankStatus        = 5,
            CommonErrorStatus       = 6,
            FeatureEnable           = 7,
            Feature1StatusBits      = 8,
            Feature2StatusBits      = 9,
            FirmwareVersionHIGH     = 10,
            FirmwareVersionLOW      = 11
        }

        private enum CommonSystemCommands
        {
            Reset = 1,
            Launch = 2
        }
    }
}
