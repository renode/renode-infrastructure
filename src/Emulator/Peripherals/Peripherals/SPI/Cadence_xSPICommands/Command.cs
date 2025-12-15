//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;

using static Antmicro.Renode.Peripherals.SPI.Cadence_xSPI;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal abstract class Command
    {
        public static Command CreateCommand(Cadence_xSPI controller, CommandPayload payload)
        {
            Command command;
            switch(controller.Mode)
            {
            case ControllerMode.SoftwareTriggeredInstructionGenerator:
                command = STIGCommand.CreateSTIGCommand(controller, payload);
                break;
            case ControllerMode.AutoCommand:
                command = AutoCommand.CreateAutoCommand(controller, payload);
                break;
            default:
                controller.Log(LogLevel.Warning, "Unable to create the command, unknown controller mode 0x{0:x}", controller.Mode);
                return null;
            }

            command.Mode = controller.Mode;
            return command;
        }

        public Command(Cadence_xSPI controller)
        {
            this.controller = controller;
        }

        public void FinishTransmission()
        {
            if(TransmissionFinished)
            {
                return;
            }
            Peripheral?.FinishTransmission();
            TransmissionFinished = true;
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}: chipSelect = {ChipSelect}, invalidCommand = {InvalidCommandError}";
        }

        public abstract void Transmit();

        public bool TransmissionFinished { get; protected set; }

        public bool Completed { get; protected set; }

        public bool CRCError { get; protected set; }

        public bool BusError { get; protected set; }

        public bool InvalidCommandError { get; protected set; }

        public bool Failed => CRCError || BusError || InvalidCommandError;

        public ControllerMode Mode { get; private set; }

        public abstract uint ChipSelect { get; }

        protected void Log(LogLevel logLevel, string message, params object[] arg)
        {
            controller.Log(logLevel, message, arg);
        }

        protected ISPIPeripheral Peripheral
        {
            get
            {
                if(!isPeripheralObtained)
                {
                    isPeripheralObtained = true;
                    if(!controller.TryGetPeripheral((int)ChipSelect, out peripheral))
                    {
                        controller.Log(LogLevel.Warning, "There is no peripheral with the selected address 0x{0:x}.", ChipSelect);
                    }
                }
                return peripheral;
            }
        }

        private ISPIPeripheral peripheral;
        private bool isPeripheralObtained;
        private readonly Cadence_xSPI controller;
    }
}
