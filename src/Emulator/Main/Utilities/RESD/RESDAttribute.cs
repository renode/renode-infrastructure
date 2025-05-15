//
// Copyright (c) 2010-2025 Antmicro
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
        public RESDSampleCallbackAttribute(SampleType sampleType, uint channel, RESDStreamStatus status = RESDStreamStatus.OK, string domain = null)
            : this(sampleType, status, domain)
        {
            ChannelId = channel;
        }

        public RESDSampleCallbackAttribute(SampleType sampleType, RESDStreamStatus status = RESDStreamStatus.OK, string domain = null)
        {
            SampleType = sampleType;
            ChannelId = null;
            Status = status;
            Domain = domain;
        }

        public SampleType SampleType { get; }
        public uint? ChannelId { get; }
        public RESDStreamStatus Status { get; }
        public string Domain { get; }
    }

    public class OnRESDSample : RESDSampleCallbackAttribute
    {
        public OnRESDSample(SampleType sampleType, uint channel, string domain = null) : base(sampleType, channel, RESDStreamStatus.OK, domain)
        {
        }

        public OnRESDSample(SampleType sampleType, string domain = null) : base(sampleType, RESDStreamStatus.OK, domain)
        {
        }
    }

    public class BeforeRESDSample : RESDSampleCallbackAttribute
    {
        public BeforeRESDSample(SampleType sampleType, uint channel, string domain = null) : base(sampleType, channel, RESDStreamStatus.BeforeStream, domain)
        {
        }

        public BeforeRESDSample(SampleType sampleType, string domain = null) : base(sampleType, RESDStreamStatus.BeforeStream, domain)
        {
        }
    }

    public class AfterRESDSample : RESDSampleCallbackAttribute
    {
        public AfterRESDSample(SampleType sampleType, uint channel, string domain = null) : base(sampleType, channel, RESDStreamStatus.AfterStream, domain)
        {
        }

        public AfterRESDSample(SampleType sampleType, string domain = null) : base(sampleType, RESDStreamStatus.AfterStream, domain)
        {
        }
    }

    public interface IUnderstandRESD : IPeripheral {}
}
