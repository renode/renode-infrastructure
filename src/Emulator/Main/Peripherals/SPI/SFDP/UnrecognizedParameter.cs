//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Peripherals.SPI.SFDP
{
    public class UnrecognizedParameter : SFDPParameter
    {
        public UnrecognizedParameter(ushort parameterId, byte[] content) : base(0, 0)
        {
            Content = content;
            _parameterId = parameterId;
        }

        public override byte ParameterIdLsb => (byte)_parameterId;

        public override byte ParameterIdMsb => (byte)(_parameterId >> 8);

        public byte[] Content { get; }

        protected override byte[] ToBytes()
        {
            return Content;
        }

        private readonly ushort _parameterId;
    }
}
