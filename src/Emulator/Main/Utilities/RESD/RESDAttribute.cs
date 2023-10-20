//
// Copyright (c) 2010-2023 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

using Antmicro.Renode.Peripherals;

namespace Antmicro.Renode.Utilities.RESD
{
    public class RESDSampleCallbackAttribute : Attribute
    {
        public RESDSampleCallbackAttribute(SampleType sampleType, uint channel = 0, RESDStreamStatus status = RESDStreamStatus.OK, string domain = null)
        {
            SampleType = sampleType;
            ChannelId = channel;
            Status = status;
            Domain = domain;
        }

        public SampleType SampleType { get; }
        public uint ChannelId { get; }
        public RESDStreamStatus Status { get; }
        public string Domain { get; }
    }

    public class OnRESDSample : RESDSampleCallbackAttribute
    {
        public OnRESDSample(SampleType sampleType, uint channel = 0, string domain = null) : base(sampleType, channel, RESDStreamStatus.OK, domain)
        {
        }
    }

    public class BeforeRESDSample : RESDSampleCallbackAttribute
    {
        public BeforeRESDSample(SampleType sampleType, uint channel = 0, string domain = null) : base(sampleType, channel, RESDStreamStatus.BeforeStream, domain)
        {
        }
    }

    public class AfterRESDSample : RESDSampleCallbackAttribute
    {
        public AfterRESDSample(SampleType sampleType, uint channel = 0, string domain = null) : base(sampleType, channel, RESDStreamStatus.AfterStream, domain)
        {
        }
    }

    public interface IUnderstandRESD : IPeripheral {}
}
