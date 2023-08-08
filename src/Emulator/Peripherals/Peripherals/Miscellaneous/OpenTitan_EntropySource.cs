//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;
using System.Collections.Generic;
using Org.BouncyCastle.Crypto.Digests;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.Miscellaneous
{
    public class OpenTitan_EntropySource: BasicDoubleWordPeripheral, IKnownSize
    {
        public OpenTitan_EntropySource(IMachine machine): base(machine)
        {
            entropySource = EmulationManager.Instance.CurrentEmulation.RandomGenerator;
            sha3Conditioner = new Sha3Conditioner();
            
            observeFifo = new Queue<uint>(ObserveFifoDepth);
            preConditionerPackerFifo = new Queue<uint>(PreConditionerPackerFifoDepth);
            preEsfinalPackerFifo = new Queue<uint>(PreEsfinalPackerFifoDepth);
            esfinalFifo = new Queue<uint>(EsfinalFifoDepth);
            entropyUnpackerFifo = new Queue<uint>(EntropyUnpackerFifoDepth);
            hardwareOutputFifo = new Queue<uint>(EntropyUnpackerFifoDepth);

            RecoverableAlert = new GPIO();
            FatalAlert = new GPIO();

            EsEntropyValidIRQ = new GPIO();
            EsHealthTestFailedIRQ = new GPIO();
            EsObserveFifoReadyIRQ = new GPIO();
            EsFatalErrIRQ = new GPIO();
            
            DefineRegisters();
            Reset();
        }

        // it is for intermodule communication between the entropy source and the CSRNG
        public byte[] RequestEntropySourceData()
        {
            var data = new List<byte>();
            if(PathSelect == Route.HardwareInterface)
            {
                this.Log(LogLevel.Debug, "Requesting entropy data from the hardware interface");
                if(hardwareOutputFifo.Count == 0)
                {
                    FillBuffers();
                }
                while(hardwareOutputFifo.Count > 0)
                {
                    data.AddRange(BitConverter.GetBytes(hardwareOutputFifo.Dequeue()));
                }
            }

            return data.ToArray();
        }

        public override void Reset()
        {
            base.Reset();
            ResetBuffers();
            sha3Conditioner.Reset();
        }

        public long Size => 0x100;

        private void FillBuffers()
        {
            // In the real device, the buffers are filled with entropy data from the PTRNG source.
            // In the emulation, we fill the buffers with random data on demand.
            for(var i = 0; i < ObserveFifoDepth; i++)
            {
                GenerateEntropyData();
            }
        }

        private void GenerateEntropyData()
        {
            var value = (uint)entropySource.Next();

            RunHealthTests(value);
            ObserveDataPath(value);
            InsertIntoEntropyFlow(value, Mode.Normal);
        }

        private void RunHealthTests(uint value)
        {
            // It is a mock implementation: no health tests are currently performed.
            // TODO: Implement the following tests: Adaptive Proportion, Repetition Count, Bucket, Markov, Mailbox.
            mainMachineState.Value = StateMachine.ContHTRunning;
        }

        private void ObserveDataPath(uint value)
        {
            if(ModeOverride != Mode.FirmwareOverride)
            {
                return;
            }

            if(observeFifo.Count == ObserveFifoDepth)
            {
                this.Log(LogLevel.Warning, "Observe FIFO is full");
                observeFifoOverflowStatus.Value = true;
                // We don't update interrupts here because it doesn't generate interrupt directly.
            }
            else
            {
                observeFifo.Enqueue(value);
            }

            if(observeFifo.Count >= (int)observeFifoThreshold.Value)
            {
                observeFifoReadyInterruptState.Value = true;
                UpdateInterrupts();
            }
        }

        private void InsertIntoEntropyFlow(uint value, Mode mode)
        {
            if(mode != ModeSelect)
            {
                return;
            }

            switch(BypassModeSelect)
            {
                case BypassMode.Sha3ConditionedEntropy:
                    preConditionerPackerFifo.Enqueue(value);
                    if(preConditionerPackerFifo.Count == PreConditionerPackerFifoDepth)
                    {
                        var data = preConditionerPackerFifo.DequeueAll();
                        sha3Conditioner.AddData(data);
                    }
                    break;
                case BypassMode.RawEntropy:
                    if(preEsfinalPackerFifo.Count < PreEsfinalPackerFifoDepth)
                    {
                        preEsfinalPackerFifo.Enqueue(value);
                    }
                    else
                    {
                        this.Log(LogLevel.Warning, "Dropping entropy data due to full FIFO");
                    }
                    break;
            }
            
            OutputPath();
        }

        private void OutputPath()
        {
            switch(BypassModeSelect)
            {
                case BypassMode.Sha3ConditionedEntropy:
                    if(sha3Conditioner.HasConditionedData && esfinalFifo.Count <= EsfinalFifoDepth - sha3Conditioner.Sha3OutputLengthBits / 32)
                    {
                        var conditionedData = sha3Conditioner.GetConditionedData();
                        esfinalFifo.EnqueueRange(conditionedData);
                    }
                    break;
                case BypassMode.RawEntropy:
                    if(preEsfinalPackerFifo.Count == PreEsfinalPackerFifoDepth && esfinalFifo.Count <= EsfinalFifoDepth - PreEsfinalPackerFifoDepth)
                    {
                        esfinalFifo.EnqueueRange(preEsfinalPackerFifo.DequeueAll());
                    }
                    break;
            }

            switch(PathSelect)
            {
                case Route.HardwareInterface:
                    HardwareInterfacePath();
                    break;
                case Route.FirmwareInterface:
                    FirmwareInterfacePath();
                    break;
            }
        }

        private void HardwareInterfacePath()
        {
            if(hardwareOutputFifo.Count == 0 && esfinalFifo.Count >= EntropyUnpackerFifoDepth)
            {
                var entropyData = esfinalFifo.DequeueRange(EntropyUnpackerFifoDepth);
                hardwareOutputFifo.EnqueueRange(entropyData);
            }
        }

        private void FirmwareInterfacePath()
        {
            if(entropyUnpackerFifo.Count == 0 && esfinalFifo.Count >= EntropyUnpackerFifoDepth)
            {
                entropyUnpackerFifo.EnqueueRange(esfinalFifo.DequeueRange(EntropyUnpackerFifoDepth));
            }

            if(entropyUnpackerFifo.Count > 0)
            {
                entropyValidInterruptState.Value = true;
                UpdateInterrupts();
            }
        }

        private void DefineRegisters()
        {
            Registers.InterruptState.Define(this)
                .WithFlag(0, out entropyValidInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "es_entropy_valid", writeCallback: (_, val) => 
                {
                    if(val && entropyUnpackerFifo.Count > 0)
                    {
                        // if there is still entropy available, the interrupt state is set again. 
                        entropyValidInterruptState.Value = true;
                    }
                 })
                .WithFlag(1, out healthTestFailedInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "es_health_test_failed")
                .WithFlag(2, out observeFifoReadyInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "es_observe_fifo_ready", writeCallback: (_, val) => 
                {
                    if(val && observeFifo.Count >= (int)observeFifoThreshold.Value)
                    {
                        observeFifoReadyInterruptState.Value = true;
                    }
                 })
                .WithFlag(3, out fatalErrorInterruptState, FieldMode.Read | FieldMode.WriteOneToClear, name: "es_fatal_err")
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptEnable.Define(this)
                .WithFlag(0, out entropyValidInterruptEnable, name: "es_entropy_valid")
                .WithFlag(1, out healthTestFailedInterruptEnable, name: "es_health_test_failed")
                .WithFlag(2, out observeFifoReadyInterruptEnable, name: "es_observe_fifo_ready")
                .WithFlag(3, out fatalErrorInterruptEnable, name: "es_fatal_err")
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.InterruptTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) entropyValidInterruptState.Value = true;     }, name: "es_entropy_valid")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) healthTestFailedInterruptState.Value = true; }, name: "es_health_test_failed")
                .WithFlag(2, FieldMode.Write, writeCallback: (_, val) => { if(val) observeFifoReadyInterruptState.Value = true; }, name: "es_observe_fifo_ready")
                .WithFlag(3, FieldMode.Write, writeCallback: (_, val) => { if(val) fatalErrorInterruptState.Value = true;       }, name: "es_fatal_err")
                .WithReservedBits(4, 28)
                .WithWriteCallback((_, __) => UpdateInterrupts());

            Registers.AlertTest.Define(this)
                .WithFlag(0, FieldMode.Write, writeCallback: (_, val) => { if(val) RecoverableAlert.Blink(); }, name: "recov_alert")
                .WithFlag(1, FieldMode.Write, writeCallback: (_, val) => { if(val) FatalAlert.Blink(); }, name: "fatal_alert")
                .WithReservedBits(2, 30);

            // Some registers are protected from writes by the means of write enable registers.
            // It is a security countermeasure that adds checks before writing to the registers.
            // Sometimes the write enable register is not taken into account in the model and marked as Tag, 
            // because it is not needed for the functional simulation.
            Registers.RegisterWriteEnableForModuleEnable.Define(this, 0x1)
                .WithFlag(0, out moduleEnableRegisterWriteEnable, FieldMode.Read | FieldMode.WriteZeroToClear, name: "ME_REGWEN")
                .WithReservedBits(1, 31);

            Registers.RegisterWriteEnableForControlAndThresholds.Define(this, 0x1)
                .WithTaggedFlag("SW_REGUPD", 0)
                .WithReservedBits(1, 31);

            Registers.RegisterWriteEnableForAllControls.Define(this, 0x1)
                .WithTaggedFlag("REGWEN", 0)
                .WithReservedBits(1, 31);

            Registers.Revision.Define(this, 0x10303)
                .WithTag("ABI_REVISION", 0, 8)
                .WithTag("HW_REVISION", 8, 8)
                .WithTag("CHIP_TYPE", 16, 8)
                .WithReservedBits(24, 8);

            Registers.ModuleEnable.Define(this, 0x9)
                .WithEnumField(0, 4, out moduleEnable, name: "MODULE_ENABLE", writeCallback: (_, val) =>
                {
                    if(moduleEnableRegisterWriteEnable.Value == false)
                    {
                        return;
                    }

                    if(val == MultiBitBool4.True)
                    {
                        this.Log(LogLevel.Noisy, "Enabling module");
                        FillBuffers();
                    }
                    else
                    {
                        this.Log(LogLevel.Noisy, "Disabling module");
                        Reset();
                    }
                })
                .WithReservedBits(4, 28);

            Registers.Configuration.Define(this, 0x909099)
                .WithTag("FIPS_ENABLE", 0, 4)
                .WithEnumField(4, 4, out entropyDataRegisterEnable, name: "ENTROPY_DATA_REG_ENABLE")
                .WithTag("THRESHOLD_SCOPE", 12, 4)
                .WithTag("RNG_BIT_ENABLE", 20, 4)
                .WithTag("RNG_BIT_SEL", 24, 2)
                .WithReservedBits(26, 6);

            Registers.EntropyControl.Define(this, 0x99)
                .WithEnumField(0, 4, out routeSelect, name: "ES_ROUTE")
                .WithEnumField(4, 4, out bypassModeSelect, name: "ES_TYPE")
                .WithReservedBits(8, 24);

            Registers.EntropyDataBits.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "ENTROPY_DATA", valueProviderCallback: (_) =>
                {
                    if(entropyDataRegisterEnable.Value != MultiBitBool4.True)
                    {
                        this.Log(LogLevel.Warning, "Entropy data register is disabled");
                        return 0;
                    }

                    if(entropyUnpackerFifo.Count == 0)
                    {
                        FillBuffers();
                        this.Log(LogLevel.Debug, "Entropy generated on demand");
                    }

                    if(!entropyUnpackerFifo.TryDequeue(out var value))
                    {
                        this.Log(LogLevel.Warning, "No entropy data available");
                    }

                    return value;
                });

            // The randomness tests registers

            Registers.HealthTestWindows.Define(this, 0x600200)
                .WithTag("FIPS_WINDOW", 0, 16)
                .WithTag("BYPASS_WINDOW", 16, 16);

            Registers.RepetitionCountTestThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.RepetitionCountSymbolTestThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.AdaptiveProportionTestHighThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.AdaptiveProportionTestLowThresholds.Define(this)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.BucketTestThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.MarkovTestHighThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.MarkovTestLowThresholds.Define(this)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.ExternalHealthTestHighThresholds.Define(this, 0xffffffff)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.ExternalHealthTestLowThresholds.Define(this)
                .WithTag("FIPS_THRESH", 0, 16)
                .WithTag("BYPASS_THRESH", 16, 16);

            Registers.RepetitionCountTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.RepetitionCountSymbolTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.AdaptiveProportionTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.AdaptiveProportionTestLowWatermarks.Define(this, 0xffffffff)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.ExternalHealthTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.ExternalHealthTestLowWatermarks.Define(this, 0xffffffff)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.BucketTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.MarkovTestHighWatermarks.Define(this)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.MarkovTestLowWatermarks.Define(this, 0xffffffff)
                .WithTag("FIPS_WATERMARK", 0, 16)
                .WithTag("BYPASS_WATERMARK", 16, 16);

            Registers.RepetitionCountTestFailureCounter.Define(this)
                .WithTag("REPCNT_TOTAL_FAILS", 0, 32);

            Registers.RepetitionCountSymbolTestFailureCounter.Define(this)
                .WithTag("REPCNTS_TOTAL_FAILS", 0, 32);

            Registers.AdaptiveProportionHighTestFailureCounter.Define(this)
                .WithTag("ADAPTP_HI_TOTAL_FAILS", 0, 32);

            Registers.AdaptiveProportionLowTestFailureCounter.Define(this)
                .WithTag("ADAPTP_LO_TOTAL_FAILS", 0, 32);

            Registers.BucketTestFailureCounter.Define(this)
                .WithTag("BUCKET_TOTAL_FAILS", 0, 32);

            Registers.MarkovHighTestFailureCounter.Define(this)
                .WithTag("MARKOV_HI_TOTAL_FAILS", 0, 32);

            Registers.MarkovLowTestFailureCounter.Define(this)
                .WithTag("MARKOV_LO_TOTAL_FAILS", 0, 32);

            Registers.ExternalHealthTestHighThresholdFailureCounter.Define(this)
                .WithTag("EXTHT_HI_TOTAL_FAILS", 0, 32);

            Registers.ExternalHealthTestLowThresholdFailureCounter.Define(this)
                .WithTag("EXTHT_LO_TOTAL_FAILS", 0, 32);

            Registers.AlertThreshold.Define(this, 0xfffd0002)
                .WithTag("ALERT_THRESHOLD", 0, 16)
                .WithTag("ALERT_THRESHOLD_INV", 16, 16);

            Registers.AlertSummaryFailureCounts.Define(this)
                .WithTag("ANY_FAIL_COUNT", 0, 16)
                .WithReservedBits(16, 16);

            Registers.AlertFailureCounts.Define(this)
                .WithTag("REPCNT_FAIL_COUNT", 4, 4)
                .WithTag("ADAPTP_HI_FAIL_COUNT", 8, 4)
                .WithTag("ADAPTP_LO_FAIL_COUNT", 12, 4)
                .WithTag("BUCKET_FAIL_COUNT", 16, 4)
                .WithTag("MARKOV_HI_FAIL_COUNT", 20, 4)
                .WithTag("MARKOV_LO_FAIL_COUNT", 24, 4)
                .WithTag("REPCNTS_FAIL_COUNT", 28, 4);

            Registers.ExternalHealthTestAlertFailureCounts.Define(this)
                .WithTag("EXTHT_HI_FAIL_COUNT", 0, 4)
                .WithTag("EXTHT_LO_FAIL_COUNT", 4, 4)
                .WithReservedBits(8, 24);

            // End of the randomness test registers

            Registers.FirmwareOverrideControl.Define(this, 0x99)
                .WithEnumField(0, 4, out firmwareOverrideMode, name: "FW_OV_MODE")
                .WithEnumField(4, 4, out firmwareOverrideEntropyInsert, name: "FW_OV_ENTROPY_INSERT")
                .WithReservedBits(8, 24);

            Registers.FirmwareOverrideSHA3BlockStartControl.Define(this, 0x9)
                .WithEnumField(0, 4, out firmwareOverrideInsertStart , name: "FW_OV_INSERT_START", writeCallback: (_, value) => 
                {
                    if(value == MultiBitBool4.True)
                    {
                        this.Log(LogLevel.Noisy, "Starting the SHA3 process - ready to accept data in write FIFO");
                    }
                    else if(value == MultiBitBool4.False)
                    {
                        this.Log(LogLevel.Noisy, "Finishing the SHA3 process - no more data will be accepted in write FIFO");
                        sha3Conditioner.FinishProcessing();
                        OutputPath();
                    }
                })
                .WithReservedBits(4, 28);

            Registers.FirmwareOverrideFIFOWriteFullStatus.Define(this)
                .WithFlag(0, out writeFifoFullStatus, FieldMode.Read, name: "FW_OV_WR_FIFO_FULL")
                .WithReservedBits(1, 31);

            Registers.FirmwareOverrideObserveFIFOOverflowStatus.Define(this)
                .WithFlag(0, out observeFifoOverflowStatus, FieldMode.Read | FieldMode.WriteZeroToClear, name: "FW_OV_RD_FIFO_OVERFLOW")
                .WithReservedBits(1, 31);

            Registers.FirmwareOverrideObserveFIFORead.Define(this)
                .WithValueField(0, 32, FieldMode.Read, name: "FW_OV_RD_DATA", valueProviderCallback: (_) =>
                {
                    if(ModeOverride != Mode.FirmwareOverride)
                    {
                        this.Log(LogLevel.Warning, "Trying to read from the Observe FIFO while the firmware override mode is disabled");
                        return 0;
                    }

                    if(observeFifo.Count == 0)
                    {
                        FillBuffers();
                        this.Log(LogLevel.Debug, "Entropy generated on demand");
                    }

                    if(!observeFifo.TryDequeue(out var value))
                    {
                        this.Log(LogLevel.Warning, "No data in the observe FIFO");
                    }

                    return value;
                });

            Registers.FirmwareOverrideFIFOWrite.Define(this)
                .WithValueField(0, 32, FieldMode.Write, name: "FW_OV_WR_DATA")
                .WithWriteCallback((_, value) =>
                {
                    if(ModeOverride != Mode.FirmwareOverride || ModeSelect != Mode.FirmwareOverride)
                    {
                        this.Log(LogLevel.Warning, "Attempt to write to FIFO while not in firmware override mode and entropy insert mode");
                        return;
                    }

                    if(writeFifoFullStatus.Value)
                    {
                        this.Log(LogLevel.Warning, "Attempt to write to full FIFO");
                        return;
                    }

                    InsertIntoEntropyFlow(value, Mode.FirmwareOverride);
                });

            Registers.ObserveFIFOThreshold.Define(this, 0x20)
                .WithValueField(0, 7, out observeFifoThreshold, name: "OBSERVE_FIFO_THRESH")
                .WithReservedBits(7, 25);

            Registers.ObserveFIFODepth.Define(this)
                .WithValueField(0, 7, FieldMode.Read, valueProviderCallback: (_) => (uint)observeFifo.Count, name: "OBSERVE_FIFO_DEPTH")
                .WithReservedBits(7, 25);

            Registers.DebugStatus.Define(this, 0x10000)
                .WithTag("ENTROPY_FIFO_DEPTH", 0, 3)
                .WithTag("SHA3_FSM", 3, 3)
                .WithTaggedFlag("SHA3_BLOCK_PR", 6)
                .WithTaggedFlag("SHA3_SQUEEZING", 7)
                .WithTaggedFlag("SHA3_ABSORBED", 8)
                .WithTaggedFlag("SHA3_ERR", 9)
                .WithTaggedFlag("MAIN_SM_IDLE", 16)
                .WithTaggedFlag("MAIN_SM_BOOT_DONE", 17)
                .WithReservedBits(18, 14);

            Registers.RecoverableAlertStatus.Define(this)
                .WithTaggedFlag("FIPS_ENABLE_FIELD_ALERT", 0)
                .WithTaggedFlag("ENTROPY_DATA_REG_EN_FIELD_ALERT", 1)
                .WithTaggedFlag("MODULE_ENABLE_FIELD_ALERT", 2)
                .WithTaggedFlag("THRESHOLD_SCOPE_FIELD_ALERT", 3)
                .WithTaggedFlag("RNG_BIT_ENABLE_FIELD_ALERT", 5)
                .WithTaggedFlag("FW_OV_SHA3_START_FIELD_ALERT", 7)
                .WithTaggedFlag("FW_OV_MODE_FIELD_ALERT", 8)
                .WithTaggedFlag("FW_OV_ENTROPY_INSERT_FIELD_ALERT", 9)
                .WithTaggedFlag("ES_ROUTE_FIELD_ALERT", 10)
                .WithTaggedFlag("ES_TYPE_FIELD_ALERT", 11)
                .WithTaggedFlag("ES_MAIN_SM_ALERT", 12)
                .WithTaggedFlag("ES_BUS_CMP_ALERT", 13)
                .WithTaggedFlag("ES_THRESH_CFG_ALERT", 14)
                .WithReservedBits(15, 17);

            Registers.HardwareDetectionOfErrorConditionsStatus.Define(this)
                .WithTaggedFlag("SFIFO_ESRNG_ERR", 0)
                .WithTaggedFlag("SFIFO_OBSERVE_ERR", 1)
                .WithTaggedFlag("SFIFO_ESFINAL_ERR", 2)
                .WithTaggedFlag("ES_ACK_SM_ERR", 20)
                .WithTaggedFlag("ES_MAIN_SM_ERR", 21)
                .WithTaggedFlag("ES_CNTR_ERR", 22)
                .WithTaggedFlag("FIFO_WRITE_ERR", 28)
                .WithTaggedFlag("FIFO_READ_ERR", 29)
                .WithTaggedFlag("FIFO_STATE_ERR", 30)
                .WithReservedBits(31, 1);

            Registers.TestErrorConditions.Define(this)
                .WithTag("ERR_CODE_TEST", 0, 5)
                .WithReservedBits(5, 27);

            Registers.MainStateMachineStateDebug.Define(this, 0xf5)
                .WithEnumField(0, 9, out mainMachineState, FieldMode.Read, name: "MAIN_SM_STATE")
                .WithReservedBits(9, 23);
        }

        private void UpdateInterrupts()
        {
            EsEntropyValidIRQ.Set(entropyValidInterruptState.Value && entropyValidInterruptEnable.Value);
            EsHealthTestFailedIRQ.Set(healthTestFailedInterruptState.Value && healthTestFailedInterruptEnable.Value);
            EsObserveFifoReadyIRQ.Set(observeFifoReadyInterruptState.Value && observeFifoReadyInterruptEnable.Value);
            EsFatalErrIRQ.Set(fatalErrorInterruptState.Value && fatalErrorInterruptEnable.Value);
        }

        private void ResetBuffers()
        {
            observeFifo.Clear();
            preConditionerPackerFifo.Clear();
            preEsfinalPackerFifo.Clear();
            esfinalFifo.Clear();
            entropyUnpackerFifo.Clear();
            hardwareOutputFifo.Clear();
        }

        public GPIO EsEntropyValidIRQ { get; }
        public GPIO EsHealthTestFailedIRQ { get; }
        public GPIO EsObserveFifoReadyIRQ { get; }
        public GPIO EsFatalErrIRQ { get; }

        public GPIO RecoverableAlert { get; }
        public GPIO FatalAlert { get; }

        private readonly PseudorandomNumberGenerator entropySource;
        private readonly Sha3Conditioner sha3Conditioner;

        private readonly Queue<uint> observeFifo;
        private readonly Queue<uint> preConditionerPackerFifo;
        private readonly Queue<uint> preEsfinalPackerFifo;
        private readonly Queue<uint> esfinalFifo;
        private readonly Queue<uint> entropyUnpackerFifo;
        private readonly Queue<uint> hardwareOutputFifo;
        
        private IValueRegisterField observeFifoThreshold;
        private IFlagRegisterField entropyValidInterruptState;
        private IFlagRegisterField healthTestFailedInterruptState;
        private IFlagRegisterField observeFifoReadyInterruptState;
        private IFlagRegisterField fatalErrorInterruptState;
        private IFlagRegisterField entropyValidInterruptEnable;
        private IFlagRegisterField healthTestFailedInterruptEnable;
        private IFlagRegisterField observeFifoReadyInterruptEnable;
        private IFlagRegisterField fatalErrorInterruptEnable;
        private IFlagRegisterField moduleEnableRegisterWriteEnable;
        private IEnumRegisterField<MultiBitBool4> entropyDataRegisterEnable;
        private IEnumRegisterField<MultiBitBool4> moduleEnable;
        private IEnumRegisterField<MultiBitBool4> firmwareOverrideMode;
        private IEnumRegisterField<MultiBitBool4> firmwareOverrideEntropyInsert;
        private IEnumRegisterField<MultiBitBool4> firmwareOverrideInsertStart;
        private IEnumRegisterField<StateMachine> mainMachineState;
        private IFlagRegisterField observeFifoOverflowStatus;
        private IEnumRegisterField<MultiBitBool4> routeSelect;
        private IEnumRegisterField<MultiBitBool4> bypassModeSelect;
        private IFlagRegisterField writeFifoFullStatus;

        private Mode ModeOverride => firmwareOverrideMode.Value == MultiBitBool4.True ? Mode.FirmwareOverride : Mode.Normal;
        private Mode ModeSelect => firmwareOverrideEntropyInsert.Value == MultiBitBool4.True ? Mode.FirmwareOverride : Mode.Normal;
        private Route PathSelect => routeSelect.Value == MultiBitBool4.True ? Route.FirmwareInterface : Route.HardwareInterface;
        private BypassMode BypassModeSelect => bypassModeSelect.Value == MultiBitBool4.True ? BypassMode.RawEntropy : BypassMode.Sha3ConditionedEntropy;

        // Depth is in 32-bit units
        private const int ObserveFifoDepth = 64;
        private const int PreConditionerPackerFifoDepth = 2;
        private const int PreEsfinalPackerFifoDepth = 12;
        private const int EsfinalFifoDepth = 48;
        private const int EntropyUnpackerFifoDepth = 12;
        
        private class Sha3Conditioner
        {
            public Sha3Conditioner(int outputLength = 384)
            {
                sha3 = new Sha3Digest(outputLength);
                Sha3OutputLengthBits = outputLength;
                conditionerFifo = new Queue<uint>();
                conditionedDataFifo = new Queue<uint>();
            }

            public void AddData(uint[] data)
            {
                conditionerFifo.EnqueueRange(data);
                if(conditionerFifo.Count >= CompressionBlockSizeBits / 32)
                {
                    // The compression operation, by default, will compress every 2048 tested bits into 384 full-entropy bits
                    FinishProcessing();
                }
            }

            public void FinishProcessing()
            {
                var entropyBytes = conditionerFifo.DequeueAll().SelectMany(BitConverter.GetBytes).ToArray();
                sha3.BlockUpdate(entropyBytes, 0, entropyBytes.Length);

                var hash = new byte[Sha3OutputLengthBits / 8];
                var written = sha3.DoFinal(hash, 0);
                
                var output = new uint[Sha3OutputLengthBits / 32];
                for(var i = 0; i < output.Length; i++)
                {
                    output[i] = BitConverter.ToUInt32(hash, i * 4);
                    conditionedDataFifo.Enqueue(output[i]);
                }
            }

            public uint[] GetConditionedData()
            {
                return conditionedDataFifo.DequeueAll();
            }

            public void Reset()
            {
                conditionerFifo.Clear();
                conditionedDataFifo.Clear();
                sha3.Reset();
            }

            public bool HasConditionedData => conditionedDataFifo.Count >= Sha3OutputLengthBits / 32;

            public int Sha3OutputLengthBits { get; }

            private readonly Queue<uint> conditionerFifo;
            private readonly Queue<uint> conditionedDataFifo;
            private readonly Sha3Digest sha3;

            private const int CompressionBlockSizeBits = 2048;
        }

        private enum Mode
        {
            Normal,
            FirmwareOverride
        }

        private enum BypassMode
        {
            Sha3ConditionedEntropy,
            RawEntropy
        }

        private enum Route
        {
            HardwareInterface,
            FirmwareInterface
        }

        // https://github.com/lowRISC/opentitan/blob/master/hw/ip/entropy_src/rtl/entropy_src_main_sm_pkg.sv
        private enum StateMachine
        {
            Idle              = 0b011110101, // idle
            BootHTRunning     = 0b111010010, // boot mode, wait for health test done pulse
            BootPostHTChk     = 0b101101110, // boot mode, wait for post health test packer not empty state
            BootPhaseDone     = 0b010001110, // boot mode, stay here until master enable is off
            StartupHTStart    = 0b000101100, // startup mode, pulse the sha3 start input
            StartupPhase1     = 0b100000001, // startup mode, look for first test pass/fail
            StartupPass1      = 0b110100101, // startup mode, look for first test pass/fail, done if pass
            StartupFail1      = 0b000010111, // startup mode, look for second fail, alert if fail
            ContHTStart       = 0b001000000, // continuous test mode, pulse the sha3 start input
            ContHTRunning     = 0b110100010, // continuous test mode, wait for health test done pulse
            FWInsertStart     = 0b011000011, // fw ov mode, start the sha3 block
            FWInsertMsg       = 0b001011001, // fw ov mode, insert fw message into sha3 block
            Sha3MsgDone       = 0b100001111, // sha3 mode, all input messages added, ready to process
            Sha3Prep          = 0b011111000, // sha3 mode, request csrng arb to reduce power
            Sha3Process       = 0b010111111, // sha3 mode, pulse the sha3 process input
            Sha3Valid         = 0b101110001, // sha3 mode, wait for sha3 valid indication
            Sha3Done          = 0b110011000, // sha3 mode, capture sha3 result, pulse done input
            Sha3Quiesce       = 0b111001101, // sha3 mode, goto alert state or continuous check mode
            AlertState        = 0b111111011, // if some alert condition occurs, pulse an alert indication
            AlertHang         = 0b101011100, // after pulsing alert signal, hang here until sw handles
            Error             = 0b100111101  // illegal state reached and hang
        }

        public enum Registers
        {
            InterruptState = 0x0,
            InterruptEnable = 0x4,
            InterruptTest = 0x8,
            AlertTest = 0xc,
            RegisterWriteEnableForModuleEnable = 0x10,
            RegisterWriteEnableForControlAndThresholds = 0x14,
            RegisterWriteEnableForAllControls = 0x18,
            Revision = 0x1c,
            ModuleEnable = 0x20,
            Configuration = 0x24,
            EntropyControl = 0x28,
            EntropyDataBits = 0x2c,
            HealthTestWindows = 0x30,
            RepetitionCountTestThresholds = 0x34,
            RepetitionCountSymbolTestThresholds = 0x38,
            AdaptiveProportionTestHighThresholds = 0x3c,
            AdaptiveProportionTestLowThresholds = 0x40,
            BucketTestThresholds = 0x44,
            MarkovTestHighThresholds = 0x48,
            MarkovTestLowThresholds = 0x4c,
            ExternalHealthTestHighThresholds = 0x50,
            ExternalHealthTestLowThresholds = 0x54,
            RepetitionCountTestHighWatermarks = 0x58,
            RepetitionCountSymbolTestHighWatermarks = 0x5c,
            AdaptiveProportionTestHighWatermarks = 0x60,
            AdaptiveProportionTestLowWatermarks = 0x64,
            ExternalHealthTestHighWatermarks = 0x68,
            ExternalHealthTestLowWatermarks = 0x6c,
            BucketTestHighWatermarks = 0x70,
            MarkovTestHighWatermarks = 0x74,
            MarkovTestLowWatermarks = 0x78,
            RepetitionCountTestFailureCounter = 0x7c,
            RepetitionCountSymbolTestFailureCounter = 0x80,
            AdaptiveProportionHighTestFailureCounter = 0x84,
            AdaptiveProportionLowTestFailureCounter = 0x88,
            BucketTestFailureCounter = 0x8c,
            MarkovHighTestFailureCounter = 0x90,
            MarkovLowTestFailureCounter = 0x94,
            ExternalHealthTestHighThresholdFailureCounter = 0x98,
            ExternalHealthTestLowThresholdFailureCounter = 0x9c,
            AlertThreshold = 0xa0,
            AlertSummaryFailureCounts = 0xa4,
            AlertFailureCounts = 0xa8,
            ExternalHealthTestAlertFailureCounts = 0xac,
            FirmwareOverrideControl = 0xb0,
            FirmwareOverrideSHA3BlockStartControl = 0xb4,
            FirmwareOverrideFIFOWriteFullStatus = 0xb8,
            FirmwareOverrideObserveFIFOOverflowStatus = 0xbc,
            FirmwareOverrideObserveFIFORead = 0xc0,
            FirmwareOverrideFIFOWrite = 0xc4,
            ObserveFIFOThreshold = 0xc8,
            ObserveFIFODepth = 0xcc,
            DebugStatus = 0xd0,
            RecoverableAlertStatus = 0xd4,
            HardwareDetectionOfErrorConditionsStatus = 0xd8,
            TestErrorConditions = 0xdc,
            MainStateMachineStateDebug = 0xe0,
        }
    }
}
