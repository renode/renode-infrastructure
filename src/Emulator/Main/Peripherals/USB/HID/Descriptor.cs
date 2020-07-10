//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Core.USB.HID
{
    public class Descriptor : DescriptorProvider
    {
        public Descriptor(ReportDescriptor reportDescriptor,
                                short classSpecification = 0,
                                byte countryCode = 0) : base(9, (byte)HID.DescriptorType.HID)
        {
            NumberOfClassDescriptors = 1;
            HID_ClassSpecification = classSpecification;
            CountryCode = countryCode;
            DescriptorType = (byte)HID.DescriptorType.Report;
            HidDescriptorLength = checked((short)reportDescriptor.DescriptorLength);
        }

        public short HID_ClassSpecification { get; }
        public byte CountryCode { get; }
        public byte NumberOfClassDescriptors { get; }
        public byte DescriptorType { get; }
        public short HidDescriptorLength { get; }

        protected override void FillDescriptor(BitStream buffer)
        {
            buffer
                .Append(HID_ClassSpecification)
                .Append(CountryCode)
                .Append(NumberOfClassDescriptors)
                .Append(DescriptorType)
                .Append(HidDescriptorLength);
        }
    }
}
