//
// Copyright (c) 2010-2019 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.Utilities
{
    public class BitBangHelper
    {
        public BitBangHelper(int width, bool outputMsbFirst = false, IEmulationElement loggingParent = null)
        {
            outputDecoder = new Decoder(width, outputMsbFirst, loggingParent);
            inputEncoder = new Encoder(width, loggingParent);

            this.loggingParent = loggingParent;
            Reset();
        }

        public void ResetOutput()
        {
            outputDecoder.Reset();
        }

        public bool Update(uint encodedSignals, int dataBit, int clockBit, bool dataEnabled = true)
        {
            var clockSignal = (encodedSignals & (1 << clockBit)) != 0;
            var dataSignal = (encodedSignals & (1 << dataBit)) != 0;

            return Update(clockSignal, dataSignal, dataEnabled);
        }

        public bool Update(bool clockSignal, bool dataSignal, bool dataEnabled = true)
        {
            var result = false;
            // clock rising
            var tickDetected = (clockSignal && !previousClockSignal);
            if(tickDetected)
            {
                loggingParent?.Log(LogLevel.Noisy, "Tick detected");

                inputEncoder.Tick();
                if(dataEnabled)
                {
                    result = outputDecoder.Tick(dataSignal);
                }
            }

            previousClockSignal = clockSignal;
            return result;
        }

        public void SetInputBuffer(uint data)
        {
            inputEncoder.Encode(data);
        }

        public void Reset()
        {
            inputEncoder.Reset();
            outputDecoder.Reset();

            previousClockSignal = false;
        }

        public uint DecodedOutput => outputDecoder.DecodedData;

        public bool EncodedInput => inputEncoder.CurrentBit;

        private bool previousClockSignal;

        private readonly Decoder outputDecoder;
        private readonly Encoder inputEncoder;
        private readonly IEmulationElement loggingParent;

        public class Encoder
        {
            public Encoder(int width, IEmulationElement loggingParent = null)
            {
                this.loggingParent = loggingParent;
                buffer = new bool[width];
                Reset();
            }

            public void Tick()
            {
                if(bufferPosition >= 0)
                {
                    bufferPosition--;
                }
            }

            public void Encode(uint data)
            {
                var dataBits = BitHelper.GetBits(data);
                Array.Copy(dataBits, 0, buffer, 0, buffer.Length);
                bufferPosition = buffer.Length;
            }

            public void Reset()
            {
                bufferPosition = -1;
            }

            public bool CurrentBit
            {
                get
                {
                    if(bufferPosition < 0 || bufferPosition >= buffer.Length)
                    {
                        loggingParent?.Log(LogLevel.Warning, "Trying to read bit, but the buffer is empty");
                        return false;
                    }

                    return buffer[bufferPosition];
                }
            }

            private int bufferPosition;

            private readonly bool[] buffer;
            private readonly IEmulationElement loggingParent;
        }

        public class Decoder
        {
            public Decoder(int width, bool msbFirst = false, IEmulationElement loggingParent = null)
            {
                this.loggingParent = loggingParent;
                this.msbFirst = msbFirst;
                buffer = new bool[width];

                Reset();
            }

            public bool Tick(bool dataSignal)
            {
                loggingParent?.Log(LogLevel.Noisy, "Latching bit #{0}, value: {1}", bufferPosition, dataSignal);

                buffer[bufferPosition] = dataSignal;
                bufferPosition += msbFirst
                    ? -1
                    : 1;

                if(bufferPosition == buffer.Length || bufferPosition == -1)
                {
                    ResetBuffer();
                    DecodedData = BitHelper.GetValueFromBitsArray(buffer);
                    return true;
                }

                return false;
            }

            public void Reset()
            {
                ResetBuffer();
                DecodedData = 0;
            }

            public uint DecodedData { get; private set; }

            private void ResetBuffer()
            {
                bufferPosition = msbFirst
                    ? buffer.Length - 1
                    : 0;
            }

            private int bufferPosition;

            private readonly bool[] buffer;
            private readonly bool msbFirst;
            private readonly IEmulationElement loggingParent;
        }
    }
}
