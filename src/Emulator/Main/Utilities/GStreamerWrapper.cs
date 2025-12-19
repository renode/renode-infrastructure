//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Runtime.InteropServices;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

#if PLATFORM_LINUX && NET
using Gst;
#endif

namespace Antmicro.Renode.Peripherals.Video
{
    public static class GStreamerWrapper
    {
#if PLATFORM_LINUX && NET
        public static Pipeline CreatePipeline(string pipelineStr)
#else
        public static object CreatePipeline(string pipelineStr)
#endif
        {
#if PLATFORM_LINUX && NET
            try
            {
                EnsureInitialized();
                return Functions.ParseLaunch(pipelineStr) as Pipeline;
            }
            catch(DllNotFoundException)
            {
                Logger.Warning("Unable to load shared library for GStreamer");
                return null;
            }
#else
            return null;
#endif
        }

#if PLATFORM_LINUX && NET
        public static void SetBufferDimensions(Gst.Buffer buffer, uint width, uint height, uint pitch)
        {
            try
            {
                // There is no binding for this function in GirCore yet so we have to call it manually
                // with 4-element arrays
                var planeOffsets = new UIntPtr[] { /* U */ 0, /* V */ (UIntPtr)pitch * height, 0, 0 };
                var pitches = new uint[] { pitch, pitch, 0, 0 };
                BufferAddVideoMetaFull(buffer.Handle, VideoFrameFlags.None, VideoFormat.Nv12, width, height, 2, planeOffsets, ref pitches[0]);
            }
            catch(DllNotFoundException)
            {
                Logger.Warning("Unable to load shared library for GStreamer");
            }
        }
#endif

        public static string H264Encoder
        {
            get
            {
                EnsureInitialized();
                return h264Encoder;
            }
        }

        public static string H265Encoder
        {
            get
            {
                EnsureInitialized();
                return h265Encoder;
            }
        }

#if PLATFORM_LINUX && NET
        [DllImport("libgstvideo-1.0.so.0", EntryPoint = "gst_buffer_add_video_meta_full")]
        private static extern UIntPtr BufferAddVideoMetaFull(Gst.Internal.BufferHandle buffer, VideoFrameFlags flags, VideoFormat format, uint width, uint height, uint nPlanes, UIntPtr[] offset, ref uint stride);
#endif

        private static void EnsureInitialized()
        {
            lock(locker)
            {
                if(initialized)
                {
                    return;
                }
                try
                {
                    Initialize();
                    initialized = true;
                }
                catch(DllNotFoundException)
                {
                    Logger.Warning("Unable to load shared library for GStreamer");
                    initialized = false;
                }
            }
        }

        private static void Initialize()
        {
#if PLATFORM_LINUX && NET
            Module.Initialize();
            GstApp.Module.Initialize();
            var a = Array.Empty<string>();
            Gst.Functions.Init(ref a);

            h264Encoder = FindEncoder("H.264", H264EncoderPriority);
            h265Encoder = FindEncoder("H.265", H265EncoderPriority);
#endif
        }

        private static string FindEncoder(string codec, string[] priority)
        {
#if PLATFORM_LINUX && NET
            foreach(var type in priority)
            {
                using(var factory = ElementFactory.Find(type.Split(' ')[0]))
                {
                    if(factory != null)
                    {
                        return type;
                    }
                }
            }
#endif
            Logger.Warning($"{codec} encoder was requested but none of {Misc.PrettyPrintCollection(priority)} were available");
            return null;
        }

        private static bool initialized;
        private static readonly object locker = new object();

        private static string h264Encoder;
        private static string h265Encoder;

        // TODO: Using HW codecs causes buffering (and a hang)
        private static readonly string[] H264EncoderPriority = new string[] {"openh264enc complexity=low deblocking=off scene-change-detection=false", "vah264lpenc target-usage=7 b-frames=0 ref-frames=1", "vah264enc target-usage=7 b-frames=0 ref-frames=1"};
        private static readonly string[] H265EncoderPriority = new string[] {"vah265lpenc target-usage=7 b-frames=0 ref-frames=1", "vah265enc target-usage=7 b-frames=0 ref-frames=1"};

        /* GstVideo */
        [Flags]
        public enum VideoFrameFlags : uint
        {
            None = 0x0,
            Interlaced = 0x1,
            Tff = 0x2,
            Rff = 0x4,
            OneField = 0x8,
            BottomField = OneField,
            TopField = Tff | OneField,
            MultipleView = 0x10,
            FirstInBundle = 0x20,
        }

        public enum VideoFormat
        {
            Unknown = 0,
            Encoded = 1,
            I420 = 2,
            Nv12 = 23,
            Gray8 = 25,
        }
    }
}
