//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_EntropyDistributionNetwork : BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_EntropyDistributionNetwork(IMachine machine, OpenTitan_CSRNG cryptoRandomGenerator) : base(machine)
        {
            this.csrng = cryptoRandomGenerator;

            DefineRegisters();
            CommandRequestDone = new GPIO();
            FatalError = new GPIO();
            RecoverableAlert = new GPIO();
            FatalAlert = new GPIO();

            reseedCommands = new Queue<uint>(ReseedCommandFifoCapacity);
            generateCommands = new Queue<uint>(GenerateCommandFifoCapacity);

            Reset();
        }

        // it is for intermodule communication between the peripheral devices and the EDN
        public uint RequestData()
        {
            if(ednEnable.Value != MultiBitBool4.True)
            {
                this.Log(LogLevel.Warning, "EDN is disabled. Returning 0.");
                return 0;
            }

            var dataLeft = csrng.RequestData(out var result);

            if(!dataLeft && bootRequestMode.Value == MultiBitBool4.True)
            {
                this.Log(LogLevel.Debug, "BOOT_REQ_MODE is on. Issueing a new request.");
                SendCsrngCommand((uint)bootGenerateCommand.Value);
            }
            
            if(!dataLeft && autoRequestModeOn)
            {
                this.Log(LogLevel.Debug, "AUTO_REQ_MODE is on. Issueing a new request.");

                if(generateCommands.TryDequeue(out var generateCommand))
                {
                    SendCsrngCommand(generateCommand);
                }
                else
                {
                    this.Log(LogLevel.Warning, "No generate commands in the generate command FIFO");
                }
                requestsBetweenReseeds.Value--;

                if(requestsBetweenReseeds.Value == 0)
                {
                    if(reseedCommands.TryDequeue(out var reseedCommand))
                    {
                        SendCsrngCommand(reseedCommand);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "No reseed commands in the reseed command FIFO");
                    }
                    requestsBetweenReseeds.Value = maxNumberOfRequestsBetweenReseeds; // reload counter
                }
            }

            return result;
        }

        public override void Reset()
        {
            base.Reset();
        }

        public long Size => 0x1000;

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out commandRequestDoneInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "edn_cmd_req_done")
                .WithFlag(1, out fatalErrorInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "edn_fatal_err")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out commandRequestDoneInterruptEnable, name: "edn_cmd_req_done")
                .WithFlag(1, out fatalErrorInterruptEnable, name: "edn_fatal_err")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) commandRequestDoneInterruptState.Value = true; }, name: "edn_cmd_req_done")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) fatalErrorInterruptState.Value = true; }, name: "edn_fatal_err")
                .WithReservedBits(2, 30)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov_alert")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_alert")
                .WithReservedBits(2, 30);

            Registers.RegisterWriteEnableForAllControls.Define(this, 0x1)
                .WithFlag(0, out allControlsRegisterWriteEnable, FieldMode.Read | FieldMode.WriteZeroToClear, name: "REGWEN")
                .WithReservedBits(1, 31);

            Registers.Control.Define(this, 0x9999)
                .WithEnumField(0, 4, out ednEnable, writeCallback: (_, val) => 
                {
                    if(!allControlsRegisterWriteEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "Register write is disabled");
                        return;
                    }

                    if(val != MultiBitBool4.True && val != MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Warning, "Invalid value for EDN_ENABLE");
                        ednEnableFieldAlert.Value = true;
                        return;
                    }
                }, name: "EDN_ENABLE")
                .WithEnumField(4, 4, out bootRequestMode, writeCallback: (_, val) => 
                {
                    if(!allControlsRegisterWriteEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "Register write is disabled");
                        return;
                    }

                    if(val != MultiBitBool4.True && val != MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Warning, "Invalid value for BOOT_REQ_MODE");
                        bootRequestModeFieldAlert.Value = true;
                        return;
                    }
                }, name: "BOOT_REQ_MODE")
                .WithEnumField(8, 4, out autoRequestMode, writeCallback: (_, val) => 
                {
                    if(!allControlsRegisterWriteEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "Register write is disabled");
                        return;
                    }

                    if(val != MultiBitBool4.True && val != MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Warning, "Invalid value for AUTO_REQ_MODE");
                        autoRequestModeFieldAlert.Value = true;
                        return;
                    }

                    if(val == MultiBitBool4.False)
                    {
                        autoRequestModeOn = false;
                    }
                }, name: "AUTO_REQ_MODE")
                .WithEnumField(12, 4, out commandFifoReset, writeCallback: (_, val) => 
                {
                    if(!allControlsRegisterWriteEnable.Value)
                    {
                        this.Log(LogLevel.Warning, "Register write is disabled");
                        return;
                    }

                    if(val != MultiBitBool4.True && val != MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Warning, "Invalid value for CMD_FIFO_RST");
                        commandFifoResetFieldAlert.Value = true;
                        return;
                    }

                    if(val == MultiBitBool4.True)
                    {
                        reseedCommands.Clear();
                        generateCommands.Clear();
                    }
                }, name: "CMD_FIFO_RST")
                .WithReservedBits(16, 16);

            Registers.BootInstantiateCommand.Define(this, 0x901)
                .WithValueField(0, 32, out bootInstantiateCommand, writeCallback: (_, val) => 
                {
                    if(bootRequestMode.Value != MultiBitBool4.True)
                    {
                        this.Log(LogLevel.Warning, "BOOT_REQ_MODE is disabled. Writing to BOOT_INST_CMD has no effect.");
                        return;
                    }

                    SendCsrngCommand((uint)val);
                }, name: "BOOT_INST_CMD");

            Registers.BootGenerateCommand.Define(this, 0xfff003)
                .WithValueField(0, 32, out bootGenerateCommand, writeCallback: (_, val) => 
                {
                    if(bootRequestMode.Value != MultiBitBool4.True)
                    {
                        this.Log(LogLevel.Warning, "BOOT_REQ_MODE is disabled. Writing to BOOT_GEN_CMD has no effect.");
                        return;
                    }

                    SendCsrngCommand((uint)val);
                }, name: "BOOT_GEN_CMD"); 

            Registers.CsrngSoftwareCommandRequest.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => 
                {
                    if(autoRequestModeOn)
                    {
                        this.Log(LogLevel.Warning, "Automatic request mode is enabled. Writing to SW_CMD_REQ has no effect.");
                        return;
                    }

                    if(autoRequestMode.Value == MultiBitBool4.True)
                    {
                        autoRequestModeOn = true;
                    }

                    SendCsrngCommand((uint)val);
                } 
                , name: "SW_CMD_REQ");

            Registers.CommandStatus.Define(this)
                .WithFlag(0, FieldMode.Read, valueProviderCallback: _ => true /* 1 - Ready to accept commands */, name: "CMD_RDY")
                .WithFlag(1, FieldMode.Read, valueProviderCallback: _ => false /* 0 - Request completed successfully*/, name: "CMD_STS")
                .WithReservedBits(2, 30);

            Registers.CsrngReseedCommand.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => 
                {
                    if(reseedCommands.Count >= ReseedCommandFifoCapacity)
                    {
                        this.Log(LogLevel.Warning, "Reseed command FIFO is full");
                        reseedCommandsError.Value = true;
                        fifoWriteError.Value = true;
                        FatalAlert.Blink();
                        fatalErrorInterruptState.Value = true;
                        UpdateInterrupts();
                        return;
                    }
                    reseedCommands.Enqueue((uint)val);
                }, name: "RESEED_CMD");

            Registers.CsrngGenerateCommand.Define(this)
                .WithValueField(0, 32, FieldMode.Write, writeCallback: (_, val) => 
                {
                    if(generateCommands.Count >= GenerateCommandFifoCapacity)
                    {
                        this.Log(LogLevel.Warning, "Generate command FIFO is full");
                        generateCommandsError.Value = true;
                        fifoWriteError.Value = true;
                        FatalAlert.Blink();
                        fatalErrorInterruptState.Value = true;
                        UpdateInterrupts();
                        return;
                    }
                    generateCommands.Enqueue((uint)val);
                }, name: "GENERATE_CMD"); 

            Registers.MaximumNumberOfRequestsBetweenReseeds.Define(this)
                .WithValueField(0, 32, out requestsBetweenReseeds, writeCallback: (_, val) => maxNumberOfRequestsBetweenReseeds = (uint)val, name: "MAX_NUM_REQS_BETWEEN_RESEEDS");

            Registers.RecoverableAlertStatus.Define(this)
                .WithFlag(0, out ednEnableFieldAlert, FieldMode.Read | FieldMode.WriteZeroToClear, name: "EDN_ENABLE_FIELD_ALERT")
                .WithFlag(1, out bootRequestModeFieldAlert, FieldMode.Read | FieldMode.WriteZeroToClear, name: "BOOT_REQ_MODE_FIELD_ALERT")
                .WithFlag(2, out autoRequestModeFieldAlert, FieldMode.Read | FieldMode.WriteZeroToClear, name: "AUTO_REQ_MODE_FIELD_ALERT")
                .WithFlag(3, out commandFifoResetFieldAlert, FieldMode.Read | FieldMode.WriteZeroToClear, name: "CMD_FIFO_RST_FIELD_ALERT")
                .WithReservedBits(4, 8)
                .WithTaggedFlag("EDN_BUS_CMP_ALERT", 12)
                .WithReservedBits(13, 19);

            Registers.ErrorCode.Define(this)
                .WithFlag(0, out reseedCommandsError, FieldMode.Read, name: "SFIFO_RESCMD_ERR")
                .WithFlag(1, out generateCommandsError, FieldMode.Read, name: "SFIFO_GENCMD_ERR")
                .WithFlag(2, out outputFifoError, FieldMode.Read, name: "SFIFO_OUTPUT_ERR")
                .WithReservedBits(3, 17)
                .WithFlag(20, out ednAckStageIllegalStateError, FieldMode.Read, name: "EDN_ACK_SM_ERR")
                .WithFlag(21, out ednMainStageIllegalStateError, FieldMode.Read, name: "EDN_MAIN_SM_ERR")
                .WithFlag(22, out hardenedCounterError, FieldMode.Read, name: "EDN_CNTR_ERR")
                .WithReservedBits(23, 5)
                .WithFlag(28, out fifoWriteError, FieldMode.Read, name: "FIFO_WRITE_ERR")
                .WithFlag(29, out fifoReadError, FieldMode.Read, name: "FIFO_READ_ERR")
                .WithFlag(30, out fifoStateError, FieldMode.Read, name: "FIFO_STATE_ERR")
                .WithReservedBits(31, 1);

            Registers.TestErrorConditions.Define(this)
                .WithValueField(0, 5, writeCallback: (_, val) => 
                {
                    // val = bit number of ErrorCode register to be set
                    switch(val)
                    {
                        case 0:
                            reseedCommandsError.Value = true;
                            break;
                        case 1:
                            generateCommandsError.Value = true;
                            break;
                        case 2:
                            outputFifoError.Value = true;
                            break;
                        case 20:
                            ednAckStageIllegalStateError.Value = true;
                            break;
                        case 21:
                            ednMainStageIllegalStateError.Value = true;
                            break;
                        case 22:
                            hardenedCounterError.Value = true;
                            break;
                        case 28:
                            fifoWriteError.Value = true;
                            break;
                        case 29:
                            fifoReadError.Value = true;
                            break;
                        case 30:    
                            fifoStateError.Value = true;
                            break;
                        default:
                            break;
                    }

                    switch (val)
                    {
                        case 0:
                        case 1:
                        case 2:
                        case 20:
                        case 21:
                        case 22:
                            fatalErrorInterruptState.Value = true;
                            UpdateInterrupts();
                            break;
                        default:
                            break;
                    }
                }, name: "ERR_CODE_TEST")
                .WithReservedBits(5, 27);

            Registers.MainStateMachineStateDebug.Define(this, (uint)StateMachine.Idle)
                .WithValueField(0, 9, FieldMode.Read, name: "MAIN_SM_STATE")
                .WithReservedBits(9, 23);
        }

        private void UpdateInterrupts()
        {
            CommandRequestDone.Set(commandRequestDoneInterruptState.Value && commandRequestDoneInterruptEnable.Value);
            FatalError.Set(fatalErrorInterruptState.Value && fatalErrorInterruptEnable.Value);
        }

        private void SendCsrngCommand(uint command)
        {
            csrng.EdnSoftwareCommandRequestWrite(command);
            commandRequestDoneInterruptState.Value = true;
            UpdateInterrupts();            
        }

        public GPIO CommandRequestDone { get; }
        public GPIO FatalError { get; }
        public GPIO RecoverableAlert { get; }
        public GPIO FatalAlert { get; }

        private IFlagRegisterField commandRequestDoneInterruptState;
        private IFlagRegisterField fatalErrorInterruptState;
        private IFlagRegisterField commandRequestDoneInterruptEnable;
        private IFlagRegisterField fatalErrorInterruptEnable;
        private IFlagRegisterField allControlsRegisterWriteEnable;
        private IEnumRegisterField<MultiBitBool4> ednEnable;
        private IEnumRegisterField<MultiBitBool4> bootRequestMode;
        private IEnumRegisterField<MultiBitBool4> autoRequestMode;
        private IEnumRegisterField<MultiBitBool4> commandFifoReset;
        private IValueRegisterField requestsBetweenReseeds;
        private IFlagRegisterField ednEnableFieldAlert;
        private IFlagRegisterField bootRequestModeFieldAlert;
        private IFlagRegisterField autoRequestModeFieldAlert;
        private IFlagRegisterField commandFifoResetFieldAlert;
        private IValueRegisterField bootInstantiateCommand;
        private IValueRegisterField bootGenerateCommand;
        private IFlagRegisterField reseedCommandsError;
        private IFlagRegisterField generateCommandsError;
        private IFlagRegisterField outputFifoError;
        private IFlagRegisterField ednAckStageIllegalStateError;
        private IFlagRegisterField ednMainStageIllegalStateError;
        private IFlagRegisterField hardenedCounterError;
        private IFlagRegisterField fifoWriteError;
        private IFlagRegisterField fifoReadError;
        private IFlagRegisterField fifoStateError;

        private readonly Queue<uint> reseedCommands;
        private readonly Queue<uint> generateCommands;
        private readonly OpenTitan_CSRNG csrng;

        private bool autoRequestModeOn;
        private uint maxNumberOfRequestsBetweenReseeds;

        private const int ReseedCommandFifoCapacity = 13;
        private const int GenerateCommandFifoCapacity = 13;

        // https://github.com/lowRISC/opentitan/blob/master/hw/ip/edn/rtl/edn_pkg.sv
        private enum StateMachine
        {
            Idle              = 0b110000101, // idle                                                                                        
            BootLoadIns       = 0b110110111, // boot: load the instantiate command                                                          
            BootLoadGen       = 0b000000011, // boot: load the generate command                                                             
            BootInsAckWait    = 0b011010010, // boot: wait for instantiate command ack                                                      
            BootCaptGenCnt    = 0b010111010, // boot: capture the gen fifo count                                                            
            BootSendGenCmd    = 0b011100100, // boot: send the generate command                                                             
            BootGenAckWait    = 0b101101100, // boot: wait for generate command ack                                                         
            BootPulse         = 0b100001010, // boot: signal a done pulse                                                                   
            BootDone          = 0b011011111, // boot: stay in done state until reset                                                        
            AutoLoadIns       = 0b001110000, // auto: load the instantiate command                                                          
            AutoFirstAckWait  = 0b001001101, // auto: wait for first instantiate command ack                                                
            AutoAckWait       = 0b101100011, // auto: wait for instantiate command ack                                                      
            AutoDispatch      = 0b110101110, // auto: determine next command to be sent                                                     
            AutoCaptGenCnt    = 0b000110101, // auto: capture the gen fifo count                                                            
            AutoSendGenCmd    = 0b111111000, // auto: send the generate command                                                             
            AutoCaptReseedCnt = 0b000100110, // auto: capture the reseed fifo count                                                         
            AutoSendReseedCmd = 0b101010110, // auto: send the reseed command                                                               
            SWPortMode        = 0b100111001, // swport: no hw request mode                                                                  
            Error             = 0b010010001  // illegal state reached and hang  
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            RegisterWriteEnableForAllControls = 0x10,
            Control = 0x14,
            BootInstantiateCommand = 0x18,
            BootGenerateCommand = 0x1c,
            CsrngSoftwareCommandRequest = 0x20,
            CommandStatus = 0x24,
            CsrngReseedCommand = 0x28,
            CsrngGenerateCommand = 0x2c,
            MaximumNumberOfRequestsBetweenReseeds = 0x30,
            RecoverableAlertStatus = 0x34,
            ErrorCode = 0x38,
            TestErrorConditions = 0x3c,
            MainStateMachineStateDebug = 0x40,
        }
    }
}
