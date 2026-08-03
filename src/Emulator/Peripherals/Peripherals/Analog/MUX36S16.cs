//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Linq;

using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Sensor;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Utilities.RESD;

namespace Antmicro.Renode.Peripherals.Analog
{
    /// <summary>
    /// The MUX36S16 is birectional in reality, but for easy of use in Renode the model is split into this: 16 to 1 input MUX
    /// and a different 1 to 16 output MUX (MUX36S16_Output)
    /// </summary>
    public class MUX36S16_Input : MUX36S16_Base, IFloatingVoltageSource, IADC
    {
        public MUX36S16_Input(IMachine machine)
        {
            ADCContainer = new SimpleContainerHelper<IRESDSampleSource<VoltageSample>>(machine, this);
            newSampleCallbacks = Enumerable.Range(0, InputOutputCount).Select<int, Action<VoltageSample>>(i => (sample => OnNewSampleInput(sample, i))).ToArray();
            Reset();
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Register(IRESDSampleSource<VoltageSample> peripheral, NumberRegistrationPoint<int> channel)
        {
            this.AssertChannel(channel.Address);
            ADCContainer.Register(peripheral, channel);
            NewSample += newSampleCallbacks[channel.Address];
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Unregister(IRESDSampleSource<VoltageSample> peripheral)
        {
            foreach(var child in ADCContainer.Children.Where(x => x.Peripheral == peripheral).Select(x => x.RegistrationPoint))
            {
                NewSample -= newSampleCallbacks[child.Address];
            }
            ADCContainer.Unregister(peripheral);
        }

        public VoltageSample Sample
        {
            get
            {
                if(!enabled)
                {
                    // Output is not valid while enabled is low, so just set a default value
                    return new VoltageSample(0);
                }
                else
                {
                    if(ADCContainer.TryGetByAddress(address, out var sampleSource))
                    {
                        return sampleSource.Sample;
                    }
                    else
                    {
                        this.ErrorLog("Failed to get source number {0}", address);
                        return new VoltageSample(0);
                    }
                }
            }
        }

        public bool IsFloating
        {
            get
            {
                if(enabled)
                {
                    if(ADCContainer.TryGetByAddress(address, out var source) && source is IFloatingVoltageSource floatingSource)
                    {
                        return floatingSource.IsFloating;
                    }
                }
                return false;
            }
        }

        public int ADCChannelCount => InputOutputCount;

        public SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; }

        public event Action<VoltageSample> NewSample;

        private void OnNewSampleInput(VoltageSample sample, int channel)
        {
            // Forward the sample if the mux is enabled and configured with this channel
            if(enabled && address == channel)
            {
                NewSample?.Invoke(sample);
            }
        }

        private readonly Action<VoltageSample>[] newSampleCallbacks;
    }

    public class MUX36S16_Output : MUX36S16_Base, IADC
    {
        public MUX36S16_Output(IMachine machine)
        {
            this.machine = machine;
            ADCContainer = new SimpleContainerHelper<IRESDSampleSource<VoltageSample>>(machine, this);
            muxOutputs = Enumerable.Range(0, InputOutputCount).Select(i => new AnalogMuxOutput(this, i)).ToArray();
            Reset();
        }

        public void ConnectOutput(int sourceChannel, IADC targetPeripheral, int targetChannel)
        {
            var muxOutput = muxOutputs[sourceChannel];
            targetPeripheral.Register(muxOutput, new NumberRegistrationPoint<int>(targetChannel));
            machine.TryGetLocalName(this, out var localName);
            machine.SetLocalName(muxOutput, $"{localName}-output{sourceChannel}");
        }

        public void DisconnectOutput(int sourceChannel)
        {
            var muxOutput = muxOutputs[sourceChannel];
            foreach(var peripheral in machine.GetParentPeripherals(muxOutput))
            {
                ((IADC)peripheral).Unregister(muxOutput);
            }
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Register(IRESDSampleSource<VoltageSample> peripheral, NumberRegistrationPoint<int> channel)
        {
            this.AssertChannel(channel.Address);
            ADCContainer.Register(peripheral, channel);
            peripheral.NewSample += OnNewSample;
        }

        void IRegisterablePeripheral<IRESDSampleSource<VoltageSample>, NumberRegistrationPoint<int>>.Unregister(IRESDSampleSource<VoltageSample> peripheral)
        {
            peripheral.NewSample -= OnNewSample;
            ADCContainer.Unregister(peripheral);
        }

        public int ADCChannelCount => 1;

        public SimpleContainerHelper<IRESDSampleSource<VoltageSample>> ADCContainer { get; private set; }

        private IRESDSampleSource<VoltageSample> GetSourceFor(int channel)
        {
            if(enabled && channel == address)
            {
                if(ADCContainer.TryGetByAddress(0, out var sampleSource))
                {
                    return sampleSource;
                }
            }
            return null;
        }

        private VoltageSample GetSample(int channel)
        {
            var source = GetSourceFor(channel);
            if(source != null)
            {
                return source.Sample;
            }
            return new VoltageSample(0);
        }

        private bool GetIsFloating(int channel)
        {
            var source = GetSourceFor(channel);
            if(source != null)
            {
                return (source as IFloatingVoltageSource)?.IsFloating ?? false;
            }
            return true;
        }

        private void OnNewSample(VoltageSample sample)
        {
            // Forward the event to the active output
            if(enabled)
            {
                muxOutputs[address].InvokeNewSample(sample);
            }
        }

        private readonly AnalogMuxOutput[] muxOutputs;
        private readonly IMachine machine;

        private class AnalogMuxOutput : IFloatingVoltageSource
        {
            public AnalogMuxOutput(MUX36S16_Output parent, int index)
            {
                this.parent = parent;
                this.index = index;
            }

            public void Reset()
            {
                // Intentionally empty
            }

            public VoltageSample Sample => parent.GetSample(index);

            public bool IsFloating => parent.GetIsFloating(index);

            public event Action<VoltageSample> NewSample;

            /// </summary>
            internal void InvokeNewSample(VoltageSample sample)
            {
                NewSample.Invoke(sample);
            }

            private readonly MUX36S16_Output parent;
            private readonly int index;
        }
    }

    public abstract class MUX36S16_Base : IGPIOReceiver
    {
        public void OnGPIO(int number, bool value)
        {
            if(number < 0 || number > EnableBit)
            {
                this.ErrorLog("GPIO pin {0} is out of range of 0-{1}", number, EnableBit);
                return;
            }
            if(number < EnableBit)
            {
                BitHelper.SetBit(ref address, (byte)number, value);
            }
            else if(number == EnableBit)
            {
                enabled = value;
            }
        }

        public void Reset()
        {
            enabled = false;
            address = 0;
        }

        protected bool enabled;
        protected byte address;

        protected const int InputOutputCount = 16;
        protected const int EnableBit = 4;
    }
}
