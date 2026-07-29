using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.Structure.Registers;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

using ELFSharp.ELF;

using System.Reflection;

namespace Antmicro.Renode.Peripherals.SPI
{
    public class LS1043SPI : SimpleContainer<ISPIPeripheral>, IKnownSize, IDoubleWordPeripheral, IWordPeripheral, IProvidesRegisterCollection<DoubleWordRegisterCollection>, IProvidesRegisterCollection<WordRegisterCollection>, IEndiannessAware
    {
        public LS1043SPI(IMachine machine) : base(machine)
        {
            IRQ = new GPIO(); //Interrupts
            DWRegistersCollection = new DoubleWordRegisterCollection(this); //Define a set of DWRegisters for this peripheral
            WRegistersCollection = new WordRegisterCollection(this); // Need Word registers to enable support for 16-bit accesses to PUSHR (cmd and data separated)
            DefineRegisters(); // Define the Registers for this peripheral (both DW and W)

            Reset(); //Resets system
        }

        public override void Reset()
        {
            this.NoisyLog("In Reset()");
            transferInProgress = false;
            // Currently connected device for transfer
            cmdPCS = -1;

            // FIFO reset
            ClearTxFifo();
            ClearRxFifo();

            // Resets DWRegisters to their initial state
            DWRegistersCollection.Reset();
            WRegistersCollection.Reset();
            UpdateInterrupts();
        }

        public uint ReadDoubleWord(long offset) //For reading a register of the module
        {
            this.NoisyLog("In ReadDoubleWord()");
            if (offset == 0x34 || offset == 0x36) //These are not Double Word registers per this implementation
            {
                this.NoisyLog("Doing two ReadWord() for Word registers");
                uint val = (uint)(ReadWord(0x34) << 16);
                val |= ReadWord(0x36);
                return (val);
            }
            return DWRegistersCollection.Read(offset);
        }

        public void WriteDoubleWord(long offset, uint value) // For writing a register of the module
        {
            this.NoisyLog("In WriteDoubleWord()");
            if (Running && (((DWRegisters)offset == DWRegisters.SPI_TCR))) { //As per doc
                this.Log(LogLevel.Warning, "SPI WriteDoubleWord: Write to register operation has been blocked because SPI is disabled");
                return;
            }
            if ((!Running || !TxFifoNotFull) && ((WRegisters)offset == WRegisters.SPI_CMD_PUSHR || (WRegisters)offset == WRegisters.SPI_DATA_PUSHR )) { //As per doc
                this.Log(LogLevel.Warning, "SPI WriteDoubleWord: Push operation has been blocked because SPI is disabled");
                return;
            }
            if ((DWRegisters)offset == DWRegisters.SPI_SREX) {
                this.Log(LogLevel.Warning, "SPI WriteDoubleWord: Write operation is not allowed on this register");
                return;
            }
            if (offset == 0x34 || offset == 0x36) //These are not Double Word registers per this implementation
            {
                this.NoisyLog("Doing two WriteWord() for Word registers");
                ushort data = (ushort)(value & 0xFFFF); //Split written data into 2
                ushort cmd = (ushort)(value >> 16);
                WRegistersCollection.Write(0x34, cmd); //Do the actual write
                WRegistersCollection.Write(0x36, data);
                return;
            }
            DWRegistersCollection.Write(offset, value);
        }

        public ushort ReadWord(long offset)
        {
            this.NoisyLog("In ReadWord()");
            if (!WRegistersCollection.HasRegisterAtOffset(offset))
            {
                this.Log(LogLevel.Error, "Attempted 16-bit read access at offset {0} ; only offset 0x34/0x36 support that", offset);
                return (ushort)ReadDoubleWord(offset);
            } else if (!xspi.Value)
            { // It's wonky doing a double word access since we only have half the data but what can we do ; should not happen anyway
                this.Log(LogLevel.Error, "Attempted 16-bit read access but they are only supported for Extended SPI mode ; doing a double word access");
                return (ushort)ReadDoubleWord(offset);
            }
            return WRegistersCollection.Read(offset);
        }

        public void WriteWord(long offset, ushort value)
        {
            this.NoisyLog("In WriteWord()");
            if (!WRegistersCollection.HasRegisterAtOffset(offset))
            {
                this.Log(LogLevel.Error, "Attempted 16-bit write access at offset {0} ; only offset 0x34/0x36 support that", offset);
                WriteDoubleWord(offset, value);
                return;
            } else if (!xspi.Value)
            {
                this.Log(LogLevel.Error, "Attempted 16-bit write access but they are only supported for Extended SPI mode");
                WriteDoubleWord(offset, value);
                return;
            }
            if ((!Running || !TxFifoNotFull))
            {
                this.Log(LogLevel.Warning, "SPI WriteWord: Write to register operation has been blocked because SPI is disabled");
                return;
            }
            WRegistersCollection.Write(offset, value);
        }

        public long Size => 0x10000;

        public GPIO IRQ { get; set;} // For IRQ Signaling

        public DoubleWordRegisterCollection DWRegistersCollection { get; } // Allows getting the DWRegisters of this peripheral

        public WordRegisterCollection WRegistersCollection { get; } // Allows getting the WRegisters of this peripheral

        public DoubleWordRegisterCollection RegistersCollection => DWRegistersCollection; //Need explicit mapping to avoid name collision

        WordRegisterCollection IProvidesRegisterCollection<WordRegisterCollection>.RegistersCollection => WRegistersCollection; //Need explicit mapping to avoid name collision ; see SAMD21_I2C

        public Endianess Endianness => Endianess.BigEndian; // This module is big-endian while the processor is little-endian ; register access do not work if we don't specify endianness

        private void DefineRegisters() {
            //"this" is of type IProvidesRegisterCollection ; the Define function will take the RegistersCollection object (mandatory by interface) and pass it as parameter to the inner function, that will add a register to this collection.
            // Here we need an explicit cast because of the collision between WordRegister and DoubleWordRegister
            // Second (optional) parameter is default value, set according to doc
            DWRegisters.SPI_MCR.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x00000001) 
                .WithFlag(31, out masterMode, name: "MASTER - Master Mode") 
                .WithTaggedFlag("CONT_SCKE - Continuous SCK Enable (no clock in emulation)", 30)
                .WithTag("DCONF - SPI Configuration (should always be 00)", 28, 2) 
                .WithReservedBits(25,3) // Driver defines a register here but it is used only in slave mode
                .WithFlag(24, out rxFifoOverwriteOnOverflow, name:"ROOE - Receive FIFO Overflow Overwrite Enable")
                .WithReservedBits(20,4)
                .WithFlag(19, name:"PCSIS3 - Peripheral Chip Select 3 inactive state") //Isn't used at emulation level
                .WithFlag(18, name:"PCSIS2 - Peripheral Chip Select 2 inactive state")
                .WithFlag(17, name:"PCSIS1 - Peripheral Chip Select 1 inactive state")
                .WithFlag(16, name:"PCSIS0 - Peripheral Chip Select 0 inactive state")
                .WithTaggedFlag("DOZE - DOZE Enable", 15) //Power-saving ; not emulated
                .WithFlag(14, out moduleDisabled, name:"MDIS - Module Disable")
                .WithFlag(13, out txDisabled, name: "DIS_TXF - TX FIFO Disable", changeCallback: (_, val) => 
                { 
                    this.Log(LogLevel.Debug, "TX Fifo is currently {0}", val?"disabled":"enabled");
                }, readCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Read access to TX Fifo enabled, returned value {0}", val?"disabled":"enabled");
                }, writeCallback: (_, _) => {
                    ClearTxFifo(); // Must reset counters between mode change
                })
                .WithFlag(12, out rxDisabled, name: "DIS_RXF - RX FIFO Disable", changeCallback: (_, val) => 
                { 
                    this.Log(LogLevel.Debug, "RX Fifo is currently {0}", val?"disabled":"enabled");
                }, readCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Read access to RX Fifo enabled, returned value {0}", val?"disabled":"enabled");
                }, writeCallback: (_, _) => {
                    ClearRxFifo(); // Must reset counters between mode change
                })
                .WithFlag(11, FieldMode.Read | FieldMode.WriteOneToClear, name: "CLR_TXF - Clear TX FIFO", writeCallback: (old, written) => { //Always read as 0 - Writing 1 clears the counter (= callback) but no write is actually done
                    if (written) {
                        ClearTxFifo();
                        this.Log(LogLevel.Debug, "TX FIFO cleared and counter reset to 0");
                    }
                }, valueProviderCallback: (_) => {return false;}) 
                .WithFlag(10, FieldMode.Read | FieldMode.WriteOneToClear, name: "CLR_RXF - Clear RX FIFO", writeCallback: (old, written) => { //Always read as 0 - Writing 1 clears the counter (= callback) but no write is actually done
                    if (written) {
                        ClearRxFifo();
                        this.Log(LogLevel.Debug, "RX FIFO cleared and counter reset to 0");
                    } //Nothing happens if we write 0 (e.g. at init)
                }, valueProviderCallback: (_) => {return false;})
                .WithReservedBits(4,6)
                .WithFlag(3, out xspi, name:"XSPI - Extended SPI Mode", readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "XSPI is {0}", newer?"enabled":"disabled");
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "XSPI set to {0}", newer?"enabled":"disabled");
                })
                .WithTaggedFlag("FCPCS - Fast Continuous PCS Mode", 2) // No emulation of time
                .WithTaggedFlag("PES - Parity Error Stop", 1) //Not used for LS1043
                .WithFlag(0, out halted, name:"HALT", readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Peripheral is {0}", newer?"halted":"running");
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Peripheral is set to {0}", newer?"halted":"running");
                }, writeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Peripheral has been {0}. Running state has been changed accordingly", newer?"halted":"unhalted");
                });

            DWRegisters.SPI_TCR.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out transferCount, name: "TCNT - Transfer Count", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Transfer count updated from {0}, to {1}", old, newer);
                }, writeCallback: (old, written) => {
                    if (Running) {
                        this.Log(LogLevel.Warning, "Attempt to write {0} to TCNT whilst module is running has been blocked", written); //Blocked in Write function
                        transferCount.Value = old;
                    } else
                        this.Log(LogLevel.Debug, "Transfer Count register manually preset to {0}", written);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Current transfer count is {0}", newer);
                })
                .WithReservedBits(0, 16);
            
            DWRegisters.SPI_CTAR0.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x78000000) // Each frame can select one of these 4 configurations (user-configured)
                .WithTaggedFlag("DBR - Double Baud Rate", 31) //Used by software, but wouldn't change anything in emulation in theory
                .WithValueField(27, 4, out frameSize0, name: "FMSZ - Frame Size", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Frame Size changed from {0} to {1}", old, newer);
                }, writeCallback: (old, newer) => {
                    if (newer < 3) { // 3 is a valid value since GetFrameSize returns +1
                        frameSize0.Value = old;
                        this.Log(LogLevel.Warning, "Tried to set frame size to {0} while the minimum is 4 ; frame size kept its old value of {1}", newer, old);
                    } else {
                        this.Log(LogLevel.Debug, "Frame Size set to {0}", newer);
                    }
                })
                //All of the below DWRegisters are used by the software but have no effect on emulation
                .WithTaggedFlag("CPOL - Clock Polarity", 26)
                .WithTaggedFlag("CPHA - Clock Phase", 25) 
                .WithTaggedFlag("LSBFE - LSB First", 24)
                .WithTag("PCSSCK - PCS-to-SCK Delay Prescaler", 22, 2)
                .WithTag("PASC - After SCK Delay Prescaler", 20, 2)
                .WithTag("PDT - Delay After Transfer Prescaler", 18, 2)
                .WithTag("PBR - Baud Rate Prescaler", 16, 2)
                .WithTag("CSSCK - PCS to SCK Delay Scaler", 12, 4)
                .WithTag("ASC - After SCK Delay Scaler", 8, 4)
                .WithTag("DT - Delay After Transfer Scaler", 4, 4)
                .WithTag("BR - Baud Rate Scaler", 0, 4);

            DWRegisters.SPI_CTAR1.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x78000000)
                .WithTaggedFlag("DBR - Double Baud Rate", 31)
                .WithValueField(27, 4, out frameSize1, changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Frame Size changed from {0} to {1}", old, newer);
                }, writeCallback: (old, newer) => {
                    if (newer < 3) {
                        frameSize1.Value = old;
                        this.Log(LogLevel.Warning, "Tried to set frame size to {0} while the minimum is 4 ; frame size kept its old value of {1}", newer, old);
                    } else {
                        this.Log(LogLevel.Debug, "Frame Size set to {0}", newer);
                    }
                })
                .WithTaggedFlag("CPOL - Clock Polarity", 26)
                .WithTaggedFlag("CPHA - Clock Phase", 25) 
                .WithTaggedFlag("LSBFE - LSB First", 24)
                .WithTag("PCSSCK - PCS-to-SCK Delay Prescaler", 22, 2)
                .WithTag("PASC - After SCK Delay Prescaler", 20, 2)
                .WithTag("PDT - Delay After Transfer Prescaler", 18, 2)
                .WithTag("PBR - Baud Rate Prescaler", 16, 2)
                .WithTag("CSSCK - PCS to SCK Delay Scaler", 12, 4)
                .WithTag("ASC - After SCK Delay Scaler", 8, 4)
                .WithTag("DT - Delay After Transfer Scaler", 4, 4)
                .WithTag("BR - Baud Rate Scaler", 0, 4);

            DWRegisters.SPI_CTAR2.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x78000000)
                .WithTaggedFlag("DBR - Double Baud Rate", 31)
                .WithValueField(27, 4, out frameSize2, changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Frame Size changed from {0} to {1}", old, newer);
                }, writeCallback: (old, newer) => {
                    if (newer < 3) {
                        frameSize2.Value = old;
                        this.Log(LogLevel.Warning, "Tried to set frame size to {0} while the minimum is 4 ; frame size kept its old value of {1}", newer, old);
                    } else {
                        this.Log(LogLevel.Debug, "Frame Size set to {0}", newer);
                    }
                })
                .WithTaggedFlag("CPOL - Clock Polarity", 26)
                .WithTaggedFlag("CPHA - Clock Phase", 25) 
                .WithTaggedFlag("LSBFE - LSB First", 24)
                .WithTag("PCSSCK - PCS-to-SCK Delay Prescaler", 22, 2)
                .WithTag("PASC - After SCK Delay Prescaler", 20, 2)
                .WithTag("PDT - Delay After Transfer Prescaler", 18, 2)
                .WithTag("PBR - Baud Rate Prescaler", 16, 2)
                .WithTag("CSSCK - PCS to SCK Delay Scaler", 12, 4)
                .WithTag("ASC - After SCK Delay Scaler", 8, 4)
                .WithTag("DT - Delay After Transfer Scaler", 4, 4)
                .WithTag("BR - Baud Rate Scaler", 0, 4);

            DWRegisters.SPI_CTAR3.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x78000000)
                .WithTaggedFlag("DBR - Double Baud Rate", 31)
                .WithValueField(27, 4, out frameSize3, changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Frame Size changed from {0} to {1}", old, newer);
                }, writeCallback: (old, newer) => {
                    if (newer < 3) {
                        frameSize3.Value = old;
                        this.Log(LogLevel.Warning, "Tried to set frame size to {0} while the minimum is 4 ; frame size kept its old value of {1}", newer, old);
                    } else {
                        this.Log(LogLevel.Debug, "Frame Size set to {0}", newer);
                    }
                })
                .WithTaggedFlag("CPOL - Clock Polarity", 26)
                .WithTaggedFlag("CPHA - Clock Phase", 25) 
                .WithTaggedFlag("LSBFE - LSB First", 24)
                .WithTag("PCSSCK - PCS-to-SCK Delay Prescaler", 22, 2)
                .WithTag("PASC - After SCK Delay Prescaler", 20, 2)
                .WithTag("PDT - Delay After Transfer Prescaler", 18, 2)
                .WithTag("PBR - Baud Rate Prescaler", 16, 2)
                .WithTag("CSSCK - PCS to SCK Delay Scaler", 12, 4)
                .WithTag("ASC - After SCK Delay Scaler", 8, 4)
                .WithTag("DT - Delay After Transfer Scaler", 4, 4)
                .WithTag("BR - Baud Rate Scaler", 0, 4);

            DWRegisters.SPI_SR.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x02010000)
                .WithFlag(31, out transferComplete, FieldMode.Read | FieldMode.WriteOneToClear, name: "TCF - Transfer Complete Flag", changeCallback: (old,newer) => {
                    this.Log(LogLevel.Debug, "Transfer flag changed from {0} to {1}", old?"complete":"not complete", newer?"complete":"not complete");
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Transfer flag is set to {0}", newer?"complete":"not complete");
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "TCF has been cleared");
                })
                .WithFlag(30, FieldMode.Read, name: "TXRXS - TX and RX Status", readCallback: (_, _) => {
                    this.Log(LogLevel.Debug, "Module is currently {0}", Running?"running":"not running");
                }, valueProviderCallback: (_) => Running) //This is an "auto-updating" variable, ensuring it's always at an accurate value
                .WithReservedBits(29,1)
                .WithFlag(28, out endOfQueueSR, name:"EOQF - End Of Queue Flag") // Set by hardware at the end of a command with EOQ set
                .WithReservedBits(26,2)
                .WithFlag(25, FieldMode.Read | FieldMode.WriteOneToClear, name: "TFFF - Transmit Fifo Fill Flag", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Transmit Fifo Fill Flag changed from {0} to {1}", old?"not full":"full", newer?"not full":"full");
                    }, valueProviderCallback: (_) => {
                        this.Log(LogLevel.Debug, "Transmit Fifo Full flag is set to {0}", TxFifoNotFull?"not full":"full");
                        return TxFifoNotFull;
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "TFFF has been cleared");
                })
                .WithFlag(24, name:"BSYF - Busy Flag", valueProviderCallback: (_) => (cmdHowMuchLeft > 1)) //Set when cyclic command underway
                .WithFlag(23, out commandTransferComplete, FieldMode.Read | FieldMode.WriteOneToClear,  name: "CMDTCF - Command Transfer Complete Flag", changeCallback: (old, newer) => { //Used only in Extended SPI mode
                    this.Log(LogLevel.Debug, "Command Transfer Complete Flag changed from {0} to {1}", old?"done":"not done", newer?"done":"not done");
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Command Transfer Complete Flag is set to {0}", newer?"done":"not done");
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "CMDTCF has been cleared");
                })
                .WithReservedBits(22,1)
                .WithTaggedFlag("SPEF - SPI Parity Error Flag", 21) // Parity isn't used by LS1043
                .WithReservedBits(20,1)
                .WithFlag(19, out rxFifoOverflow, FieldMode.Read | FieldMode.WriteOneToClear, name: "RFOF - Receive Fifo Overflow Flag", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Receive Fifo Overflow Flag changed from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Receive Fifo Overflow Flag is set to {0}", newer);
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "RFOF has been cleared");
                })
                .WithFlag(18, out txFifoInvalidWrite, FieldMode.Read | FieldMode.WriteOneToClear, name: "TFIWF - Transmit Fifo Invalid Write Flag", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Transmit Fifo Invalid Write Flag changed from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Transmit Fifo Invalid Write Flag is set to {0}", newer);
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "TFIWF has been cleared");
                })
                .WithFlag(17, FieldMode.Read, name:"RFDF - Receive FIFO Drain Flag", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Receive Fifo Drain Flag changed from {0} to {1}", old?"not empty":"empty", newer?"not full":"full");
                    }, valueProviderCallback: (_) => {
                        this.Log(LogLevel.Debug, "Receive Fifo Drain flag is set to {0}", TxFifoNotFull?"not empty":"empty");
                        return RxDrainFlag;
                })
                .WithFlag(16, FieldMode.Read | FieldMode.WriteOneToClear, name:"CMDFFF - Command FIFO Fill Flag", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Command Fifo Fill Flag changed from {0} to {1}", old?"not full":"full", newer?"not full":"full");
                    }, valueProviderCallback: (_) => {
                        this.Log(LogLevel.Debug, "Command Fifo Full flag is set to {0}", TxFifoNotFull?"not full":"full");
                        return CmdFifo;
                }, writeCallback: (_, newer) => {
                    if (newer) 
                        this.Log(LogLevel.Debug, "CMDFFF has been cleared");
                })// = 1 -> not full
                //All of the DWRegisters below are not used by the software but I assume they can be useful to the inner functionning as well as for debug purposes
                .WithValueField(12, 4, out txCounter, FieldMode.Read, name: "TXCTR - TX Fifo Counter", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "TX FIFO counter updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "TX FIFO counter is {0}", newer);
                })
                .WithValueField(8, 4, out txNext, FieldMode.Read, name: "TXNXTPTR - Transmit Next Pointer", changeCallback: (old, newer) => {
                    // NOTE : this shouldn't be set to 0 by the Clear flag, but we do it here for convinience
                    this.Log(LogLevel.Debug, "Transmit Next Pointer updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Transmit Next Pointer is {0}", newer);
                })
                .WithValueField(4, 4, out rxCounter, FieldMode.Read, name: "RXCTR - RX Fifo Counter", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "RX FIFO counter updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "RX FIFO counter is {0}", newer);
                })
                .WithValueField(0, 4, out rxNext, FieldMode.Read, name: "POPNXTPTR - Pop Next Pointer", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Pop Next Pointer updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Pop Next Pointer is {0}", newer);
                })
                .WithChangeCallback((_,_) => UpdateInterrupts()); //Any modification on the SR register should trigger interrupt updates

            DWRegisters.SPI_RSER.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithFlag(31, out transferCompleteInterrupt, name: "TCF_RE - Transmission Complete Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Transmission Complete Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Transmission Complete {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithFlag(30, out cmdFifoInterrupt, name: "CMDFFF_RE - Command FIFO Fill Flag Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Command FIFO Fill Flag Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Command FIFO Fill Flag {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithReservedBits(29,1)
                .WithFlag(28, out endOfQueueSRInterrupt, name: "EOQF_RE - Finished Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Finished Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "End Of Queue (SR) {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithReservedBits(26,2)
                .WithFlag(25, out txFifoNotFullInterrupt, name: "TFFF_RE - TX FIFO Fill Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "TX FIFO Fill Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "TX FIFO Fill flag {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithTaggedFlag("TFFF_DIRS - TX FIFO Fill DMA or Interrupt Request Select", 24) // DMA is not implemented ; leave at 0
                .WithFlag(23, out commandTransferCompleteInterrupt, name: "CMDTCF_RE - Command Transmission Complete Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Command Transmission Complete Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Command Transmission Complete {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithReservedBits(22,1)
                .WithTaggedFlag("SPEF_RE - SPI Parity Error Request Enable", 21)
                .WithReservedBits(20,1)
                .WithFlag(19, out rxFifoOverflowInterrupt, name:"RFOF_RE - Receive Fifo Overflow Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Receive Fifo Overflow Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Receive Fifo Overflow {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithFlag(18, out txFifoInvalidWriteInterrupt, name:"TFIWF_RE - Transmit FIFO Invalid Write Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Transmit FIFO Invalid Write Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Transmit FIFO Invalid Write {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithFlag(17, out rxDrainFlagInterrupt, name: "RFDF_RE - Receive FIFO Drain Request Enable", changeCallback: (old, newer) => {
                    this.Log(LogLevel.Debug, "Receive FIFO Drain Request Enable updated from {0} to {1}", old, newer);
                }, readCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Receive FIFO Drain {0}generate interrupt requests", newer?"":"do not ");
                })
                .WithTaggedFlag("RFDF_DIRS - Receive FIFO Drain DMA or Interrupt Request Select", 16)  // We don't have a DMA
                .WithTaggedFlag("CMDFFF_DIRS - Command FIFO Fill DMA or Interrupt Request Select", 15)
                .WithReservedBits(0, 15)
                .WithChangeCallback((_,_) => UpdateInterrupts()); //Any modification on the RSER register should trigger interrupt updates

            // Need to store the whole register for when FIFO is disabled ; this will be the immediate value used
            // No out registers on this register ; only when we send the command will the arguments be determined
            cmd_pushr_reg = WRegisters.SPI_CMD_PUSHR.Define(this as IProvidesRegisterCollection<WordRegisterCollection>, 0x0) //As seen in Peripherals/I2C/SAMD21_I2C.c
                .WithFlag(15, name:"CONT - Continuous Peripheral Chip Select Enable") 
                .WithValueField(12, 3, name: "CTAS - Clock and Transfer Attributes Select", writeCallback: (_, newer) => {
                    if (newer > 3) {
                        this.Log(LogLevel.Warning, "PUSHR : CTAS writes of the form 1XX ({0}) are reserved ; it will be changed to 0XX", newer);
                    }
                })
                .WithFlag(11, name:"EOQ - End Of Queue") 
                .WithFlag(10, name:"CTCNT - Clear Transfer Counter")
                .WithTaggedFlag("PE_MASC - Parity Enable or Mask T_asc delay in current frame", 9)
                .WithTaggedFlag("PP_MCSC - Parity Polarity or Masc T_asc delay in the next frame", 8)
                .WithReservedBits(4,4)
                .WithValueField(0, 4, name: "PCS - Peripheral Chip Select")
                .WithWriteCallback((old, cmd) => // Writing the whole register in the CMD FIFO
                {
                    if (txDisabled.Value) {
                        txCounter.Value = 1; // Filled PUSHR so at least 1 data ; no FIFO so at max 1 data
                        this.Log(LogLevel.Debug, "Registered cmd : {0} ; FIFO disabled", cmd);
                    } else {
                        if (cmdCounter.Value >= CmdFifoSize)
                        {
                            cmd_pushr_reg.Value = old;
                            this.Log(LogLevel.Warning, "Could not push cmd : {0} to TX FIFO because it is full. PUSHR register restored to its old state", cmd);
                        } else {
                            PushDataToCMDFIFO(cmd); // Actually pushes the data in the correct register and updates pointers
                            this.Log(LogLevel.Debug, "Pushing cmd : {0} to TX FIFO", cmd);
                        }                       
                    }
                });

            data_pushr_reg = WRegisters.SPI_DATA_PUSHR.Define(this as IProvidesRegisterCollection<WordRegisterCollection>, 0x0) //As seen in Peripherals/I2C/SAMD21_I2C.c
                .WithValueField(0, 16, name: "TXDATA")
                .WithWriteCallback((old, data) => {
                    if (txDisabled.Value) {
                        txCounter.Value = 1; // Filled PUSHR so at least 1 data ; no FIFO so at max 1 data
                        this.Log(LogLevel.Debug, "Registered data : {0} ; FIFO disabled", data);
                    } else {
                        if (txCounter.Value >= TxFifoSize) { //Ignores attempt to push data to a full TX FIFO
                            data_pushr_reg.Value = old; //Restores the old value
                            this.Log(LogLevel.Warning, "Could not push data : {0} to TX FIFO because it is full. PUSHR register restored to its old state", data);
                        }
                        else {
                            PushDataToTXFIFO(data); // Actually pushes the data in the correct register and updates pointers
                            this.Log(LogLevel.Debug, "Pushed and sent data : {0} to TX FIFO", data);

                            while (inLaunchTransfer);
                            if (!LaunchTransfer()) // Does a transfer as soon as there is data (not command alone) to be transferred
                            {
                                this.ErrorLog("Couldn't do a transfer :(");
                            }
                        }                       
                    }
                });

            popr_reg = DWRegisters.SPI_POPR.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0,32, FieldMode.Read, readCallback: (value, _) => {
                    // Actual data fetch is done in valueProvider
                    this.Log(LogLevel.Debug, "Popped data {0}", value);
                }, writeCallback: (_,_) => {
                    throw new InvalidRegisterAccessException("TransferError (Cannot do write access to this register as per the documentation)", null); 
                }, valueProviderCallback: old => {
                    if (rxCounter.Value == 0) {
                        this.Log(LogLevel.Warning, "Attempting to pop data from empty RXFIFO ; return data is undetermined");
                        return old;
                    } else if (rxDisabled.Value) {
                        rxCounter.Value = 0; // Value should not be more than 1 since fifo is disabled ; bringing this value down to 0
                        return old; //Value stored in this register when RX Fifo was disabled
                    } else return GetDataFromRXFIFO(); // Get RX Data in register, increase next pointer and decrease counter
                });

            DWRegisters.SPI_TXFR0.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[0], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", newer);
                })
                .WithValueField(0, 16, out txdata[0], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", newer);
                });

            DWRegisters.SPI_TXFR1.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[1], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", newer);
                })
                .WithValueField(0, 16, out txdata[1], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, newer) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", newer);
                });
                
            DWRegisters.SPI_TXFR2.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[2], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[2], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR3.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[3], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[3], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR4.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[4], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[4], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR5.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[5], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[5], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR6.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[6], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[6], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR7.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[7], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[7], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR8.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[8], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[8], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR9.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[9], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[9], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR10.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[10], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[10], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR11.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[11], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[11], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR12.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[12], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[12], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR13.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[13], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[13], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR14.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[14], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[14], FieldMode.Read, name: "TXDATA - Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_TXFR15.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(16, 16, out txcmd[15], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Cmd register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Cmd register set to {0}", val);
                })
                .WithValueField(0, 16, out txdata[15], FieldMode.Read, name: "TXCMD_TXDATA - Transmit Command or Transmit Data", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Data register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Data register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR0.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[0], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR1.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[1], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR2.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[2], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR3.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[3], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR4.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[4], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR5.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[5], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR6.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[6], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR7.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[7], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR8.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[8], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR9.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[9], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR10.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[10], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR11.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[11], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR12.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[12], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR13.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[13], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR14.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[14], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_RXFR15.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithValueField(0, 32, out rxdata[15], FieldMode.Read, name: "RXFR - Receive Fifo DWRegisters", readCallback: (val, _) => {
                    this.Log(LogLevel.Debug, "Register contains {0}", val);
                }, changeCallback: (_, val) => {
                    this.Log(LogLevel.Debug, "Register set to {0}", val);
                });
                
            DWRegisters.SPI_CTARE0.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x1)
                .WithReservedBits(17, 14)
                .WithFlag(16, out frameSizeExt0, name: "FMSIZE - Frame Size Extended", writeCallback: (_,val) =>
                {
                    this.Log(LogLevel.Debug, "Frame size will {0}be +16",(val ? "" : "not "));
                }) // Needed by soft
                .WithReservedBits(11, 5)
                .WithValueField(0,11, out dataTransferCount0, name:"DTCP - Data Transfer Count Preload");
            DWRegisters.SPI_CTARE1.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x1)
                .WithReservedBits(17, 14)
                .WithFlag(16, out frameSizeExt1, name: "FMSIZE - Frame Size Extended", writeCallback: (_,val) =>
                {
                    this.Log(LogLevel.Debug, "Frame size will {0}be +16",(val ? "" : "not "));
                }) // Needed by soft
                .WithReservedBits(11, 5)
                .WithValueField(0,11, out dataTransferCount1, name:"DTCP - Data Transfer Count Preload");
            DWRegisters.SPI_CTARE2.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x1)
                .WithReservedBits(17, 14)
                .WithFlag(16, out frameSizeExt2, name: "FMSIZE - Frame Size Extended", writeCallback: (_,val) =>
                {
                    this.Log(LogLevel.Debug, "Frame size will {0}be +16",(val ? "" : "not "));
                }) // Needed by soft
                .WithReservedBits(11, 5)
                .WithValueField(0,11, out dataTransferCount2, name:"DTCP - Data Transfer Count Preload");
            DWRegisters.SPI_CTARE3.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x1)
                .WithReservedBits(17, 14)
                .WithFlag(16, out frameSizeExt3, name: "FMSIZE - Frame Size Extended", writeCallback: (_,val) =>
                {
                    this.Log(LogLevel.Debug, "Frame size will {0}be +16",(val ? "" : "not "));
                }) // Needed by soft
                .WithReservedBits(11, 5)
                .WithValueField(0,11, out dataTransferCount3, name:"DTCP - Data Transfer Count Preload");

            DWRegisters.SPI_SREX.Define(this as IProvidesRegisterCollection<DoubleWordRegisterCollection>, 0x0)
                .WithReservedBits(0, 17)
                .WithTaggedFlag("TXCTR4 - TX FIFO Counter Extension", 17) //We are not doing extensions here
                .WithReservedBits(18,2)
                .WithTaggedFlag("RXCTR4 - RX FIFO Counter Extension", 20)
                .WithReservedBits(21,2)
                .WithValueField(23, 5, out cmdCounter, name:"CMDCTR - CMD FIFO Counter")
                .WithValueField(28, 4, out cmdNext, name:"CMDNXTPTR - Command Next Pointer");                
        }

//____________________
// Basic helper functions
        private void IncrementTransferCounter()
        {
            transferCount.Value +=1;
            if (transferCount.Value > 0xFFFF)
            {
                transferCount.Value = (transferCount.Value) % 0xFFFF;
            }
        }

        private void IncrementTxNxt()
        {
            txNext.Value +=1;
            if (txNext.Value > TxFifoSize)
                txNext.Value = (txNext.Value) % TxFifoSize;
        }

        private void IncrementRxNxt()
        {
            rxNext.Value +=1;
            if (rxNext.Value > RxFifoSize)
                rxNext.Value = (rxNext.Value) % RxFifoSize;
        }

        /*
         * From IMXRT_LPSPI.cs.
         * Returns the Frame Size = number of bits to transfer
         * Since TX FIFO is 32 bits, there can be up to 4 bytes = 4 transfers to do with 1 FIFO entry
         */
        private uint GetFrameSize()
        {
            this.NoisyLog("In GetFrameSize()");
            // frameSize keeps value substracted by 1 - see documentation
            ulong frameSize;
            // Extended SPI is taken into account with the following function
            switch (cmdClockTransferAttributeSelect) { //Filled when decyphering the command
                case 4:
                case 0: frameSize = frameSize0.Value; break;
                case 5:
                case 1: frameSize = frameSize1.Value; break;
                case 6:
                case 2: frameSize = frameSize2.Value; break;
                case 7:
                case 3: frameSize = frameSize3.Value; break;
                default: this.Log(LogLevel.Error, "GetFrameSize: CTAS selection not in range 0-7 should not happen"); 
                         frameSize = frameSize0.Value;
                         break;
            }
            var size = (uint)frameSize + 1;
            if(size % 8 != 0) // Spi standard - we transmit bytes
            {
                size += 8 - (size % 8); //Change to multiple of 8 smaller value
                this.Log(LogLevel.Warning, "Only 8-bit-aligned transfers are currently supported, but frame size is set to {0}, adjusting it to: {1}", frameSize, size);
            }
            this.NoisyLog("Returning {0}", size);
            return size;
        }

        private bool IsFrame32Bits()
        {
            bool resp;
            this.NoisyLog("In IsFrame32Bits()");
            switch (cmdClockTransferAttributeSelect) { //Filled when decyphering the command
                case 4:
                case 0: resp = (frameSizeExt0.Value); break;
                case 5:
                case 1: resp = (frameSizeExt1.Value); break;
                case 6:
                case 2: resp = (frameSizeExt2.Value); break;
                case 7:
                case 3: resp = (frameSizeExt3.Value); break;
                default: this.Log(LogLevel.Error, "GetFrameSize: CTAS selection not in range 0-7 should not happen"); 
                         resp = false; break;
            }
            this.DebugLog("Return value is {0}", resp);
            return resp;
        }

        /*
         * We have 1 interrupt output that is on whenever one of the conditions is met.
         * For the condition to be met, this specific action must enable interrupt and also be up.
         */
        private void UpdateInterrupts()
        {
            this.NoisyLog("In UpdateInterrupts()");
            var flag = false;

            flag |= transferComplete.Value && transferCompleteInterrupt.Value; 
            flag |= CmdFifo && cmdFifoInterrupt.Value; 
            flag |= endOfQueueSR.Value && endOfQueueSRInterrupt.Value; 
            flag |= TxFifoNotFull && txFifoNotFullInterrupt.Value;
            flag |= commandTransferComplete.Value && commandTransferCompleteInterrupt.Value;
            flag |= RxDrainFlag && rxDrainFlagInterrupt.Value;
            flag |= rxFifoOverflow.Value && rxFifoOverflowInterrupt.Value;
            flag |= txFifoInvalidWrite.Value && txFifoInvalidWriteInterrupt.Value;

            this.Log(LogLevel.Debug, "Setting IRQ flag to {0}", flag);
            IRQ.Set(flag);
        }

        /* Empties out Fifo.
         * Technically, emptying just means saying there is 0 elements in it.
         */
        private void ClearTxFifo() {
            this.NoisyLog("In ClearTxFifo()");
            txCounter.Value = 0;
            txNext.Value = 0; 
            // Data will be inaccessible since counter 0 will prevent any transfer from happening.
        }

        private void ClearRxFifo() {
            this.NoisyLog("In ClearRxFifo()");
            rxCounter.Value = 0;
            rxNext.Value = 0;
        }

        private void PushDataToCMDFIFO(ulong data) {
            this.NoisyLog("In PushDataCmdFIFO()");
            uint nextToWrite = (uint)(cmdNext.Value + cmdCounter.Value) % CmdFifoSize;

            txcmd[nextToWrite].Value = data; 
            this.NoisyLog($"Command has been pushed to CMDFIFO {nextToWrite}");

            // Fifo Full case handled inside the write handler of the push register
            cmdCounter.Value+=1; //One entry has been added
            UpdateInterrupts();
        }

        private void PushDataToTXFIFO(ulong data) {
            this.NoisyLog("In PushDataTXFIFO()");
            // Fifo Full case handled inside the write handler of the push register
            uint nextToWrite = (uint)(txNext.Value + txCounter.Value) % TxFifoSize;

            txdata[nextToWrite].Value = data; 
            this.NoisyLog($"Data has been pushed to TXFIFO {nextToWrite}");

            txCounter.Value+=1;

            if (cmdCounter.Value == 0) // No data in cmd fifo so this entry is invalid
                txFifoInvalidWrite.Value = true;
            UpdateInterrupts();
        }

        private ulong GetDataFromRXFIFO() {
            this.NoisyLog("In TryGetDataRxFifo()");
            // fifo empty case handled in the value provider of the popr register
            ulong data = rxdata[rxNext.Value].Value;
            this.NoisyLog($"Command has been popped from RXFIFO {rxNext.Value}");

            rxCounter.Value-=1;
            rxNext.Value = (rxNext.Value + 1) % RxFifoSize;
            return data;
        }

        private bool SendDecypher(ulong cmd) {
            this.NoisyLog("In SendDecypher()");
            cmdContinuousPSCEnable = (((cmd >> 15) & 0x1) == 1); // Bit 15
            cmdClockTransferAttributeSelect = (uint)((cmd >> 12) & 0x7); // Bit 14-12
            cmdEndOfQueue = (((cmd >> 11) & 1) == 1); //Bit 11
            var cmdClearTransferCount = (((cmd >> 10) & 0x1) == 1); //Bit 10
            if (cmdClearTransferCount) transferCount.Value = 0; //Clearing the TCNT field before current transfer
            // Mask (9) and polarity (8) are ignored ; 7-4 are reserved
            switch (cmd & 0xF) { // See documentation for PCS routing - bits 3-0
                case 1: cmdPCS = 0; break;
                case 2: cmdPCS = 1; break;
                case 4: cmdPCS = 2; break;
                case 8: cmdPCS = 3; break;
                default: 
                    this.Log(LogLevel.Error, "Wrong Peripheral Chip Select for this transfer (value {0} is not allowed); abort", cmd & 0xF);
                    cmdPCS = -1; return false;
            }
            return true;
        }

        /*
         * From IMXRT_LPSPI.cs ; adapted for our use case
         * Returns if the peripheral with the given offset exists. If it does, also returns the associated peripheral.
         */
        private bool TryGetDevice(out ISPIPeripheral device)
        {
            this.NoisyLog("In TryGetDevice()");
            if (!TryGetByAddress(cmdPCS, out device)) { //cmdPCS is the device ID selected in the command currently being handled
                device = null;
                this.Log(LogLevel.Warning, "Device {0} isn't connected!", cmdPCS);
                return false;
            }
            return true;
        }

        private bool LaunchTransfer() {
            this.NoisyLog("In LaunchTransfer()");
            inLaunchTransfer = true;
            while(transferInProgress && transferComplete.Value); //Wait for the current transfer to complete and for the system to acknowledge it
            bool r=TryExchange();
            inLaunchTransfer = false;
            return r;
        }

        private bool TakeNewCommand()
        {
            ulong cmd = txcmd[cmdNext.Value].Value;
            if (!SendDecypher(cmd)) return false;
            if (cmdCounter.Value == 0)
            {
                this.ErrorLog("TakeNewCommand: Not possible to take a new command as command FIFO is empty"); 
                return false;  
            }
            cmdCounter.Value -= 1;
            cmdNext.Value = (cmdNext.Value + 1) % CmdFifoSize;

            if (xspi.Value) // Number of data to transfer with this command is set by driver
            {
                switch (cmdClockTransferAttributeSelect)
                {
                    case 0: cmdHowMuchLeft = dataTransferCount0.Value; break;
                    case 1: cmdHowMuchLeft = dataTransferCount1.Value; break;
                    case 2: cmdHowMuchLeft = dataTransferCount2.Value; break;
                    case 3: cmdHowMuchLeft = dataTransferCount3.Value; break;
                    default: this.Log(LogLevel.Error, "cmdClockTransferAttributeSelect value {0} should not happen, only between 0 and 3", cmdClockTransferAttributeSelect); break;
                }
            } else //When no XSPI, one command = one data
                cmdHowMuchLeft = 1;

            this.Log(LogLevel.Debug, "cmdHowMuchLeft has been set to {0}", cmdHowMuchLeft);
            return true;
        }

        private void EnqueueResponse(uint receivedWord)
        {
            if (rxDisabled.Value)
            {
                if (rxCounter.Value > 0) //This is an overflow
                {
                    rxFifoOverflow.Value = true;
                    if (rxFifoOverwriteOnOverflow.Value)
                    {
                        popr_reg.Value = receivedWord;
                    } // else data is discarded, we do nothing
                } else // Store value and increment rxctr
                {
                    popr_reg.Value = receivedWord; 
                    rxCounter.Value += 1;
                }
            } else //RX FIFO is enabled
            {
                ulong nextRxSpace = (rxNext.Value + rxCounter.Value) % RxFifoSize;
                if (rxCounter.Value >= RxFifoSize) //We overflowed
                {
                    rxFifoOverflow.Value = true;
                    if (rxFifoOverwriteOnOverflow.Value)
                    {
                        ulong currentSpace = (nextRxSpace - 1) % RxFifoSize;
                        rxdata[currentSpace].Value = receivedWord;
                    } // else data is discarded, we do nothing
                } else // Store value and increment rxctr
                {
                    rxdata[nextRxSpace].Value = receivedWord; 
                    rxCounter.Value += 1;
                }
            } //Storage of the RX data END   
        }

        private uint TransmitWhileData(ulong data, uint currentFrameSize)
        {
            var byteIdx = 0;
            uint receivedWord = 0;

            while(currentFrameSize != 0 && byteIdx < 4) 
            {
                byte toTransmit = (byte)(data >> (int)(currentFrameSize - 8)); //Need this to send in the right order
                this.Log(LogLevel.Noisy, "Transmitting byte: {0}. Full data is {1}", toTransmit, data);
                var resp = device.Transmit(toTransmit);
                receivedWord |= (uint)resp << (int)(currentFrameSize - 8);
                currentFrameSize -= 8;
                byteIdx++;
            }
            
            return receivedWord;
        }

        private bool TryExchange() {
            this.NoisyLog("In TryExchange()");

            // We can't do a transfer if there is nothing to transfer!
            if (txCounter.Value == 0)
            {
                this.Log(LogLevel.Warning, "TX FIFO is empty - no SPI transfer happening");
                return false;
            }

            // Command is not stored as is ; it's taken and decyphered when we need it
            ulong data;

            // Fetching command and data for this transfer
            if (txDisabled.Value)
            {
                this.NoisyLog("TX is disabled ; using contents of PUSHR register");
                data = data_pushr_reg.Value;
                SendDecypher(cmd_pushr_reg.Value);  
                txCounter.Value -= 1;
                cmdCounter.Value -= 1;
            } else
            {
                this.NoisyLog("TX is enabled ; using contents of FIFO");

                if (cmdHowMuchLeft == 0) //We need to pull a new command
                {
                    if (!TakeNewCommand())
                    {
                        this.ErrorLog("Failed to pull a new command ; stopping transfer");
                        return false;
                    }
                    
                    // We can't do a transfer if the target peripheral is not connected!
                    device = null;
                    // Test if transfer possible and save target
                    if (!TryGetDevice(out device)) {
                        this.Log(LogLevel.Warning, "SPI transfer abort since transfer is not possible");
                        return false; //Avoids popping a data and not using it ; here command will stay valid until next time since HowMuchLeft is not 0
                    }
                } // In every other case, we already have the command parameters set

                if (IsFrame32Bits() && txCounter.Value < 2) //Using the command we prepared earlier
                {
                    return true; // Transfer technically didn't fail, but we need to wait a little more
                }
            } // Preparing data and command END

            // We know what we send and how ; time to transfer
            var currentFrameSize = GetFrameSize();
            uint receivedWord;
            if (IsFrame32Bits())
            {
                ulong data_low = txdata[txNext.Value].Value; // Contents of the 1st entry
                ulong data_high = txdata[txNext.Value +1].Value; //Contents of the 2nd entry
                txCounter.Value -= 2;
                txNext.Value = (txNext.Value + 2) % TxFifoSize; 
                data = data_high << 16 | data_low;
                receivedWord = TransmitWhileData(data, 32);
            }
            else
            {
                data = txdata[txNext.Value].Value;
                this.Log(LogLevel.Debug, "Sending 0x{0:X} to the device, with a frame size of {1}", data, currentFrameSize);
                txCounter.Value -= 1;
                txNext.Value = (txNext.Value + 1) % TxFifoSize;  
                receivedWord = TransmitWhileData(data, currentFrameSize);
            }

            if (!cmdContinuousPSCEnable) device.FinishTransmission();
            transferComplete.Value = true; //One FIFO entry is out
            this.Log(LogLevel.Debug, "Received response 0x{0:X} from the device", receivedWord);
            EnqueueResponse(receivedWord);

            if (cmdEndOfQueue) endOfQueueSR.Value = true;
            cmdHowMuchLeft--;
            if (cmdHowMuchLeft == 0)
                commandTransferComplete.Value = true;
            IncrementTransferCounter();
            UpdateInterrupts();
            return true;
        }

        //"auto updated" SR flags
        private bool Running => !halted.Value && !moduleDisabled.Value;

        private bool TxFifoNotFull =>  txDisabled.Value ? (txCounter.Value < 1) : (txCounter.Value < TxFifoSize);

        private bool RxDrainFlag =>  rxCounter.Value > 0;

        private bool CmdFifo => txDisabled.Value ? (cmdCounter.Value < 1) : (cmdCounter.Value < CmdFifoSize);

        private uint TxFifoSize => 16;

        private uint CmdFifoSize => 16;

        private uint RxFifoSize => 16;

        // SPI_MCR Flags
        private IFlagRegisterField masterMode;
        private IFlagRegisterField rxFifoOverwriteOnOverflow;
        private IFlagRegisterField moduleDisabled;
        private IFlagRegisterField txDisabled;
        private IFlagRegisterField rxDisabled;
        private IFlagRegisterField xspi;
        private IFlagRegisterField halted;

        // SPI_TCR Flags
        private IValueRegisterField transferCount;

        // SPI_CTAR X
        private IValueRegisterField frameSize0;
        private IValueRegisterField frameSize1;
        private IValueRegisterField frameSize2;
        private IValueRegisterField frameSize3;

        // SPI_SR Flags
        private IFlagRegisterField transferComplete; // HW signals transfer is complete
        private IFlagRegisterField endOfQueueSR; //HW signals this was the last of a transfer (EOQ was set in the command)
        private IFlagRegisterField commandTransferComplete; //HW signals that this was the last of a cyclic command (end of this cmd entry)
        private IFlagRegisterField rxFifoOverflow; //HW signals that a SPI transfer is initiated while RX fifo is full
        private IFlagRegisterField txFifoInvalidWrite; //HW signals that there was a write to TX Data FIFO while there was no command - illegal
        private IValueRegisterField txCounter; //Number of entries in the FIFO
        private IValueRegisterField txNext; //Pointer to next TX FIFO entry
        private IValueRegisterField rxCounter; //Number of entried in the RX FIFO
        private IValueRegisterField rxNext; //Pointer to next RX FIFO entry

        // SPI_RSER Flags
        private IFlagRegisterField transferCompleteInterrupt;
        private IFlagRegisterField cmdFifoInterrupt;
        private IFlagRegisterField endOfQueueSRInterrupt;
        private IFlagRegisterField txFifoNotFullInterrupt;
        private IFlagRegisterField commandTransferCompleteInterrupt;
        private IFlagRegisterField rxFifoOverflowInterrupt;
        private IFlagRegisterField rxDrainFlagInterrupt;
        private IFlagRegisterField txFifoInvalidWriteInterrupt;
   
        // SPI_PUSHR (CMD) register and Flags
        WordRegister cmd_pushr_reg;

            // Command Transfer Flags - used when the command is decyphered
        bool cmdContinuousPSCEnable; // Do we call TransferComplete?
        uint cmdClockTransferAttributeSelect; //Which CTAR will be used for this command?
        bool cmdEndOfQueue; // EOQF should be set
        int cmdPCS; //Index of device to be chosen
        ISPIPeripheral device; //Chosen device

        // SPI_PUSHR (DATA) register (no flags)
        WordRegister data_pushr_reg;

        // SPI_POPR register (no flags)
        DoubleWordRegister popr_reg;

        // SPI_CTAREX Flags
        private IFlagRegisterField frameSizeExt0; 
        private IValueRegisterField dataTransferCount0;
        private IFlagRegisterField frameSizeExt1; 
        private IValueRegisterField dataTransferCount1;
        private IFlagRegisterField frameSizeExt2; 
        private IValueRegisterField dataTransferCount2;
        private IFlagRegisterField frameSizeExt3; 
        private IValueRegisterField dataTransferCount3;

        // SPI_SREX Flags
        private IValueRegisterField cmdCounter;
        private IValueRegisterField cmdNext;
        ulong cmdHowMuchLeft = 0; //Counter of number of frames to transfer with current command. Null for the initial transfer.

        //TXFifo Contents
        private readonly IValueRegisterField[] txcmd = new IValueRegisterField[16];
        private readonly IValueRegisterField[] txdata = new IValueRegisterField[16];
        private readonly IValueRegisterField[] rxdata = new IValueRegisterField[16];

        // To handle : store where?
        bool transferInProgress;
        bool inLaunchTransfer;

        private enum WRegisters
        {
            // Data to be transmitted to TX FIFO and CMD FIFO
            // One write to the command or TX part transmits this part to the appropriate fifo -> write in chunks
            // If Extended SPI not set, must be filled simulaneously
            // Does not update FIFO if module disabled
            SPI_CMD_PUSHR = 0x34, // The PUSHR register is split into 2 WORD registers since word accesses are supported
            SPI_DATA_PUSHR = 0x36,
        }

        private enum DWRegisters
        {
            // Module Configuration Register
            SPI_MCR = 0x0,
            // Transfer Count Register : number of SPI transfers made
            SPI_TCR = 0x8,
            //Defines transfer attributes (frame size, clock phase, data bit ordering...)
            // We can store several configurations and choose which one to use within the command part of the TX fifo
            // Do not write when module is running
            SPI_CTAR0 = 0xC,
            SPI_CTAR1 = 0x10,
            SPI_CTAR2 = 0x14,
            SPI_CTAR3 = 0x18,
            SPI_SR = 0x2C,
            SPI_RSER = 0x30,

            // PUSHR register can be accessed by word accesses ; moved to Word Register Collection
            SPI_POPR = 0x38,

            //Contents of the TX FIFO for debug purposes. DO NOT WRITE
            SPI_TXFR0  = 0x3C,
            SPI_TXFR1  = 0x40,
            SPI_TXFR2  = 0x44,
            SPI_TXFR3  = 0x48,
            SPI_TXFR4  = 0x4C,
            SPI_TXFR5  = 0x50,
            SPI_TXFR6  = 0x54,
            SPI_TXFR7  = 0x58,
            SPI_TXFR8  = 0x5C,
            SPI_TXFR9  = 0x60,
            SPI_TXFR10 = 0x64,
            SPI_TXFR11 = 0x68,
            SPI_TXFR12 = 0x6C,
            SPI_TXFR13 = 0x70,
            SPI_TXFR14 = 0x74,
            SPI_TXFR15 = 0x78,

            //Contents of the TX FIFO for debug purposes. DO NOT WRITE
            SPI_RXFR0  = 0x7C,
            SPI_RXFR1  = 0x80,
            SPI_RXFR2  = 0x84,
            SPI_RXFR3  = 0x88,
            SPI_RXFR4  = 0x8C,
            SPI_RXFR5  = 0x90,
            SPI_RXFR6  = 0x94,
            SPI_RXFR7  = 0x98,
            SPI_RXFR8  = 0x9C,
            SPI_RXFR9  = 0xA0,
            SPI_RXFR10 = 0xA4,
            SPI_RXFR11 = 0xA8,
            SPI_RXFR12 = 0xAC,
            SPI_RXFR13 = 0xB0,
            SPI_RXFR14 = 0xB4,
            SPI_RXFR15 = 0xB8,
            SPI_CTARE0 = 0x11C,
            SPI_CTARE1 = 0x120,
            SPI_CTARE2 = 0x124,
            SPI_CTARE3 = 0x128,
            SPI_SREX   = 0x13C
        }
    }
}