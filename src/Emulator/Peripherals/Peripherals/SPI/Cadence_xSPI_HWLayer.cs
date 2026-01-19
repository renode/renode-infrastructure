//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.SPI.SFDP;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI
{
    // Represents hardware-layer configuration registers whose values have no effect on how the peripheral actually operates in Renode.
    public partial class Cadence_xSPI
    {
        private void ExecuteDiscovery()
        {
            var isFullDiscoverySet = fullDiscovery.Value;
            if(!TryGetByAddress((int)discoveryBnk.Value, out var spiPeripheral))
            {
                this.ErrorLog("Device discovery attempted, but device not found");
                return;
            }

            var sfdpSig = (byte[])null;
            var decodedSFDP = (SFDPData)null;
            if(isFullDiscoverySet)
            {
                if(spiPeripheral is ISFDPPeripheral sfdpPeripheral)
                {
                    sfdpSig = sfdpPeripheral.SFDPSignature;
                    if(!SFDPData.TryDecodeAsSFDP(sfdpSig, out decodedSFDP))
                    {
                        this.WarningLog("SFDPSignature decoding failed during full device discovery. Executing partial discovery instead.");
                        isFullDiscoverySet = false;
                    }
                }
                else
                {
                    this.WarningLog("Device's SFDPSignature was not found during full device discovery. Executing partial discovery instead.");
                    isFullDiscoverySet = false;
                }
            }

            CommonSequenceDiscovery(decodedSFDP, isFullDiscoverySet);
            ResetSequenceDiscovery(decodedSFDP, isFullDiscoverySet);
            EraseSequenceDiscovery(decodedSFDP, isFullDiscoverySet);
            WESequenceDiscovery();
            ReadSequenceDiscovery(decodedSFDP, isFullDiscoverySet);
            ProgramSequenceDiscovery(decodedSFDP, isFullDiscoverySet);
        }

        private void CommonSequenceDiscovery(SFDPData sfdp, bool fullDiscovery)
        {
            var defaultProfile1PageSizeProgram = 8UL;
            var defaultProfile1PageSizeRead = 15UL;

            if(fullDiscovery)
            {
                var jedecParam = sfdp.JedecParameter;
                pageSizeProgram.Value = (ulong)Math.Log(jedecParam.PageSize, 2);
                dataSwap888.Value = jedecParam.ByteOrder8d8d8d;
            }
            else
            {
                pageSizeProgram.Value = defaultProfile1PageSizeProgram;
                dataSwap888.Value = false;
            }

            // Common for profile 1 devices
            pageSizeRead.Value = defaultProfile1PageSizeRead;
        }

        private void ResetSequenceDiscovery(SFDPData sfdp, bool fullDiscovery)
        {
            var defaultSoftResetCmd = 0xF0UL;
            var defaultResetCmd = 0x99UL;
            var defaultResetEnableCmd = 0x66UL;
            var defaultDataConfirmationCmd = 0xD0UL;

            if(fullDiscovery)
            {
                var jedecParam = sfdp.JedecParameter;
                if(jedecParam.CmdF0Supported)
                {
                    resetCmd0Enabled.Value = false;
                    resetCmd0.Value = defaultResetEnableCmd;
                    resetCmd1.Value = defaultSoftResetCmd;
                }
                else
                {
                    resetCmd0Enabled.Value = true;
                    resetCmd0.Value = defaultResetEnableCmd;
                    resetCmd1.Value = defaultResetCmd;
                }
                return;
            }
            else
            {
                resetCmd0Enabled.Value = true;
                resetCmd0.Value = defaultResetEnableCmd;
                resetCmd1.Value = defaultResetCmd;
            }

            // Common for profile 1 devices
            resetCmd1ConfirmationEnabled.Value = false;
            resetCmd1Confirmation.Value = defaultDataConfirmationCmd;
        }

        private void EraseSequenceDiscovery(SFDPData sfdp, bool fullDiscovery)
        {
            var defaultEraseSectorCmd = 0x20UL;
            var defaultEraseSector4ByteCmd = 0x21UL;
            var defaultEraseSectorSize = 18UL;

            if(fullDiscovery)
            {
                var jedecParam = sfdp.JedecParameter;
                eraseCmd.Value = jedecParam.EraseCode;
                sectorSize.Value = (ulong)Math.Log(jedecParam.EraseSize, 2);
            }
            else
            {
                eraseCmd.Value = discoveryABNUM.Value ? defaultEraseSector4ByteCmd : defaultEraseSectorCmd;
                sectorSize.Value = defaultEraseSectorSize;
            }

            // Common for profile 1 devices
            eraseAllCmd.Value = 0xC7;
        }

        private void WESequenceDiscovery()
        {
            var defaultWECmd = 0x6UL;

            // Common for profile 1 devices
            weCmdEnabled.Value = true;
            weCmd.Value = defaultWECmd;
        }

        private void ProgramSequenceDiscovery(SFDPData sfdp, bool fullDiscovery)
        {
            var defaultProgramCmd = 0x2UL;
            var defaultProgram4ByteCmd = 0x12UL;

            var defaultProgramSequence = new SequenceConfiguration(true, 1, 1, 1, 3UL, 0, defaultProgramCmd);
            var foundProgramSequence = defaultProgramSequence;

            if(fullDiscovery)
            {
                var jedecParam = sfdp.JedecParameter;
                var support4ByteParam = sfdp.Support4ByteCommandsParameter;
                var programSequencePriority = new List<SequenceConfiguration>  {
                    new SequenceConfiguration(support4ByteParam.Support1s8s8sPageProgramCommand, 1, 8, 8, 4, 0, 0x8E),
                    new SequenceConfiguration(support4ByteParam.Support1s1s8sPageProgramCommand, 1, 1, 8, 4, 0, 0x84),
                    new SequenceConfiguration(support4ByteParam.Support1s4s4sPageProgramCommand, 1, 4, 4, 4, 0, 0x3E),
                    new SequenceConfiguration(support4ByteParam.Support1s1s4sPageProgramCommand, 1, 1, 4, 4, 0, 0x34),
                    new SequenceConfiguration(support4ByteParam.Support1s1s1sPageProgramCommand, 1, 1, 1, 4, 0, defaultProgram4ByteCmd),
                };

                if(programSequencePriority.TryFirst(v => v.IsSupported, out var supportedProgramSequence))
                {
                    foundProgramSequence = supportedProgramSequence;
                }
            }
            else
            {
                foundProgramSequence.Cmd = discoveryABNUM.Value ? defaultProgram4ByteCmd : defaultProgramCmd;
                foundProgramSequence.AddressCount = discoveryABNUM.Value ? 4UL : 3UL;
            }

            programCmdIOS.Value = (ulong)Math.Log(foundProgramSequence.CmdIOS, 2);
            programDataIOS.Value = (ulong)Math.Log(foundProgramSequence.DataIOS, 2);
            programAddressIOS.Value = (ulong)Math.Log(foundProgramSequence.AddressIOS, 2);
            programAddressCount.Value = foundProgramSequence.AddressCount - 1;
            programDummyCount.Value = foundProgramSequence.DummyBytes;
            programCmd.Value = foundProgramSequence.Cmd;

            // Common for profile 1 devices
            programCmdEdge.Value = false;
            programDataEdge.Value = false;
        }

        private void ReadSequenceDiscovery(SFDPData sfdp, bool fullDiscovery)
        {
            var addressCount = discoveryABNUM.Value ? 4UL : 3UL;

            var defaultReadSequence = new SequenceConfiguration(true, 1, 1, 2, 3, 8, 0xB);
            var foundReadSequence = defaultReadSequence;
            if(fullDiscovery)
            {
                var jedecParam = sfdp.JedecParameter;
                var support4ByteParam = sfdp.Support4ByteCommandsParameter;
                switch(discoveryNumberOfLines.Value)
                {
                case 0:
                    var readSequencePriority = new List<SequenceConfiguration>
                    {
                            new SequenceConfiguration(jedecParam.SupportsRead1s8s8s(out var command188), 1, 8, 8, addressCount, 0, command188),
                            new SequenceConfiguration(jedecParam.SupportsRead1s1s8s(out var command118), 1, 1, 8, addressCount, 0, command118),
                            new SequenceConfiguration(jedecParam.SupportsRead1s4s4s(out var command144), 1, 4, 4, addressCount, 0, command144),
                            new SequenceConfiguration(jedecParam.SupportsRead1s1s4s(out var command114), 1, 1, 4, addressCount, 0, command114),
                            new SequenceConfiguration(jedecParam.SupportsRead1s2s2s(out var command122), 1, 2, 2, addressCount, 0, command122),
                            new SequenceConfiguration(jedecParam.SupportsRead1s1s2s(out var command112), 1, 1, 2, addressCount, 0, command112),
                    };

                    if(support4ByteParam != null)
                    {
                        var read4BytePriorityList = new List<SequenceConfiguration>
                        {
                                new SequenceConfiguration(support4ByteParam.Support1s8s8sFastReadCommand, 1, 8, 8, 4, 0, 0xCC),
                                new SequenceConfiguration(support4ByteParam.Support1s1s8sFastReadCommand, 1, 1, 8, 4, 0, 0x7C),
                                new SequenceConfiguration(support4ByteParam.Support1s4s4sFastReadCommand, 1, 4, 4, 4, 0, 0xEC),
                                new SequenceConfiguration(support4ByteParam.Support1s1s4sFastReadCommand, 1, 1, 4, 4, 0, 0x6C),
                                new SequenceConfiguration(support4ByteParam.Support1s2s2sFastReadCommand, 1, 2, 2, 4, 0, 0xBC),
                                new SequenceConfiguration(support4ByteParam.Support1s1s2sFastReadCommand, 1, 1, 2, 4, 0, 0x3C),
                                new SequenceConfiguration(support4ByteParam.Support1s1s1sFastReadCommand, 1, 1, 1, 4, 8, 0x0C),
                        };
                        readSequencePriority = read4BytePriorityList.Concat(readSequencePriority).ToList();
                    }
                    if(readSequencePriority.TryFirst(rs => rs.IsSupported, out var supportedReadSequence))
                    {
                        foundReadSequence = supportedReadSequence;
                    }
                    break;
                case 1:
                    break;
                case 2:
                    if(jedecParam.SupportsRead2s2s2s(out var command222, out var dummy222))
                    {
                        foundReadSequence = new SequenceConfiguration(true, 2, 2, 2, discoveryNumberOfLines.Value, dummy222, command222);
                    }
                    break;
                case 4:
                    if(jedecParam.SupportsRead4s4s4s(out var command444, out var dummy444))
                    {
                        foundReadSequence = new SequenceConfiguration(true, 4, 4, 4, discoveryNumberOfLines.Value, dummy444, command444);
                    }
                    break;
                case 8:
                    this.ErrorLog($"Fast Read 8-8-8 is not supported in Renode. Falling back to 1-1-1");
                    goto case 1;
                default:
                    this.ErrorLog($"Chosen number of lines ({discoveryNumberOfLines.Value}) during discovery is not supported. Falling back to 1-1-1");
                    break;
                }
            }
            else
            {
                switch(discoveryNumberOfLines.Value)
                {
                case 0:
                case 1:
                    byte[,] commandMap =
                        {
                            //   DMY_CNT
                            //   0     1
                            { 0x03, 0x0B }, // ABNUM = 0
                            { 0x13, 0x0C }  // ABNUM = 1
                        };
                    foundReadSequence.CmdIOS = 1;
                    foundReadSequence.AddressIOS = 1;
                    foundReadSequence.DataIOS = 1;
                    foundReadSequence.DummyBytes = (byte)(discoveryDummyCount.Value ? 8 : 0);
                    foundReadSequence.Cmd = (byte)commandMap[Convert.ToInt32(discoveryABNUM.Value), Convert.ToInt32(discoveryDummyCount.Value)];
                    break;
                case 2:
                    foundReadSequence.CmdIOS = 2;
                    foundReadSequence.AddressIOS = 2;
                    foundReadSequence.DataIOS = 2;
                    foundReadSequence.DummyBytes = (byte)(discoveryDummyCount.Value ? 10 : 8);
                    foundReadSequence.Cmd = discoveryABNUM.Value ? 0xCUL : 0xBUL;
                    break;
                case 4:
                    foundReadSequence.CmdIOS = 4;
                    foundReadSequence.AddressIOS = 4;
                    foundReadSequence.DataIOS = 4;
                    foundReadSequence.DummyBytes = (byte)(discoveryDummyCount.Value ? 10 : 8);
                    foundReadSequence.Cmd = discoveryABNUM.Value ? 0xCUL : 0xBUL;
                    break;
                case 8:
                    this.ErrorLog($"Fast Read 8-8-8 is not supported in Renode. Falling back to 1-1-1");
                    goto case 1;
                default:
                    this.ErrorLog($"Chosen number of lines ({discoveryNumberOfLines.Value}) during discovery is not supported. Falling back to 1-1-1");
                    goto case 1;
                }
                foundReadSequence.AddressCount = discoveryABNUM.Value == true ? 4UL : 3UL;
            }

            readCmdIOS.Value = (ulong)Math.Log(foundReadSequence.CmdIOS, 2);
            readAddressIOS.Value = (ulong)Math.Log(foundReadSequence.AddressIOS, 2);
            readDataIOS.Value = (ulong)Math.Log(foundReadSequence.DataIOS, 2);
            readAddressCount.Value = foundReadSequence.AddressCount - 1;
            readDummyCount.Value = foundReadSequence.DummyBytes;
            readCmd.Value = foundReadSequence.Cmd;

            // Common for profile 1 devices
            readCmdEdge.Value = false;
            readDataEdge.Value = false;
        }

        private IValueRegisterField eraseAddrCount;
        private IValueRegisterField readCmdIOS;
        private IFlagRegisterField readCmdEdge;
        private IValueRegisterField readAddressCount;
        private IValueRegisterField readAddressIOS;
        private IFlagRegisterField readAddressEdge;
        private IValueRegisterField readDataIOS;
        private IFlagRegisterField readDataEdge;
        private IFlagRegisterField fullDiscovery;
        private IValueRegisterField resultOfLastDiscovery;
        private IValueRegisterField discoveryNumberOfLines;
        private IFlagRegisterField discoveryPassed;
        private IFlagRegisterField discoveryABNUM;
        private IFlagRegisterField discoveryDummyCount;
        private IValueRegisterField discoveryBnk;
        private IValueRegisterField pageSizeProgram;
        private IValueRegisterField programDummyCount;
        private IValueRegisterField readDummyCount;
        private IValueRegisterField readCmd;
        private IValueRegisterField programCmd;
        private IFlagRegisterField weCmdEnabled;
        private IValueRegisterField programCmdIOS;
        private IFlagRegisterField programCmdEdge;
        private IValueRegisterField programAddressCount;
        private IValueRegisterField programAddressIOS;
        private IFlagRegisterField programAddressEdge;
        private IValueRegisterField programDataIOS;
        private IFlagRegisterField programDataEdge;
        private IValueRegisterField eraseAllCmd;
        private IValueRegisterField eraseAllCmdIOS;
        private IFlagRegisterField dataSwap888;
        private IFlagRegisterField resetCmd0Enabled;
        private IValueRegisterField resetCmd0;
        private IValueRegisterField resetCmd1;
        private IValueRegisterField resetCmd1Confirmation;
        private IFlagRegisterField resetCmd1ConfirmationEnabled;
        private IValueRegisterField eraseCmd;
        private IValueRegisterField sectorSize;
        private IValueRegisterField weCmd;
        private IValueRegisterField pageSizeRead;
        private IFlagRegisterField eraseAllCmdEdge;

        private struct SequenceConfiguration
        {
            public bool IsSupported;
            public ulong CmdIOS;
            public ulong AddressIOS;
            public ulong DataIOS;
            public ulong AddressCount;
            public ulong DummyBytes;
            public ulong Cmd;

            public SequenceConfiguration(bool isSupported, ulong cmdIOS, ulong addressIOS, ulong dataIOS, ulong addressCount, ulong dummyBytes, ulong cmd)
            {
                IsSupported = isSupported;
                CmdIOS = cmdIOS;
                AddressIOS = addressIOS;
                DataIOS = dataIOS;
                AddressCount = addressCount;
                DummyBytes = dummyBytes;
                Cmd = cmd;
            }
        }
    }
}
