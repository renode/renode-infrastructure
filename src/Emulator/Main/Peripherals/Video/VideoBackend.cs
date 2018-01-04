//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Video;
using Antmicro.Renode.Backends.Display;
using ELFSharp.ELF;

namespace Antmicro.Renode.Backends.Video
{
    public class VideoBackend : IAnalyzableBackend<IVideo>
    {
        public void Attach(IVideo peripheral)
        {
            Video = peripheral;
            Video.FrameRendered += HandleFrameRendered;
            Video.ConfigurationChanged += HandleConfigurationChanged;
        }
       
        private void HandleFrameRendered(byte[] frame)
        {
            if(frame != null)
            {
                Frame = frame;
            }
        }

        private void HandleConfigurationChanged(int width, int height, PixelFormat format, Endianess endianess)
        {
            Width = width;
            Height = height;
            Format = format;
            Endianess = endianess;
        }

        public int Width { get; private set; }
        public int Height { get; private set; }
        public PixelFormat Format { get; private set; }
        public Endianess Endianess { get; private set; }

        public byte[] Frame { get; private set; }

        public IVideo Video { get; private set; }
        public IAnalyzable AnalyzableElement { get { return Video; } }
    }
}

