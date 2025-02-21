//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.Linq;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.CPU
{
    public class TraceWriterBuilder
    {
        public TraceWriterBuilder(TranslationCPU cpu, SequencedFilePath path, TraceFormat format, bool isBinary, bool compress)
        {
            // SequencedFilePath ensures that file in given path doesn't exist
            this.cpu = cpu;
            this.path = path;
            this.format = format;
            this.isBinary = isBinary;
            this.compress = compress;

            if(!AreArgumentsValid())
            {
                throw new RecoverableException($"Tracing don't support {(this.isBinary ? "binary" : "text")} output file with the '{this.format}' formatting.");
            }
        }

        public string Path => path;

        public TraceWriter CreateWriter()
        {
            if(format == TraceFormat.TraceBasedModel)
            {
                return new TraceBasedModelFlatBufferWriter(cpu, path, format, compress);
            }
            if(isBinary)
            {
                return new TraceBinaryWriter(cpu, path, format, compress);
            }
            else
            {
                return new TraceTextWriter(cpu, path, format, compress);
            }
        }

        private bool AreArgumentsValid()
        {
            if(format == TraceFormat.TraceBasedModel)
            {
                return true;
            }
            if(isBinary)
            {
                return TraceBinaryWriter.SupportedFormats.Contains(this.format);
            }
            else
            {
                return TraceTextWriter.SupportedFormats.Contains(this.format);
            }
        }

        private readonly TranslationCPU cpu;
        private readonly string path;
        private readonly TraceFormat format;
        private readonly bool isBinary;
        private readonly bool compress;
    }
}
