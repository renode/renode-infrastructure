//
// Copyright (c) 2010-2025 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Core;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Antmicro.Renode.Core.CAN;
using Antmicro.Renode.Exceptions;
using System.Linq;

namespace Antmicro.Renode.Peripherals.CAN
{
    public class STMCAN : IDoubleWordPeripheral, ICAN, INumberedGPIOOutput
    {
        public STMCAN(STMCAN master = null)
        {
            // Register master CAN
            this.master = master;

            for(int i = 0; i < NumberOfRxFifos; i++)
            {
                RxFifo[i] = new Queue<CANMessage>();
                if(!IsSlave)
                {
                    FifoFiltersPrioritized[i] = new List<FilterBank>();
                }
            }

            var innerConnections = new Dictionary<int, IGPIO>();
            for(int i = 0; i < NumberOfInterruptLines; i++)
            {
                innerConnections[i] = new GPIO();
            }
            Connections = new ReadOnlyDictionary<int, IGPIO>(innerConnections);

            registers = new DeviceRegisters();

            // Fixup receive fifo regs
            registers.CAN_RFR[RxFifo0].SetRxFifo(RxFifo[RxFifo0]);
            registers.CAN_RFR[RxFifo0].UpdateInterruptLine =
                new UpdateInterruptLine(UpdateFifo0InterruptLine);

            registers.CAN_RFR[RxFifo1].SetRxFifo(RxFifo[RxFifo1]);
            registers.CAN_RFR[RxFifo1].UpdateInterruptLine =
                new UpdateInterruptLine(UpdateFifo1InterruptLine);

            if(!IsSlave)
            {
                FilterBanks = new FilterBank[NumberOfFilterBanks];
                for(int i = 0; i < NumberOfFilterBanks; i++)
                {
                    FilterBanks[i] = new FilterBank();
                }
            }
            else
            {
                if(master.FilterBanks == null)
                {
                    throw new ConstructionException("You need to construct the master first.");
                }
                if(master.IsSlave)
                {
                    throw new ConstructionException("The master of this peripheral cannot be a slave to another master.");
                }
                FilterBanks = master.FilterBanks;
                FifoFiltersPrioritized = master.FifoFiltersPrioritized;
                registers.CAN_FMR = master.registers.CAN_FMR;
                registers.CAN_FM1R = master.registers.CAN_FM1R;
                registers.CAN_FS1R = master.registers.CAN_FS1R;
                registers.CAN_FFA1R = master.registers.CAN_FFA1R;
                registers.CAN_FA1R = master.registers.CAN_FA1R;
            }

            Reset();
        }

        public event Action<int, byte[]> FrameReceived;

        public void OnFrameReceived(CANMessageFrame message)
        {
            if(registers.CAN_MCR.SleepRequest == true)
            {
                // Wake up if autowake up is on
                if(registers.CAN_MCR.AutoWakeUpMode == true)
                {
                    registers.CAN_MCR.SleepRequest = false;
                    registers.CAN_MSR.SleepAck = false;
                }
                // Signal wake up interrupt
                registers.CAN_MSR.WakeupInterrupt = true;
                UpdateSCEInterruptLine();
            }
            else if(registers.CAN_BTR.LoopbackMode == false)
            {
                CANMessage RxMsg = new CANMessage(message);
                for(int fifo = 0; fifo < NumberOfRxFifos; fifo++)
                {
                    if(FilterCANMessage(fifo, RxMsg) == true)
                    {
                        this.Log(LogLevel.Debug, "Message received RIR={0:X}", RxMsg.CAN_RIR);
                        ReceiveCANMessage(RxMsg);
                    }
                    else
                    {
                       this.Log(LogLevel.Debug, "Message dropped by filter RIR={0:X}", RxMsg.CAN_RIR);
                    }
                }
            }
            FrameReceived?.Invoke((int)CANMessage.GetRIRFromCANMessageFrame(message), message.Data);
        }

        public void WriteDoubleWord(long address, uint value)  // cpu do per
        {
            if(IsSlave && AddressIsWithinFilterRegistersArea(address))
            {
                this.Log(LogLevel.Warning, "Trying to access slave's filter registers, but the slave has to be configured via master's filters. This change has no effect.");
                return;
            }

            // Filter bank registers
            if((registerOffset)address >= registerOffset.CAN_F0R1 &&
                (registerOffset)address <= registerOffset.CAN_F27R2)
            {
                int bankIdx = AddressToFilterBankIdx(address);

                // The filter bank registers can only be written if
                // CAN_FMR.FINIT is true or the FA1R has FACtx cleared
                // (the filter is disabled)
                if(registers.CAN_FMR.FilterInitMode == true ||
                    registers.CAN_FA1R.FilterActive(bankIdx) == false)
                {
                    int regIdx = AddressToRegIdx(address);
                    //registers.CAN_FiRx[RegIdx] = value;
                    FilterBanks[bankIdx].FR[regIdx] = value;
                }
                return;
            }

            switch((registerOffset)address)
            {
                case registerOffset.CAN_MSR:
                    registers.CAN_MSR.SetValue(value);
                    UpdateSCEInterruptLine();
                    return;
                case registerOffset.CAN_MCR:
                    registers.CAN_MCR.SetValue(value);

                    if(registers.CAN_MCR.InitRequest == true &&
                       registers.CAN_MCR.SleepRequest == false)
                    {
                        // Enter initialisation mode
                        registers.CAN_MSR.InitAck = true;
                        registers.CAN_MSR.SleepAck = false;
                    }
                    else if(registers.CAN_MCR.SleepRequest == true &&
                            registers.CAN_MCR.InitRequest == false)
                    {
                        // Enter sleep mode
                        registers.CAN_MSR.SleepAck = true;
                        registers.CAN_MSR.SleepAckInterrupt = true;
                        registers.CAN_MSR.InitAck = false;
                        UpdateSCEInterruptLine();
                    }
                    else
                    {
                        // Enter normal mode
                        registers.CAN_MSR.SleepAck = false;
                        registers.CAN_MSR.InitAck = false;
                    }

                    if(registers.CAN_MCR.Reset == true)
                    {
                        registers.Reset();
                    }

                    return;

                case registerOffset.CAN_TSR:
                    registers.CAN_TSR.SetValue(value);
                    UpdateTransmitInterruptLine();
                    return;

                case registerOffset.CAN_RF0R:
                    registers.CAN_RFR[RxFifo0].SetValue(value);
                    return;

                case registerOffset.CAN_RF1R:
                    registers.CAN_RFR[RxFifo1].SetValue(value);
                    return;

                case registerOffset.CAN_IER:
                    registers.CAN_IER.SetValue(value);
                    UpdateTransmitInterruptLine();
                    registers.CAN_RFR[RxFifo0].UpdateInterruptLine();
                    registers.CAN_RFR[RxFifo1].UpdateInterruptLine();
                    UpdateSCEInterruptLine();
                    return;

                case registerOffset.CAN_ESR:
                    registers.CAN_ESR.SetValue(value);
                    UpdateSCEInterruptLine();
                    return;

                case registerOffset.CAN_BTR:
                    if(registers.CAN_MSR.InitAck == true)
                    {
                        registers.CAN_BTR.SetValue(value);
                    }
                    return;

                // Filter registers
                case registerOffset.CAN_FMR:
                    registers.CAN_FMR.SetValue(value);
                    UpdateFilterCANAssignment();
                    return;
                case registerOffset.CAN_FM1R:
                    if(registers.CAN_FMR.FilterInitMode == true)
                    {
                        registers.CAN_FM1R.SetValue(value);
                        for(int i = 0; i < NumberOfFilterBanks; i++)
                        {
                            FilterBanks[i].Mode =
                                registers.CAN_FM1R.FilterMode(i);
                        }
                    }
                    return;
                case registerOffset.CAN_FS1R:
                    if(registers.CAN_FMR.FilterInitMode == true)
                    {
                        registers.CAN_FS1R.SetValue(value);
                        for(int i = 0; i < NumberOfFilterBanks; i++)
                        {
                            FilterBanks[i].Scale =
                                registers.CAN_FS1R.FilterScale(i);
                        }
                    }
                    return;
                case registerOffset.CAN_FFA1R:
                    if(registers.CAN_FMR.FilterInitMode == true)
                    {
                        registers.CAN_FFA1R.SetValue(value);
                        for(int i = 0; i < NumberOfFilterBanks; i++)
                        {
                            FilterBanks[i].FifoAssignment =
                                registers.CAN_FFA1R.FifoAssignment(i);
                        }
                    }
                    return;
                case registerOffset.CAN_FA1R:
                    registers.CAN_FA1R.SetValue(value);
                    for(int i = 0; i < NumberOfFilterBanks; i++)
                    {
                        FilterBanks[i].Active =
                            registers.CAN_FA1R.FilterActive(i);
                    }
                    for(uint fifo = 0; fifo < NumberOfRxFifos; fifo++)
                    {
                        PrioritizeFiFoFilters(fifo);
                    }
                    return;

                // TX mailbox 0
                case registerOffset.CAN_TI0R:
                    registers.CAN_TI0R.SetValue(value);
                    if(registers.CAN_TI0R.TransmitMailboxRequest(value) == true)
                    {
                        // Transmit data
                        // registers.CAN_TDT0R = timestamp_me; FIXME
                        CANMessage TxMsg = new CANMessage(registers.CAN_TI0R.GetValue(),
                                                              registers.CAN_TDT0R,
                                                              registers.CAN_TDL0R,
                                                              registers.CAN_TDH0R);
                        TransmitData(TxMsg);

                        // Transmition done
                        registers.CAN_TSR.TxMailbox0Done();
                        UpdateTransmitInterruptLine();
                    }
                    return;
                case registerOffset.CAN_TDT0R:
                    registers.CAN_TDT0R = value;
                    return;
                case registerOffset.CAN_TDL0R:
                    registers.CAN_TDL0R = value;
                    return;
                case registerOffset.CAN_TDH0R:
                    registers.CAN_TDH0R = value;
                    return;

                // TX mailbox 1
                case registerOffset.CAN_TI1R:
                    registers.CAN_TI1R.SetValue(value);
                    if(registers.CAN_TI1R.TransmitMailboxRequest(value) == true)
                    {
                        // Transmit data
                        // registers.CAN_TDT1R = timestamp_me; FIXME
                        CANMessage TxMsg = new CANMessage(registers.CAN_TI1R.GetValue(),
                                                              registers.CAN_TDT1R,
                                                              registers.CAN_TDL1R,
                                                              registers.CAN_TDH1R);
                        TransmitData(TxMsg);

                        // Transmition done
                        registers.CAN_TSR.TxMailbox1Done();
                        UpdateTransmitInterruptLine();
                    }
                    return;
                case registerOffset.CAN_TDT1R:
                    registers.CAN_TDT1R = value;
                    return;
                case registerOffset.CAN_TDL1R:
                    registers.CAN_TDL1R = value;
                    return;
                case registerOffset.CAN_TDH1R:
                    registers.CAN_TDH1R = value;
                    return;

                // TX mailbox 2
                case registerOffset.CAN_TI2R:
                    registers.CAN_TI2R.SetValue(value);
                    if(registers.CAN_TI2R.TransmitMailboxRequest(value) == true)
                    {
                        // Transmit data
                        // registers.CAN_TDT2R = timestamp_me; FIXME
                        CANMessage TxMsg = new CANMessage(registers.CAN_TI2R.GetValue(),
                                                              registers.CAN_TDT2R,
                                                              registers.CAN_TDL2R,
                                                              registers.CAN_TDH2R);
                        TransmitData(TxMsg);

                        // Transmition done
                        registers.CAN_TSR.TxMailbox2Done();
                        UpdateTransmitInterruptLine();
                    }
                    return;
                case registerOffset.CAN_TDT2R:
                    registers.CAN_TDT2R = value;
                    return;
                case registerOffset.CAN_TDL2R:
                    registers.CAN_TDL2R = value;
                    return;
                case registerOffset.CAN_TDH2R:
                    registers.CAN_TDH2R = value;
                    return;
                default:
                    this.LogUnhandledWrite(address, value);
                    return;
            }
        }

        public void TransmitData(CANMessage msg)
        {
            if(registers.CAN_BTR.SilentMode == false)
            {
                if(FrameSent != null)
                {
                    FrameSent(msg.ToCANMessageFrame());
                }
                else
                {
                    registers.CAN_ESR.SetLECBitDominantError();
                    UpdateSCEInterruptLine();
                }
            }
            if(registers.CAN_BTR.LoopbackMode == true)
            {
                for(int fifo = 0; fifo < NumberOfRxFifos; fifo++)
                {
                    if(FilterCANMessage(fifo, msg) == true)
                    {
                        ReceiveCANMessage(msg);
                    }
                }
            }
        }

        public void PrioritizeFiFoFilters(uint Fifo)
        {
            uint FirstFilterNumber = 0;

            FifoFiltersPrioritized[Fifo].Clear();

            // Enumarate Fifo filters and add to priolist
            for(int i = 0; i < NumberOfFilterBanks; i++)
            {
                if(FilterBanks[i].FifoAssignment == Fifo)
                {
                    FilterBanks[i].FirstFilterNumber = FirstFilterNumber;
                    FirstFilterNumber += FilterBanks[i].NumberOfFiltersInBank();
                    FifoFiltersPrioritized[Fifo].Add(FilterBanks[i]);
                }
            }

            UpdateFilterCANAssignment();
            FifoFiltersPrioritized[Fifo].Sort();
        }

        public void UpdateFilterCANAssignment()
        {
            for(var i = 0; i < NumberOfFilterBanks; i++)
            {
                FilterBanks[i].BelongsToMaster = i < registers.CAN_FMR.CAN2StartBank;
            }
        }

        public bool FilterCANMessage(int RxFifo, CANMessage msg)
        {
            foreach(FilterBank filterBank in FifoFiltersPrioritized[RxFifo].Where(m => m.BelongsToMaster == !IsSlave))
            {
                if(filterBank.Active == true &&
                        filterBank.MatchMessage(msg) == true)
                {
                    return true;
                }
            }
            return false;
        }

        public void ReceiveCANMessage(CANMessage msg)
        {
            if(msg.RxFifo < NumberOfRxFifos)
            {
                bool LockedMode = registers.CAN_MCR.RxFifoLockedMode;
                registers.CAN_RFR[msg.RxFifo].ReceiveMessage(msg, LockedMode);

                if(registers.CAN_RFR[msg.RxFifo].UpdateInterruptLine != null)
                {
                    registers.CAN_RFR[msg.RxFifo].UpdateInterruptLine();
                }
            }
        }

        public void UpdateTransmitInterruptLine()
        {
            // Transmit interrupt
            if(EnableTransmitInterrupt() == true)
            {
                Connections[CAN_Tx].Set();
            }
            else
            {
                Connections[CAN_Tx].Unset();
            }
        }

        public delegate void UpdateInterruptLine();

        public void UpdateFifo0InterruptLine()
        {
            // Fifo0 interrupt
            if(EnableFifo0Interrupt() == true)
            {
                Connections[CAN_Rx0].Set();
            }
            else
            {
                Connections[CAN_Rx0].Unset();
            }
        }

        public void UpdateFifo1InterruptLine()
        {
            // Fifo1 interrupt
            if(EnableFifo1Interrupt() == true)
            {
                Connections[CAN_Rx1].Set();
            }
            else
            {
                Connections[CAN_Rx1].Unset();
            }
        }

        public void UpdateSCEInterruptLine()
        {
            // Error and status change interrupt
            if(EnableSCEInterrupt() == true)
            {
                Connections[CAN_SCE].Set();
            }
            else
            {
                Connections[CAN_SCE].Unset();
            }
        }

        public bool EnableTransmitInterrupt()
        {
            if(registers.CAN_IER.TMEInterruptEnabled == true &&
                registers.CAN_TSR.MailboxRequestCompleted() == true)
            {
                return true;
            }
            return false;
        }

        public bool EnableFifo0Interrupt()
        {
            // Check pending message
            if(registers.CAN_IER.FMP0InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo0].HasMessagesPending() == true)
            {
                return true;
            }
            // Check Fifo0 full
            if(registers.CAN_IER.FF0InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo0].FifoFull == true)
            {
                return true;
            }
            // Check Fifo0 overrun
            if(registers.CAN_IER.FOV0InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo0].FifoOverrun == true)
            {
                return true;
            }
            return false;
        }

        public bool EnableFifo1Interrupt()
        {
            // Check pending message
            if(registers.CAN_IER.FMP1InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo1].HasMessagesPending() == true)
            {
                return true;
            }
            // Check Fifo1 full
            if(registers.CAN_IER.FF1InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo1].FifoFull == true)
            {
                return true;
            }
            // Check Fifo1 overrun
            if(registers.CAN_IER.FOV1InterruptEnabled == true &&
                registers.CAN_RFR[RxFifo1].FifoOverrun == true)
            {
                return true;
            }
            return false;
        }

        public bool EnableSCEInterrupt()
        {
            // An error condition is pending
            if(registers.CAN_IER.ERRInterruptEnabled == true &&
                registers.CAN_MSR.ErrorInterrupt == true)
            {
                return true;
            }
            // Error warning interrupt
            if(registers.CAN_IER.EWGInterruptEnabled == true &&
                registers.CAN_ESR.ErrorWarningFlag == true)
            {
                return true;
            }
            // Error passive interrupt
            if(registers.CAN_IER.EPVInterruptEnabled == true &&
                registers.CAN_ESR.ErrorPassiveFlag == true)
            {
                return true;
            }
            // Error passive interrupt
            if(registers.CAN_IER.BOFInterruptEnabled == true &&
                registers.CAN_ESR.BusOffFlag == true)
            {
                return true;
            }
            // LEC Error pending
            if(registers.CAN_IER.LECInterruptEnabled == true &&
                registers.CAN_ESR.LECErrorPending() == true)
            {
                return true;
            }
            //  Sleep interrupt
            if(registers.CAN_IER.SLKInterruptEnabled == true &&
                registers.CAN_MSR.SleepAckInterrupt == true)
            {
                return true;
            }
            //  Wakup interrupt
            if(registers.CAN_IER.WKUInterruptEnabled == true &&
                registers.CAN_MSR.WakeupInterrupt == true)
            {
                return true;
            }
            return false;
        }

        public int AddressToFilterBankIdx(long address)
        {
            // Filter bank has a size of 2 * uint
            return (int)(address - (long)registerOffset.CAN_F0R1) / (2 * sizeof(uint));
        }

        public int AddressToRegIdx(long address)
        {
            return (int)((address - (long)registerOffset.CAN_F0R1) / sizeof(uint)) % 2;
        }

        public uint ReadDoubleWord(long offset)
        {
            uint Retval = 0;

            if(IsSlave && AddressIsWithinFilterRegistersArea(offset))
            {
                this.Log(LogLevel.Warning, "Trying to read slave's filter registers, but the slave uses master's filter registers - reading from master instead.");
            }

            // Filter bank registers
            if((registerOffset)offset >= registerOffset.CAN_F0R1 && (registerOffset)offset <= registerOffset.CAN_F27R2)
            {
                int bankIdx = AddressToFilterBankIdx(offset);
                int regIdx = AddressToRegIdx(offset);
                //Retval = registers.CAN_FiRx[RegIdx];
                Retval = FilterBanks[bankIdx].FR[regIdx];
            }
            else
            {
                switch((registerOffset)offset)
                {
                    case registerOffset.CAN_MCR:
                        Retval = registers.CAN_MCR.GetValue();
                        break;
                    case registerOffset.CAN_MSR:
                        Retval = registers.CAN_MSR.GetValue();
                        break;
                    case registerOffset.CAN_TSR:
                        Retval = registers.CAN_TSR.GetValue();
                        break;
                    case registerOffset.CAN_RF0R:
                        Retval = registers.CAN_RFR[RxFifo0].GetValue();
                        break;
                    case registerOffset.CAN_RF1R:
                        Retval = registers.CAN_RFR[RxFifo1].GetValue();
                        break;
                    case registerOffset.CAN_IER:
                        Retval = registers.CAN_IER.GetValue();
                        break;
                    case registerOffset.CAN_ESR:
                        Retval = registers.CAN_ESR.GetValue();
                        break;
                    case registerOffset.CAN_BTR:
                        if(registers.CAN_MSR.InitAck == true)
                        {
                            Retval = registers.CAN_BTR.GetValue();
                        }
                        break;

                    // Filter registers
                    case registerOffset.CAN_FMR:
                        Retval = registers.CAN_FMR.GetValue();
                        break;
                    case registerOffset.CAN_FM1R:
                        Retval = registers.CAN_FM1R.GetValue();
                        break;
                    case registerOffset.CAN_FS1R:
                        Retval = registers.CAN_FS1R.GetValue();
                        break;
                    case registerOffset.CAN_FFA1R:
                        Retval = registers.CAN_FFA1R.GetValue();
                        break;
                    case registerOffset.CAN_FA1R:
                        Retval = registers.CAN_FA1R.GetValue();
                        break;

                    // TX mailboxes 0
                    case registerOffset.CAN_TI0R:
                        Retval = registers.CAN_TI0R.GetValue();
                        break;
                    case registerOffset.CAN_TDT0R:
                        Retval = registers.CAN_TDT0R;
                        break;
                    case registerOffset.CAN_TDL0R:
                        Retval = registers.CAN_TDL0R;
                        break;
                    case registerOffset.CAN_TDH0R:
                        Retval = registers.CAN_TDH0R;
                        break;

                    // TX mailboxes 1
                    case registerOffset.CAN_TI1R:
                        Retval = registers.CAN_TI1R.GetValue();
                        break;
                    case registerOffset.CAN_TDT1R:
                        Retval = registers.CAN_TDT1R;
                        break;
                    case registerOffset.CAN_TDL1R:
                        Retval = registers.CAN_TDL1R;
                        break;
                    case registerOffset.CAN_TDH1R:
                        Retval = registers.CAN_TDH1R;
                        break;

                    // TX mailboxes 2
                    case registerOffset.CAN_TI2R:
                        Retval = registers.CAN_TI2R.GetValue();
                        break;
                    case registerOffset.CAN_TDT2R:
                        Retval = registers.CAN_TDT2R;
                        break;
                    case registerOffset.CAN_TDL2R:
                        Retval = registers.CAN_TDL2R;
                        break;
                    case registerOffset.CAN_TDH2R:
                        Retval = registers.CAN_TDH2R;
                        break;

                    // RX mailbox 0
                    case registerOffset.CAN_RI0R:
                        if(registers.CAN_RFR[RxFifo0].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo0].Peek().CAN_RIR;
                        }
                        break;
                    case registerOffset.CAN_RDT0R:
                        if(registers.CAN_RFR[RxFifo0].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo0].Peek().CAN_RDTR;
                        }
                        break;
                    case registerOffset.CAN_RL0R:
                        if(registers.CAN_RFR[RxFifo0].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo0].Peek().CAN_RLR;
                        }
                        break;
                    case registerOffset.CAN_RH0R:
                        if(registers.CAN_RFR[RxFifo0].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo0].Peek().CAN_RHR;
                        }
                        break;

                    // RX mailboxes 1
                    case registerOffset.CAN_RI1R:
                        if(registers.CAN_RFR[RxFifo1].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo1].Peek().CAN_RIR;
                        }
                        break;
                    case registerOffset.CAN_RDT1R:
                        if(registers.CAN_RFR[RxFifo1].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo1].Peek().CAN_RDTR;
                        }
                        break;
                    case registerOffset.CAN_RL1R:
                        if(registers.CAN_RFR[RxFifo1].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo1].Peek().CAN_RLR;
                        }
                        break;
                    case registerOffset.CAN_RH1R:
                        if(registers.CAN_RFR[RxFifo1].HasMessagesPending() == true)
                        {
                            Retval = RxFifo[RxFifo1].Peek().CAN_RHR;
                        }
                        break;

                    default:
                        this.LogUnhandledRead(offset);
                        break;
                }
            }

            return Retval;
        }

        public void Reset()
        {
            registers.Reset(!IsSlave);

            if(!IsSlave)
            {
                for(int i = 0; i < NumberOfFilterBanks; i++)
                {
                    FilterBanks[i].Active = false;
                    FilterBanks[i].Mode = FilterBankMode.FilterModeIdMask;
                    FilterBanks[i].FifoAssignment = 0;
                    FilterBanks[i].Scale = FilterBankScale.FilterScale16Bit;
                }
                UpdateFilterCANAssignment();
            }
        }

        public bool IsSlave => master != null;

        private bool AddressIsWithinFilterRegistersArea(long address)
        {
            return (registerOffset)address >= registerOffset.CAN_FMR
                && (registerOffset)address <= registerOffset.CAN_F27R2;
        }

        private enum registerOffset : uint
        {
            CAN_MCR = 0x00,
            CAN_MSR = 0x04,
            CAN_TSR = 0x08,
            CAN_RF0R = 0x0C,
            CAN_RF1R = 0x10,
            CAN_IER = 0x14,
            CAN_ESR = 0x18,
            CAN_BTR = 0x1C,

            // TX mailboxes
            CAN_TI0R = 0x180,
            CAN_TDT0R = 0x184,
            CAN_TDL0R = 0x188,
            CAN_TDH0R = 0x18C,

            CAN_TI1R = 0x190,
            CAN_TDT1R = 0x194,
            CAN_TDL1R = 0x198,
            CAN_TDH1R = 0x19C,

            CAN_TI2R = 0x1A0,
            CAN_TDT2R = 0x1A4,
            CAN_TDL2R = 0x1A8,
            CAN_TDH2R = 0x1AC,

            // RX mailboxes
            CAN_RI0R = 0x1B0,
            CAN_RDT0R = 0x1B4,
            CAN_RL0R = 0x1B8,
            CAN_RH0R = 0x1BC,

            CAN_RI1R = 0x1C0,
            CAN_RDT1R = 0x1C4,
            CAN_RL1R = 0x1C8,
            CAN_RH1R = 0x1CC,

            // Filter registers
            CAN_FMR = 0x200,
            CAN_FM1R = 0x204,
            CAN_FS1R = 0x20C,
            CAN_FFA1R = 0x214,
            CAN_FA1R = 0x21C,

            // Filter bank registers
            CAN_F0R1 = 0x240,
            CAN_F27R2 = 0x31C,
        }

        public enum FilterBankScale
        {
            FilterScale16Bit,
            FilterScale32Bit
        }

        public enum FilterBankMode
        {
            FilterModeIdMask,
            FilterIdentifierList
        }

        private class DeviceRegisters
        {
            // Constants
            public const uint NUM_FILTERBANKS = 28;
            public const uint NUM_FILTERBANK_REGS = 2 * NUM_FILTERBANKS;

            // Control registers
            public MasterControlRegister CAN_MCR = new MasterControlRegister();
            public MasterStatusRegister CAN_MSR = new MasterStatusRegister();
            public TransmitStatusRegister CAN_TSR = new TransmitStatusRegister();
            public ReceiveFifoRegister[] CAN_RFR = new ReceiveFifoRegister[NumberOfRxFifos];
            public InterruptEnableRegister CAN_IER = new InterruptEnableRegister();
            public ErrorStatusRegister CAN_ESR = new ErrorStatusRegister();
            public BitTimingRegister CAN_BTR = new BitTimingRegister();

            // Filter registers
            public FilterMasterRegister CAN_FMR = new FilterMasterRegister();
            public FilterModeRegister CAN_FM1R = new FilterModeRegister();
            public FilterScaleRegister CAN_FS1R = new FilterScaleRegister();
            public FifoAssignmentRegister CAN_FFA1R = new FifoAssignmentRegister();
            public FilterActiveRegister CAN_FA1R = new FilterActiveRegister();

            // Filter bank registers
            public uint[] CAN_FiRx = new uint[NUM_FILTERBANK_REGS];

            // TX mailbox 0
            public MailboxIdentifierRegister CAN_TI0R = new MailboxIdentifierRegister();
            public uint CAN_TDT0R;
            public uint CAN_TDL0R;
            public uint CAN_TDH0R;

            // TX mailbox 1
            public MailboxIdentifierRegister CAN_TI1R = new MailboxIdentifierRegister();
            public uint CAN_TDT1R;
            public uint CAN_TDL1R;
            public uint CAN_TDH1R;

            // TX mailbox 2
            public MailboxIdentifierRegister CAN_TI2R = new MailboxIdentifierRegister();
            public uint CAN_TDT2R;
            public uint CAN_TDL2R;
            public uint CAN_TDH2R;

            public DeviceRegisters()
            {
                for(int i = 0; i < NumberOfRxFifos; i++)
                {
                    CAN_RFR[i] = new ReceiveFifoRegister();
                }
            }

            public void Reset(bool resetFilters = true)
            {
                CAN_MCR.SetValue(0x00010002);
                CAN_MSR.SetResetValue(0x00000C02);
                CAN_TSR.SetResetValue(0x1C000000);
                CAN_RFR[RxFifo0].SetValue(0x0);
                CAN_RFR[RxFifo1].SetValue(0x0);
                CAN_IER.SetValue(0x0);
                CAN_ESR.SetResetValue(0x0);
                CAN_BTR.SetValue(0x01230000);

                if(resetFilters)
                {
                    // Filter Registers
                    CAN_FMR.SetValue(0x2A1C0E01);
                    CAN_FM1R.SetValue(0x0);
                    CAN_FS1R.SetValue(0x0);
                    CAN_FFA1R.SetValue(0x0);
                    CAN_FA1R.SetValue(0x0);
                }
            }

            public class MasterControlRegister
            {
                public const uint DBF   = (1u << 16);
                public const uint RESET = (1u << 15);
                public const uint TTCM  = (1u << 7);
                public const uint ABOM  = (1u << 6);
                public const uint AWUM  = (1u << 5);
                public const uint NART  = (1u << 4);
                public const uint RFLM  = (1u << 3);
                public const uint TXFP  = (1u << 2);
                public const uint SLEEP = (1u << 1);
                public const uint INRQ  = (1u << 0);

                public bool DebugFreeze;
                public bool Reset;
                public bool TimeTriggeredComMode;
                public bool AutoBusOffManagement;
                public bool AutoWakeUpMode;
                public bool NoAutoRetransmission;
                public bool RxFifoLockedMode;
                public bool TxFifoPriority;
                public bool SleepRequest;
                public bool InitRequest;

                public void SetValue(uint value)
                {
                    DebugFreeze          = (value & DBF)   != 0;
                    Reset                = (value & RESET) != 0;
                    TimeTriggeredComMode = (value & TTCM)  != 0;
                    AutoBusOffManagement = (value & ABOM)  != 0;
                    AutoWakeUpMode       = (value & AWUM)  != 0;
                    NoAutoRetransmission = (value & NART)  != 0;
                    RxFifoLockedMode     = (value & RFLM)  != 0;
                    TxFifoPriority       = (value & TXFP)  != 0;
                    SleepRequest         = (value & SLEEP) != 0;
                    InitRequest          = (value & INRQ)  != 0;
                }

                public uint GetValue()
                {
                    var retVal =
                        (DebugFreeze           ? DBF   : 0) |
                        (TimeTriggeredComMode  ? TTCM  : 0) |
                        (AutoBusOffManagement  ? ABOM  : 0) |
                        (AutoWakeUpMode        ? AWUM  : 0) |
                        (NoAutoRetransmission  ? NART  : 0) |
                        (RxFifoLockedMode      ? RFLM  : 0) |
                        (TxFifoPriority        ? TXFP  : 0) |
                        (SleepRequest          ? SLEEP : 0) |
                        (InitRequest ? INRQ : 0);
                    return retVal;
                }
            }

            public class MasterStatusRegister
            {
                public const uint RX    = (1u << 11);
                public const uint SAMP  = (1u << 10);
                public const uint RXM   = (1u << 9);
                public const uint TXM   = (1u << 8);
                public const uint SLAKI = (1u << 4);
                public const uint WKUI  = (1u << 3);
                public const uint ERRI  = (1u << 2);
                public const uint SLAK  = (1u << 1);
                public const uint INAK  = (1u << 0);

                public bool RxSignal;
                public bool LastSamplePoint;
                public bool RxMode;
                public bool TxMode;
                public bool SleepAckInterrupt;
                public bool WakeupInterrupt;
                public bool ErrorInterrupt;
                public bool SleepAck;
                public bool InitAck;

                public void SetResetValue(uint value)
                {
                    RxSignal          = (value & RX)    != 0;
                    LastSamplePoint   = (value & SAMP)  != 0;
                    RxMode            = (value & RXM)   != 0;
                    TxMode            = (value & TXM)   != 0;
                    SleepAckInterrupt = (value & SLAKI) != 0;
                    WakeupInterrupt   = (value & WKUI)  != 0;
                    ErrorInterrupt    = (value & ERRI)  != 0;
                    SleepAck          = (value & SLAK)  != 0;
                    InitAck           = (value & INAK)  != 0;
                }

                public void SetValue(uint value)
                {
                    if((value & SLAKI) != 0)
                    {
                        SleepAckInterrupt = false;
                    }
                    if((value & WKUI) != 0)
                    {
                        WakeupInterrupt = false;
                    }
                    if((value & ERRI) != 0)
                    {
                        ErrorInterrupt = false;
                    }
                }

                public uint GetValue()
                {
                    var retVal =
                        (RxSignal          ? RX    : 0) |
                        (LastSamplePoint   ? SAMP  : 0) |
                        (RxMode            ? RXM   : 0) |
                        (TxMode            ? TXM   : 0) |
                        (SleepAckInterrupt ? SLAKI : 0) |
                        (WakeupInterrupt   ? WKUI  : 0) |
                        (ErrorInterrupt    ? ERRI  : 0) |
                        (SleepAck          ? SLAK  : 0) |
                        (InitAck           ? INAK  : 0);
                    return retVal;
                }
            }

            public class ReceiveFifoRegister
            {
                public const uint MaxMessagesInFifo = 3;
                public const uint FULL = (1u << 3);
                public const uint FOVR = (1u << 4);
                public const uint RFOM = (1u << 5);
                public const uint FMPMASK = 0x3;

                public bool FifoFull = false;
                public bool FifoOverrun = false;
                public uint FifoMessagesPending = 0;
                private Queue<CANMessage> RxFifo;

                public UpdateInterruptLine UpdateInterruptLine;

                public void SetRxFifo(Queue<CANMessage> RxFifo)
                {
                    this.RxFifo = RxFifo;
                }

                public void SetValue(uint value)
                {
                    if((value & FULL) != 0)
                    {
                        FifoFull = false;
                    }
                    if((value & FOVR) != 0)
                    {
                        FifoOverrun = false;
                    }
                    if((value & RFOM) != 0)
                    {
                        if(RxFifo.Count > 0)
                        {
                            RxFifo.Dequeue();
                        }
                    }
                    if(UpdateInterruptLine != null)
                        UpdateInterruptLine();
                }

                public uint GetValue()
                {
                    var retVal =
                        (FifoFull    ? FULL : 0) |
                        (FifoOverrun ? FOVR : 0) |
                        ((uint)RxFifo.Count & FMPMASK);
                    return retVal;
                }

                public bool HasMessagesPending()
                {
                    return (RxFifo.Count > 0);
                }

                public bool FifoEmpty()
                {
                    return (RxFifo.Count == 0);
                }

                public void ReceiveMessage(CANMessage msg, bool RxFifoLockedMode)
                {
                    if(RxFifo.Count < 3)
                    {
                        RxFifo.Enqueue(msg);
                        if(RxFifo.Count == MaxMessagesInFifo)
                        {
                            FifoFull = true;
                        }
                    }
                    else if(RxFifoLockedMode == false)
                    {
                        // Keep 3 newest messages in queue and signal overrun
                        RxFifo.Dequeue();
                        RxFifo.Enqueue(msg);
                        FifoOverrun = true;
                    }
                    else
                    {
                        // Keep 3 oldest messages in queue and signal overrun
                        FifoOverrun = true;
                    }
                }
            }

            public class InterruptEnableRegister
            {
                public const uint TMEIE  = (1u << 0);
                public const uint FMPIE0 = (1u << 1);
                public const uint FFIE0  = (1u << 2);
                public const uint FOVIE0 = (1u << 3);
                public const uint FMPIE1 = (1u << 4);
                public const uint FFIE1  = (1u << 5);
                public const uint FOVIE1 = (1u << 6);
                public const uint EWGIE  = (1u << 8);
                public const uint EPVIE  = (1u << 9);
                public const uint BOFIE  = (1u << 10);
                public const uint LECIE  = (1u << 11);
                public const uint ERRIE  = (1u << 15);
                public const uint WKUIE  = (1u << 16);
                public const uint SLKIE  = (1u << 17);

                public bool TMEInterruptEnabled;
                public bool FMP0InterruptEnabled;
                public bool FF0InterruptEnabled;
                public bool FOV0InterruptEnabled;
                public bool FMP1InterruptEnabled;
                public bool FF1InterruptEnabled;
                public bool FOV1InterruptEnabled;
                public bool EWGInterruptEnabled;
                public bool EPVInterruptEnabled;
                public bool BOFInterruptEnabled;
                public bool LECInterruptEnabled;
                public bool ERRInterruptEnabled;
                public bool WKUInterruptEnabled;
                public bool SLKInterruptEnabled;

                public void SetValue(uint value)
                {
                    TMEInterruptEnabled  = (value & TMEIE)  != 0;
                    FMP0InterruptEnabled = (value & FMPIE0) != 0;
                    FF0InterruptEnabled  = (value & FFIE0)  != 0;
                    FOV0InterruptEnabled = (value & FOVIE0) != 0;
                    FMP1InterruptEnabled = (value & FMPIE1) != 0;
                    FF1InterruptEnabled  = (value & FFIE1)  != 0;
                    FOV1InterruptEnabled = (value & FOVIE1) != 0;
                    EWGInterruptEnabled  = (value & EWGIE)  != 0;
                    EPVInterruptEnabled  = (value & EPVIE)  != 0;
                    BOFInterruptEnabled  = (value & BOFIE)  != 0;
                    LECInterruptEnabled  = (value & LECIE)  != 0;
                    ERRInterruptEnabled  = (value & ERRIE)  != 0;
                    WKUInterruptEnabled  = (value & WKUIE)  != 0;
                    SLKInterruptEnabled  = (value & SLKIE)  != 0;
                }

                public uint GetValue()
                {
                    var retVal =
                        (TMEInterruptEnabled  ? TMEIE  : 0) |
                        (FMP0InterruptEnabled ? FMPIE0 : 0) |
                        (FF0InterruptEnabled  ? FFIE0  : 0) |
                        (FOV0InterruptEnabled ? FOVIE0 : 0) |
                        (FMP1InterruptEnabled ? FMPIE1 : 0) |
                        (FF1InterruptEnabled  ? FFIE1  : 0) |
                        (FOV1InterruptEnabled ? FOVIE1 : 0) |
                        (EWGInterruptEnabled  ? EWGIE  : 0) |
                        (EPVInterruptEnabled  ? EPVIE  : 0) |
                        (BOFInterruptEnabled  ? BOFIE  : 0) |
                        (LECInterruptEnabled  ? LECIE  : 0) |
                        (ERRInterruptEnabled  ? ERRIE  : 0) |
                        (WKUInterruptEnabled  ? WKUIE  : 0) |
                        (SLKInterruptEnabled  ? SLKIE  : 0);
                    return retVal;
                }
            }

            public class ErrorStatusRegister
            {
                public const uint EWGF = (1u << 0);
                public const uint EPVF = (1u << 1);
                public const uint BOFF = (1u << 2);

                public const int LECSHIFT = 4;
                public const uint LECMASK = 0x7;
                public const int TECSHIFT = 16;
                public const uint TECMASK = 0xFF;
                public const int RECSHIFT = 24;
                public const uint RECMASK = 0xFF;

                public const uint LECNoError = 0x0;
                public const uint LECBitDominantError = 0x5;
                public const uint LECSetBySoftware = 0x7;

                public bool ErrorWarningFlag;
                public bool ErrorPassiveFlag;
                public bool BusOffFlag;

                public uint LastErrorCode;
                public uint TransmitErrorCounter;
                public uint ReceiveErrorCounter;

                public void SetResetValue(uint value)
                {
                    ErrorWarningFlag = (value & EWGF) != 0;
                    ErrorPassiveFlag = (value & EPVF) != 0;
                    BusOffFlag = (value & BOFF) != 0;
                    LastErrorCode = (value >> LECSHIFT) & LECMASK;
                    TransmitErrorCounter = (value >> TECSHIFT) & TECMASK;
                    ReceiveErrorCounter = (value >> RECSHIFT) & RECMASK;
                }

                public void SetValue(uint value)
                {
                    LastErrorCode = (value >> LECSHIFT) & LECMASK;
                    TransmitErrorCounter = (value >> TECSHIFT) & TECMASK;
                    ReceiveErrorCounter = (value >> RECSHIFT) & RECMASK;
                }

                public uint GetValue()
                {
                    var retVal =
                        (ErrorWarningFlag ? EWGF : 0) |
                        (ErrorPassiveFlag ? EPVF : 0) |
                        (BusOffFlag      ? BOFF : 0) |
                        ((LastErrorCode & LECMASK) << LECSHIFT) |
                        ((TransmitErrorCounter & TECMASK) << TECSHIFT) |
                        ((ReceiveErrorCounter & RECMASK) << RECSHIFT);
                    return retVal;
                }

                public void SetLECBitDominantError()
                {
                    LastErrorCode = LECBitDominantError;
                }

                public bool LECErrorPending()
                {
                    return (LastErrorCode > LECNoError) && (LastErrorCode < LECSetBySoftware);
                }
            }

            public class BitTimingRegister
            {
                public const uint SILM  = (1u << 31);
                public const uint LBKM  = (1u << 30);

                public bool SilentMode;
                public bool LoopbackMode;

                public uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = value;
                    SilentMode = (value & SILM) != 0;
                    LoopbackMode = (value & LBKM) != 0;
                }

                public uint GetValue()
                {
                    return RegValue;
                }
            }

            // CAN_TSR
            public class TransmitStatusRegister
            {
                public const uint RQCP0 = (1u << 0);
                public const uint TXOK0 = (1u << 1);
                public const uint ALST0 = (1u << 2);
                public const uint TERR0 = (1u << 3);
                public const uint ABRQ0 = (1u << 7);

                public const uint RQCP1 = (1u << 8);
                public const uint TXOK1 = (1u << 9);
                public const uint ALST1 = (1u << 10);
                public const uint TERR1 = (1u << 11);
                public const uint ABRQ1 = (1u << 15);

                public const uint RQCP2 = (1u << 16);
                public const uint TXOK2 = (1u << 17);
                public const uint ALST2 = (1u << 18);
                public const uint TERR2 = (1u << 19);
                public const uint ABRQ2 = (1u << 23);

                public const uint TME0  = (1u << 26);
                public const uint TME1  = (1u << 27);
                public const uint TME2  = (1u << 28);

                private uint RegValue;

                public void SetValue(uint value)
                {
                    if((value & TERR2) != 0)
                    {
                        RegValue &= ~(TERR2);
                    }
                    if((value & ALST2) != 0)
                    {
                        RegValue &= ~(ALST2);
                    }
                    if((value & TXOK2) != 0)
                    {
                        RegValue &= ~(TXOK2);
                    }
                    if((value & TERR1) != 0)
                    {
                        RegValue &= ~(TERR1);
                    }
                    if((value & ALST1) != 0)
                    {
                        RegValue &= ~(ALST1);
                    }
                    if((value & TXOK1) != 0)
                    {
                        RegValue &= ~(TXOK1);
                    }
                    if((value & TERR0) != 0)
                    {
                        RegValue &= ~(TERR1);
                    }
                    if((value & ALST0) != 0)
                    {
                        RegValue &= ~(ALST0);
                    }
                    if((value & TXOK0) != 0)
                    {
                        RegValue &= ~(TXOK0);
                    }
                    if((value & RQCP0) != 0)
                    {
                        RegValue &= ~(TXOK0 | ALST0 | TERR0 | RQCP0);
                    }
                    if((value & RQCP1) != 0)
                    {
                        RegValue &= ~(TXOK1 | ALST1 | TERR1 | RQCP1);
                    }
                    if((value & RQCP2) != 0)
                    {
                        RegValue &= ~(TXOK2 | ALST2 | TERR2 | RQCP2);
                    }
                }

                public void SetResetValue(uint value)
                {
                    RegValue = value;
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public void TxMailbox0Done()
                {
                    RegValue |= (TME0 | TXOK0 | RQCP0);
                }

                public void TxMailbox1Done()
                {
                    RegValue |= (TME1 | TXOK1 | RQCP1);
                }

                public void TxMailbox2Done()
                {
                    RegValue |= (TME2 | TXOK2 | RQCP2);
                }

                public bool ClearTxInterrupt(uint value)
                {
                    return ((value & RQCP0) != 0);
                }

                public bool MailboxRequestCompleted()
                {
                    var retVal =
                             ((RegValue & RQCP0) != 0) ||
                             ((RegValue & RQCP1) != 0) ||
                             ((RegValue & RQCP2) != 0);
                    return retVal;
                }
            }

            public class FilterMasterRegister
            {
                public const uint FINIT = (1u << 0);

                public const int CAN2SBSHIFT = 8;
                public const uint CAN2SBMASK = 0x3F;

                public const int RESERVED1SHIFT = 14;
                public const int RESERVED2SHIFT = 1;
                public const uint RESERVED2MASK = 0x7F;
                public const uint RESERVED1MASK = 0x3FFFF;

                public bool FilterInitMode;
                public uint CAN2StartBank;
                public uint Reserved1;
                public uint Reserved2;

                public void SetValue(uint value)
                {
                    FilterInitMode = (value & FINIT) != 0;
                    CAN2StartBank = (value >> CAN2SBSHIFT) & CAN2SBMASK;
                    Reserved1 = (value >> RESERVED1SHIFT) & RESERVED1MASK;
                    Reserved2 = (value >> RESERVED2SHIFT) & RESERVED2MASK;
                }
                public uint GetValue()
                {
                    var retVal = (FilterInitMode ? FINIT : 0) |
                        ((CAN2StartBank & CAN2SBMASK) << CAN2SBSHIFT) |
                        ((Reserved1 & RESERVED1MASK) << RESERVED1SHIFT) |
                        ((Reserved2 & RESERVED2MASK) << RESERVED2SHIFT);
                    return retVal;
                }
            }

            public class FilterActiveRegister
            {
                public const uint REGMASK = 0xFFFFFFF;

                private uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = value & REGMASK;
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public bool FilterActive(int filterIdx)
                {
                    return (RegValue & (1u << filterIdx)) != 0;
                }
            }

            public class FilterModeRegister
            {
                private uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = value;
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public FilterBankMode FilterMode(int filterIdx)
                {
                    if((RegValue & (1u << filterIdx)) != 0)
                    {
                        return FilterBankMode.FilterIdentifierList;
                    }
                    else
                    {
                        return FilterBankMode.FilterModeIdMask;
                    }
                }
            }

            public class FilterScaleRegister
            {
                private uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = value;
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public FilterBankScale FilterScale(int filterIdx)
                {
                    if((RegValue & (1u << filterIdx)) != 0)
                    {
                        return FilterBankScale.FilterScale32Bit;
                    }
                    else
                    {
                        return FilterBankScale.FilterScale16Bit;
                    }
                }
            }

            public class FifoAssignmentRegister
            {
                private uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = value;
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public uint FifoAssignment(int filterIdx)
                {
                    if((RegValue & (1u << filterIdx)) != 0)
                    {
                        return 1;
                    }
                    else
                    {
                        return 0;
                    }
                }
            }

            public class MailboxIdentifierRegister
            {

                public const uint TXRQ = (1u << 0);
                private uint RegValue;

                public void SetValue(uint value)
                {
                    RegValue = (value & 0xFFFFFFFE);
                }

                public uint GetValue()
                {
                    return RegValue;
                }

                public bool TransmitMailboxRequest(uint value)
                {
                    return (value & TXRQ) != 0;
                }
            }
        }

        public class CANMessage
        {
            public static uint GetRIRFromCANMessageFrame(CANMessageFrame message)
            {
                var rir = message.ExtendedId << IDSHIFT;
                BitHelper.SetBit(ref rir, IDESHIFT, message.ExtendedFormat);
                BitHelper.SetBit(ref rir, RTRSHIFT, message.RemoteFrame);
                return rir;
            }

            public const int IDSHIFT = 3;
            public const int IDWIDTH = 29;
            public const int STIDSHIFT = IDSHIFT + EXIDWIDTH;
            public const byte STIDWIDTH = 11;
            public const uint STIDMASK = (1u << STIDWIDTH) - 1;
            public const int EXIDSHIFT = IDSHIFT;
            public const byte EXIDWIDTH = IDWIDTH - STIDWIDTH;
            public const uint EXIDMASK = (1u << EXIDWIDTH) - 1;
            public const int IDESHIFT = 2;
            public const uint IDEMASK = 0x1;
            public const int RTRSHIFT = 1;
            public const uint RTRMASK = 0x1;

            public const int TIMESHIFT = 16;
            public const uint TIMEMASK = 0xFFFF;
            public const int FMISHIFT = 8;
            public const uint FMIMASK = 0xFF;
            public const int DLCSHIFT = 0;
            public const uint DLCMASK = 0xF;

            public const uint RIRMASK =
                (STIDMASK << STIDSHIFT) |
                (EXIDMASK << EXIDSHIFT) |
                (IDEMASK  << IDESHIFT)  |
                (RTRMASK  << RTRSHIFT);
            public const uint RDTRMASK = (DLCMASK << DLCSHIFT);

            public uint CAN_RIR;
            public uint CAN_RDTR;
            public uint CAN_RLR;
            public uint CAN_RHR;

            public uint STID;
            public uint RTR;
            public uint IDE;
            public uint EXID;
            public uint DLC;
            public byte[] Data;
            public uint TimeStamp;

            public uint FilterMatchIndex;
            public uint RxFifo;

            public CANMessage(uint RIR, uint RDTR, uint RLR, uint RHR)
            {
                CAN_RIR = RIR & RIRMASK;
                CAN_RDTR = RDTR & RDTRMASK;
                CAN_RLR = RLR;
                CAN_RHR = RHR;

                // Not needed at Tx
                //TimeStamp = (RDTR >> TIMESHIFT) & TIMEMASK;

                ExtractRegisters();
            }

            public CANMessage(uint STID, uint EXID, uint RTR,
                    uint IDE, uint DLC, byte[] Data, uint TimeStamp)
            {
                this.STID = STID;
                this.EXID = EXID;
                this.RTR = RTR;
                this.IDE = IDE;
                this.DLC = DLC;
                this.Data = Data;
                this.TimeStamp = TimeStamp;

                GenerateRegisters();
            }

            public CANMessage(CANMessageFrame message)
            {
                CAN_RIR = GetRIRFromCANMessageFrame(message);
                ExtractRIRRegister();

                //this.TimeStamp = (Data[6] << 8) | Data[7];
                this.DLC = (uint)message.Data.Length;
                GenerateRDTRRegister();

                this.Data = message.Data;
                GenerateDataRegisters();
            }

            public CANMessageFrame ToCANMessageFrame()
            {
                return CANMessageFrame.CreateWithExtendedId(
                    id: BitHelper.GetValue(CAN_RIR, offset: IDSHIFT, size: IDWIDTH),
                    data: Data,
                    extendedFormat: BitHelper.IsBitSet(CAN_RIR, IDESHIFT),
                    remoteFrame: BitHelper.IsBitSet(CAN_RIR, RTRSHIFT)
                );
            }

            public void GenerateRegisters()
            {
                CAN_RIR =
                    ((STID & STIDMASK) << STIDSHIFT) |
                    ((EXID & EXIDMASK) << EXIDSHIFT) |
                    ((IDE  & IDEMASK)  << IDESHIFT)  |
                    ((RTR  & RTRMASK)  << RTRSHIFT);

                GenerateRDTRRegister();
                GenerateDataRegisters();
            }

            public void GenerateRDTRRegister()
            {
                CAN_RDTR =
                    ((TimeStamp & TIMEMASK) << TIMESHIFT) |
                    ((FilterMatchIndex & FMIMASK) << FMISHIFT) |
                    ((DLC & DLCMASK) << DLCSHIFT);
            }

            public void GenerateDataRegisters()
            {
                var l = Data.Length;
                CAN_RLR = 0U;
                CAN_RLR |= (uint)((l > 0) ? Data[0] << 0  : 0);
                CAN_RLR |= (uint)((l > 1) ? Data[1] << 8  : 0);
                CAN_RLR |= (uint)((l > 2) ? Data[2] << 16 : 0);
                CAN_RLR |= (uint)((l > 3) ? Data[3] << 24 : 0);
                CAN_RHR = 0U;
                CAN_RHR |= (uint)((l > 4) ? Data[4] << 0  : 0);
                CAN_RHR |= (uint)((l > 5) ? Data[5] << 8  : 0);
                CAN_RHR |= (uint)((l > 6) ? Data[6] << 16 : 0);
                CAN_RHR |= (uint)((l > 7) ? Data[7] << 24 : 0);
            }

            public void ExtractRegisters()
            {
                ExtractRIRRegister();
                ExtractRDTRRegister();
                ExtractDataRegisters();
            }

            public void ExtractRIRRegister()
            {
                STID = (CAN_RIR >> STIDSHIFT) & STIDMASK;
                EXID = (CAN_RIR >> EXIDSHIFT) & EXIDMASK;
                IDE  = (CAN_RIR >> IDESHIFT) & IDEMASK;
                RTR  = (CAN_RIR >> RTRSHIFT) & RTRMASK;
            }

            public void ExtractRDTRRegister()
            {
                TimeStamp = (CAN_RDTR >> TIMESHIFT) & TIMEMASK;
                DLC = (CAN_RDTR >> DLCSHIFT) & DLCMASK;
            }

            public void ExtractDataRegisters()
            {
                if(DLC > 0)
                {
                    Data = new byte[DLC];

                    // Extract CAN_RLR
                    for(int i = 0; i < DLC && i < 4; i++)
                    {
                        Data[i] = (byte)((CAN_RLR >> (i * 8)) & 0xFF);
                    }

                    // Extract CAN_RHR
                    for(int i = 0; (i + 4) < DLC && i < 4; i++)
                    {
                        Data[i + 4] = (byte)((CAN_RHR >> (i * 8)) & 0xFF);
                    }
                }
            }
        }

        public class Filter
        {
            public uint STID;
            public uint RTR;
            public uint IDE;
            public uint EXID;
            public uint EXIDMask;
        }

        public class FilterBank : IComparable<FilterBank>
        {
            public const int STIDSHIFT = 21;
            public const int STIDSHIFT2 = 5;
            public const uint STIDMASK = 0x7FF;
            public const uint STIDMASK2 = 0x7;
            public const int EXIDSHIFT = 3;
            public const int EXIDSHIFT2 = 16;
            public const int EXIDSHIFT3 = 0;
            public const int EXIDSHIFT4 = 15;
            public const uint EXIDMASK = 0x3FFFF;
            public const uint EXIDMASK2 = 0x7;
            public const int IDESHIFT = 2;
            public const int IDESHIFT2 = 20;
            public const int IDESHIFT3 = 4;
            public const uint IDEMASK = 0x1;
            public const int RTRSHIFT = 1;
            public const int RTRSHIFT2 = 19;
            public const int RTRSHIFT3 = 3;
            public const uint RTRMASK = 0x1;

            public bool Active;
            public FilterBankMode Mode;
            public FilterBankScale Scale;
            public uint FifoAssignment;
            public uint[] FR = new uint[2];
            public uint FirstFilterNumber;
            public bool BelongsToMaster;

            public FilterBank()
            {
                Active = false;
                Mode = FilterBankMode.FilterModeIdMask;
                FifoAssignment = 0;
                Scale = FilterBankScale.FilterScale16Bit;
                BelongsToMaster = true;
            }

            public bool MatchMessage(CANMessage msg)
            {
                Filter[] filters = ExtractFilters();
                int NumOfFilters = (Scale == FilterBankScale.FilterScale32Bit) ? 2 : 4;
                uint FilterNumber = FirstFilterNumber;

                if(Mode == FilterBankMode.FilterModeIdMask)
                {
                    for(int i = 0; i < NumOfFilters / 2; i += 2, FilterNumber++)
                    {
                        int mask = i + 1;
                        if(
                                ((filters[i].STID & filters[mask].STID) == (msg.STID & filters[mask].STID)) &&
                                ((filters[i].EXID & filters[mask].EXID & filters[mask].EXIDMask) ==
                                 (msg.EXID & filters[mask].EXID & filters[mask].EXIDMask)) &&
                                ((filters[i].IDE & filters[mask].IDE) == (msg.IDE & filters[mask].IDE)) &&
                                ((filters[i].RTR & filters[mask].RTR) == (msg.RTR & filters[mask].RTR)) &&
                                ((filters[i].STID & filters[mask].STID) == (msg.STID & filters[mask].STID))
                            )
                        {
                            msg.FilterMatchIndex = FilterNumber;
                            msg.RxFifo = FifoAssignment;
                            return true;
                        }
                    }
                }
                else
                {
                    for(int i = 0; i < NumOfFilters; i++, FilterNumber++)
                    {
                        if(
                                (filters[i].STID == msg.STID) &&
                                ((filters[i].EXID & filters[i].EXIDMask) == (msg.EXID & filters[i].EXIDMask)) &&
                                (filters[i].IDE == msg.IDE) &&
                                (filters[i].RTR == msg.RTR)
                           )
                        {
                            msg.FilterMatchIndex = FilterNumber;
                            msg.RxFifo = FifoAssignment;
                            return true;
                        }
                    }
                }
                return false;
            }

            public Filter[] ExtractFilters()
            {
                Filter[] filters;

                if(Scale == FilterBankScale.FilterScale32Bit)
                {
                    filters = new Filter[2];
                    for(int i = 0; i < 2; i++)
                    {
                        filters[i] = new Filter();
                        filters[i].STID = (FR[i] >> STIDSHIFT) & STIDMASK;
                        filters[i].EXID = (FR[i] >> EXIDSHIFT) & EXIDMASK;
                        filters[i].IDE  = (FR[i] >> IDESHIFT)  & IDEMASK;
                        filters[i].RTR  = (FR[i] >> RTRSHIFT)  & RTRMASK;
                        filters[i].EXIDMask = 0x3FFF;
                    }
                }
                else
                {
                    filters = new Filter[4];
                    for(int i = 0; i < 4; i++)
                    {
                        filters[i] = new Filter();
                    }
                    filters[0].STID = (FR[0] >> STIDSHIFT) & STIDMASK;
                    filters[0].IDE  = (FR[0] >> IDESHIFT2) & IDEMASK;
                    filters[0].RTR  = (FR[0] >> RTRSHIFT2) & RTRMASK;
                    filters[0].EXID = ((FR[0] >> EXIDSHIFT2) & EXIDMASK2) << EXIDSHIFT4;
                    filters[0].EXIDMask = EXIDMASK2 << EXIDSHIFT4;
                    filters[1].STID = (FR[0] >> STIDSHIFT2) & STIDMASK;
                    filters[1].IDE  = (FR[0] >> IDESHIFT3) & IDEMASK;
                    filters[1].RTR  = (FR[0] >> RTRSHIFT3) & RTRMASK;
                    filters[1].EXID = ((FR[0] >> EXIDSHIFT3) & EXIDMASK2) << EXIDSHIFT4;
                    filters[1].EXIDMask = EXIDMASK2 << EXIDSHIFT4;
                    filters[2].STID = (FR[1] >> STIDSHIFT) & STIDMASK;
                    filters[2].IDE  = (FR[1] >> IDESHIFT2) & 0x1;
                    filters[2].RTR  = (FR[1] >> RTRSHIFT2) & 0x1;
                    filters[2].EXID = ((FR[1] >> EXIDSHIFT2) & EXIDMASK2) << EXIDSHIFT4;
                    filters[2].EXIDMask = EXIDMASK2 << EXIDSHIFT4;
                    filters[3].STID = (FR[1] >> STIDSHIFT2) & STIDMASK;
                    filters[3].IDE  = (FR[1] >> IDESHIFT3) & IDEMASK;
                    filters[3].RTR  = (FR[1] >> RTRSHIFT3) & RTRMASK;
                    filters[3].EXID = ((FR[1] >> EXIDSHIFT3) & EXIDMASK2) << EXIDSHIFT4;
                    filters[3].EXIDMask = EXIDMASK2 << EXIDSHIFT4;
                }

                return filters;
            }

            public int CompareTo(FilterBank filterBank)
            {
                if(filterBank == null)
                    return 1;

                // 32BitScale higher priority than 16BitScale
                if(Scale > filterBank.Scale)
                    return 1;

                // IdentifierList higher priority than IdMask
                if(Mode > filterBank.Mode)
                    return 1;

                // Lower FilterNumber has higher priority
                if(FirstFilterNumber < filterBank.FirstFilterNumber)
                    return 1;

                // Lower priority on this than filterBank
                return -1;
            }

            public uint NumberOfFiltersInBank()
            {
                uint NumFiltersInBank = 4;
                if(Scale == FilterBankScale.FilterScale32Bit)
                {
                    NumFiltersInBank = 2;
                }
                if(Mode == FilterBankMode.FilterModeIdMask)
                {
                    NumFiltersInBank /= 2;
                }
                return NumFiltersInBank;
            }
        }

        private readonly STMCAN master;

        private const int NumberOfInterruptLines = 4;
        private const int CAN_Tx = 0;
        private const int CAN_Rx0 = 1;
        private const int CAN_Rx1 = 2;
        private const int CAN_SCE = 3;

        private const int NumberOfRxFifos = 2;
        private const int RxFifo0 = 0;
        private const int RxFifo1 = 1;
        private const int NumberOfFilterBanks = 28;

        public event Action<CANMessageFrame> FrameSent;
        private DeviceRegisters registers;
        public IReadOnlyDictionary<int, IGPIO> Connections { get; private set; }
        private Queue<CANMessage>[] RxFifo = new Queue<CANMessage>[NumberOfRxFifos];
        public FilterBank[] FilterBanks;
        public List<FilterBank>[] FifoFiltersPrioritized = new List<FilterBank>[NumberOfRxFifos];
    }
}
