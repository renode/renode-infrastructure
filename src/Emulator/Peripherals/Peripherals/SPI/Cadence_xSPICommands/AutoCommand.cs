//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using static Antmicro.Renode.Peripherals.SPI.Cadence_xSPI;

namespace Antmicro.Renode.Peripherals.SPI.Cadence_xSPICommands
{
    internal abstract class AutoCommand : Command
    {
        static public AutoCommand CreateAutoCommand(Cadence_xSPI controller, CommandPayload payload)
        {
            var commandMode = DecodeCommandMode(payload);
            switch(commandMode)
            {
                case CommandMode.PIO:
                    return PIOCommand.CreatePIOCommand(controller, payload);
                default:
                    controller.Log(LogLevel.Warning, "Unable to create the auto command, unknown command mode 0x{0:x}", commandMode);
                    return null;
            }
        }

        public AutoCommand(Cadence_xSPI controller, CommandPayload payload) : base(controller)
        {
            mode = DecodeCommandMode(payload);
            ChipSelect = BitHelper.GetValue(payload[0], 20, 3);
        }

        public override string ToString()
        {
            return $"{base.ToString()}, commandMode = {mode}";
        }

        public override uint ChipSelect { get; }

        protected CommandMode mode;

        static private CommandMode DecodeCommandMode(CommandPayload payload)
        {
            return (CommandMode)BitHelper.GetValue(payload[0], 30, 2);
        }

        protected enum CommandMode
        {
            PIO = 0x1
        }
    }
}
