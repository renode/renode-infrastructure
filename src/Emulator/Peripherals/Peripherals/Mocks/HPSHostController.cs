//
// Copyright (c) 2010-2024 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.I2C;
using Antmicro.Renode.Sockets;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities;
#if !PLATFORM_WINDOWS
using Mono.Unix;
#endif

namespace Antmicro.Renode.Extensions.Mocks
{
    public static class HPSHostControllerExtensions
    {
        public static void AddHPSHostController(this Emulation emulation, STM32F7_I2C device, string name = "HPSHostController")
        {
            emulation.HostMachine.AddHostMachineElement(new HPSHostController(device), name);
        }
    }

    public class HPSHostController : IHostMachineElement
    {
        public HPSHostController(STM32F7_I2C device)
        {
            currentSlave = device;
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
                // Poll until all the bytes are written
                PollForRegisterBit(RegisterBitName.RXNE);

                address += 256;
                left -= batchSize;
            }
        }

        public void CommandEraseSPIFlash()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemCommands,
                0x0,
                (byte)CommonSystemCommands.EraseSPIFlash,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void CommandEraseStage1()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemCommands,
                0x0,
                (byte)CommonSystemCommands.EraseStage1,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void CommandLaunchApplication()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemCommands,
                0x0,
                (byte)CommonSystemCommands.LaunchApplication,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void CommandLaunchStage1()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemCommands,
                0x0,
                (byte)CommonSystemCommands.LaunchStage1,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void CommandIssueReset()
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemCommands,
                0x0,
                (byte)CommonSystemCommands.Reset,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void SetNumberOfCameraTestIterations(int count)
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CameraTestIterations,
                0x0,
                (byte)count,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        public void SetOptionBytes(int value)
        {
            var data = new byte[] {
                ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.OptionBytesConfiguration,
                0x0,
                (byte)(value >> 8),
                (byte)value,
                0x0
            };
            IssueCommand(data);
            currentSlave.Read(0);
        }

        // Register 0:
        public byte[] ReadMagicNumber(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.MagicNumber});
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 1:
        public byte[] ReadHardwareVersion(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.HardwareVersion });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 2:
        public string[,] ReadSystemStatus(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SystemStatus });
            return FormatSystemStatus(GetBytesFromSlave(2, timeInterval));
        }

        // Register 5:
        public byte[] ReadMemoryBankAvailable(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.MemoryBankAvailable });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 6:
        public string[,] ReadError(TimeInterval timeInterval)
        {
            return FormatCommonErrorStatus(ReadErrorBytes(timeInterval));
        }

        public byte[] ReadErrorBytes(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Error });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 7: should be RW
        public string[,] ReadEnabledFeatures(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Enabledfeatures });
            return FormatEnabledFeatures(GetBytesFromSlave(2, timeInterval));
        }

        // Register 8:
        public string[,] ReadFeature0StatusBits(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Feature0 });
            return FormatFeature(GetBytesFromSlave(2, timeInterval));
        }

        // Register 9:
        public string[,] ReadFeature1StatusBits(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.Feature1 });
            return FormatFeature(GetBytesFromSlave(2, timeInterval));
        }

        // Register 10:
        public byte[] ReadFirmwareVersionHigh(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FirmwareVersionHIGH });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 11:
        public byte[] ReadFirmwareVersionLow(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FirmwareVersionLOW });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 12:
        public byte[] ReadFPGABootCount(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FPGABootCount });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 13:
        public byte[] ReadFPGALoopCount(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FPGALoopCount });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 14:
        public byte[] ReadFPGAROMVersion(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.FPGAROMVersion });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 15:
        public byte[] ReadSPIFlashStatus(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.SPIFlashStatusBits });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 16:
        public byte[] ReadDebugIndex(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.DebugIndex });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 17:
        public byte[] ReadDebugValue(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.DebugValue });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 18:
        public string[,] ReadCameraConfiguration(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CameraConfiguration });
            return FormatCameraConfiguration(GetBytesFromSlave(2, timeInterval));
        }

        // Register 19:
        public byte[] ReadCameraTestIterations(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.CameraTestIterations });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 20:
        public byte[] ReadOptionBytes(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.OptionBytesConfiguration });
            return GetBytesFromSlave(2, timeInterval);
        }

        // Register 21:
        public byte[] ReadPartIDs(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.PartIDs });
            return GetBytesFromSlave(20, timeInterval);
        }

        // Register 22:
        public byte[] ReadPreviousCrash(TimeInterval timeInterval)
        {
            IssueCommand(new byte[] { ((byte)Commands.RegisterAccess << 6) | (byte)RegisterAccessType.PreviousCrash });
            // PreviousCrash register can return up to 256 bytes, but we are limited to 255 bytes because the NBYTES field
            // in STM32F7_I2C model is 8 bytes long.
            return GetBytesFromSlave(255, timeInterval);
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

        // This method is for getting OA1EN,
        // returns true when OA1EN is enabled.
        private bool OA1ENEnabled()
        {
            return currentSlave.OwnAddress1Enabled;
        }

        // This method is for getting RXEN,
        // returns true when the buffer is empty.
        private bool RXNECleared()
        {
            return !currentSlave.RxNotEmpty;
        }

        private void PollForRegisterBit(RegisterBitName bitName)
        {
            int i = 0;
            Func<bool> registerAccess;
            switch(bitName)
            {
                case RegisterBitName.OA1EN:
                    registerAccess = OA1ENEnabled;
                    break;
                case RegisterBitName.RXNE:
                    registerAccess = RXNECleared;
                    break;
                default:
                    throw new RecoverableException("Register bit name does not exist.");
            }
            while(!registerAccess() && i < 8)
            {
                Thread.Sleep(500);
                i++;
            }
        }

        private void PollForError(TimeInterval timeInterval)
        {
            for(int i = 0; i < 3; i++)
            {
                var errors = ReadErrorBytes(timeInterval);
                if(errors[0] + errors[1] != 0x00)
                {
                    break;
                }
                Thread.Sleep(1000);
            }
        }

        // When we need to read for more than 255 bytes, we will need this method
        private byte[] GetLongBytes(int count, TimeInterval timeInterval)
        {
            var result = new byte[count];
            var rounds = count / ReadChunkSize;
            var remainder = count % ReadChunkSize;
            for(int i = 0; i <= rounds; i++)
            {
                int toRead = i == rounds ? remainder : ReadChunkSize;
                Array.Copy(GetBytesFromSlave(toRead, timeInterval), 0, result, i * ReadChunkSize, toRead);
            }
            return result;
        }

        public void StartSocketServer(TimeInterval timeInterval)
        {
#if PLATFORM_WINDOWS
            throw new RecoverableException("This method is not supported on Windows");
#else
            string path = "/tmp/i2c.sock";
            if(File.Exists(path))
            {
                File.Delete(path);
            }
            this.Log(LogLevel.Info, "Server starting at{0}", path);
            var socket = SocketsManager.Instance.AcquireSocket(this, AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified, new UnixEndPoint(path));
            this.Log(LogLevel.Info, "Connection established");

            currentSlave.Read(0);
            var connection = socket.Accept();

            var commandBuffer = new byte[(int)NumberOfBytes.MessageType];
            var prevWrite = false;
            var length = 0;

            while(SocketConnected(connection))
            {
                PollForRegisterBit(RegisterBitName.OA1EN);
                try
                {
                    PollForSocketData(connection, commandBuffer);
                }
                catch(Exception e)
                {
                    this.Log(LogLevel.Error, "{0} Exception caught.", e);
                    return;
                }
                switch((SocketI2CMessageType)commandBuffer[0])
                {
                    case SocketI2CMessageType.Stop:
                        currentSlave.FinishTransmission();
                        break;
                    // Parse read command
                    case SocketI2CMessageType.Read:
                        prevWrite = false;
                        length = GetLength(connection);

                        byte[] dataToSend = GetLongBytes(length, timeInterval);
                        try
                        {
                            PollForSocketData(connection, dataToSend, true);
                        }
                        catch(Exception e)
                        {
                            this.Log(LogLevel.Error, "{0} Exception caught.", e);
                            return;
                        }
                        break;
                    // Parse write command
                    case SocketI2CMessageType.Write:
                        // Seems that the slave needs a read operation
                        // to flush out the bits written
                        if(prevWrite)
                        {
                            currentSlave.Read(0);
                            PollForRegisterBit(RegisterBitName.RXNE);
                        }
                        prevWrite = true;
                        length = GetLength(connection);
                        byte[] data = new byte[length];
                        try
                        {
                            PollForSocketData(connection, data);
                        }
                        catch(Exception e)
                        {
                            this.Log(LogLevel.Error, "{0} Exception caught.", e);
                            return;
                        }
                        if(data.Length == 1 && data[0] == 0x86)
                        {
                            PollForError(timeInterval);
                            Thread.Sleep(10);
                        }
                        IssueCommand(data);
                        break;
                }
            }
#endif
        }

        private int GetLength(Socket connection)
        {
            var lengthBytes = new byte[(int)NumberOfBytes.LengthBytes];
            PollForSocketData(connection, lengthBytes);
            var combined = (int)lengthBytes[0] << 8 | lengthBytes[1];
            return combined;
        }

        private void PollForSocketData(Socket connection, byte[] data, bool send = false)
        {
            var tempLength = 0;
            var sendReceive = (send == true)
                ? connection.Send
                : (Func<byte[], int, int, SocketFlags, int>)connection.Receive;

            while(tempLength < data.Length)
            {
                tempLength += sendReceive(data, tempLength, data.Length - tempLength, SocketFlags.None);
            }
        }

        private void IssueCommand(byte[] data)
        {
            if(currentSlave == null)
            {
                throw new RecoverableException("Cannot issue command because no slave is connected.");
            }
            currentSlave.Write(data);
        }

        private bool SocketConnected(Socket s)
        {
            bool dataAvailableForReading = s.Poll(1000, SelectMode.SelectRead);
            bool availableBytesIsEmpty = (s.Available == 0);
            return !(dataAvailableForReading && availableBytesIsEmpty);
        }

        private string[,] FormatSystemStatus(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from System Status register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow(string.Empty, "15", string.Empty, string.Empty);
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow((data[0] & 0x20) >> 5 == 1 ? "1" : "0", "13", "ONE_TIME_INIT", "Whether the one_time_init binary is running");
            table.AddRow((data[0] & 0x10) >> 4 == 1 ? "1" : "0", "12", "STAGE0_PERM_LOCKED", "Whether stage0 has been made permanently read-only");
            table.AddRow((data[0] & 0x8) >> 3 == 1 ? "1" : "0", "11", "STAGE0_LOCKED", "Whether stage0 has been make read-only");
            table.AddRow((data[0] & 0x4) >> 2 == 1 ? "1" : "0", "10", "CMDINPROGRESS", "A command is in-progress");
            table.AddRow((data[0] & 0x2) >> 1 == 1 ? "1" : "0", "9", "APPLREADY", "Application is running, and features may be enabled");
            table.AddRow((data[0] & 0x1) == 1 ? "1" : "0", "8", "APPLRUN", "Stage 1 has been launched, and is now running");
            table.AddRow(string.Empty, "7", string.Empty, string.Empty);
            table.AddRow(string.Empty, "6", string.Empty, string.Empty);
            table.AddRow((data[1] & 0x20) >> 5 == 1 ? "1" : "0", "5", "WPOFF", "Write protect pin off");
            table.AddRow((data[1] & 0x10) >> 4 == 1 ? "1" : "0", "4", "WPON", "Write protect pin on");
            table.AddRow((data[1] & 0x8) >> 3 == 1 ? "1" : "0", "3", "STAGE0", "Stage 0 is running");
            table.AddRow(string.Empty, "2", string.Empty, string.Empty);
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

        private string[,] FormatEnabledFeatures(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from Enabled Features register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow(string.Empty, "15", string.Empty, string.Empty);
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow(string.Empty, "13", string.Empty, string.Empty);
            table.AddRow(string.Empty, "12", string.Empty, string.Empty);
            table.AddRow(string.Empty, "11", string.Empty, string.Empty);
            table.AddRow(string.Empty, "10", string.Empty, string.Empty);
            table.AddRow(string.Empty, "9", string.Empty, string.Empty);
            table.AddRow(string.Empty, "8", string.Empty, string.Empty);
            table.AddRow(string.Empty, "7", string.Empty, string.Empty);
            table.AddRow(string.Empty, "6", string.Empty, string.Empty);
            table.AddRow(string.Empty, "5", string.Empty, string.Empty);
            table.AddRow(string.Empty, "4", string.Empty, string.Empty);
            table.AddRow(string.Empty, "3", string.Empty, string.Empty);
            table.AddRow(string.Empty, "2", string.Empty, string.Empty);
            table.AddRow((data[1] & 0x2) >> 1 == 1 ? "1" : "0", "1", "FEATURE1", "Enable feature 1");
            table.AddRow((data[1] & 0x1) == 1 ? "1" : "0", "0", "FEATURE0", "Enable feature 0");
            return table.ToArray();
        }

        private string[,] FormatFeature(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from Feature N register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow((data[0] & 0x80) >> 7 == 1 ? "1" : "0", "15", "USABLE", "The feature result is valid to use");
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow(string.Empty, "13", string.Empty, string.Empty);
            table.AddRow(string.Empty, "12", string.Empty, string.Empty);
            table.AddRow(string.Empty, "11", string.Empty, string.Empty);
            table.AddRow(string.Empty, "10", string.Empty, string.Empty);
            table.AddRow(string.Empty, "9", string.Empty, string.Empty);
            table.AddRow(string.Empty, "8", string.Empty, string.Empty);
            table.AddRow(string.Empty, "7", string.Empty, string.Empty);
            table.AddRow(string.Empty, "6", string.Empty, string.Empty);
            table.AddRow(string.Empty, "5", string.Empty, string.Empty);
            table.AddRow(string.Empty, "4", string.Empty, string.Empty);
            table.AddRow(string.Empty, "3", string.Empty, string.Empty);
            table.AddRow(string.Empty, "2", string.Empty, string.Empty);
            table.AddRow(string.Empty, "1", string.Empty, string.Empty);
            table.AddRow(string.Empty, "0", string.Empty, string.Empty);
            return table.ToArray();
        }

        private string[,] FormatCameraConfiguration(byte[] data)
        {
            if(data.Length != 2)
            {
                throw new RecoverableException(string.Format("Received {0} bytes of data (expected 2) from Camera Configuration register.", data.Length));
            }
            var table = new Table().AddRow("Value", "Bit", "Name", "Description");
            table.AddRow(string.Empty, "15", string.Empty, string.Empty);
            table.AddRow(string.Empty, "14", string.Empty, string.Empty);
            table.AddRow(string.Empty, "13", string.Empty, string.Empty);
            table.AddRow(string.Empty, "12", string.Empty, string.Empty);
            table.AddRow(string.Empty, "11", string.Empty, string.Empty);
            table.AddRow(string.Empty, "10", string.Empty, string.Empty);
            table.AddRow(string.Empty, "9", string.Empty, string.Empty);
            table.AddRow(string.Empty, "8", string.Empty, string.Empty);
            table.AddRow(string.Empty, "7", string.Empty, string.Empty);
            table.AddRow(string.Empty, "6", string.Empty, string.Empty);
            table.AddRow(string.Empty, "5", string.Empty, string.Empty);
            table.AddRow(string.Empty, "4", string.Empty, string.Empty);
            table.AddRow(string.Empty, "3", string.Empty, string.Empty);
            table.AddRow(string.Empty, "2", string.Empty, string.Empty);
            table.AddRow((data[1] & 0x2) >> 1 == 1 ? "1" : "0", "1", "ROTATION", "Rotation bit 1");
            table.AddRow((data[1] & 0x1) == 1 ? "1" : "0", "0", "ROTATION", "Rotation bit 0");
            return table.ToArray();
        }

        private STM32F7_I2C currentSlave;

        private const int ReadChunkSize = 255;

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
            MagicNumber                 = 0,
            HardwareVersion             = 1,
            SystemStatus                = 2,
            SystemCommands              = 3,
            Unused                      = 4,
            MemoryBankAvailable         = 5,
            Error                       = 6,
            Enabledfeatures             = 7,
            Feature0                    = 8,
            Feature1                    = 9,
            FirmwareVersionHIGH         = 10,
            FirmwareVersionLOW          = 11,
            FPGABootCount               = 12,
            FPGALoopCount               = 13,
            FPGAROMVersion              = 14,
            SPIFlashStatusBits          = 15,
            DebugIndex                  = 16,
            DebugValue                  = 17,
            CameraConfiguration         = 18,
            CameraTestIterations        = 19,
            OptionBytesConfiguration    = 20,
            PartIDs                     = 21,
            PreviousCrash               = 22,
        }

        private enum CommonSystemCommands
        {
            Reset = 1,
            LaunchStage1 = 2,
            LaunchApplication = 4,
            EraseStage1 = 8,
            EraseSPIFlash = 16,
        }

        private enum RegisterBitName
        {
            OA1EN = 1,
            RXNE = 2,
        }

        private enum SocketI2CMessageType
        {
            Stop = 0,
            Read = 1,
            Write = 2,
        }

        private enum NumberOfBytes
        {
            MessageType = 1,
            LengthBytes = 2,
        }
    }
}
