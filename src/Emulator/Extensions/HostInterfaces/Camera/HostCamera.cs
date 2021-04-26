//
// Copyright (c) 2010-2021 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.IO;
using Antmicro.Renode.Core;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Exceptions;
using BitMiracle.LibJpeg.Classic;

using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;

namespace Antmicro.Renode.HostInterfaces.Camera
{
    public static class HostCameraExtensions
    {
        public static void AddExternalCamera(this Emulation emulation, string device, string name = "camera")
        {
            var camera = new HostCamera(device);

            emulation.HostMachine.AddHostMachineElement(camera, name);
        }
    }

    public class HostCamera : IHostMachineElement
    {
        public HostCamera(string device)
        {
#if !PLATFORM_LINUX
            throw new RecoverableException("Host camera integration is currently available on Linux only");
#else
            this.device = device;
            InitCamera();
#endif
        }

        public byte[] GrabFrame()
        {
            var frame = VideoCapturer.GrabSingleFrame();
            if(ForcedScaleDownFactor != -1 || Quality != -1)
            {
                var decompressed = DecompressJpgToRaw(frame);
                frame = CompressRawToJpeg(decompressed.Data, decompressed.Width, decompressed.Height, ForcedScaleDownFactor, Quality);
            }

            lastFrame = frame;
            return lastFrame;
        }

        public RawImageData GetLastFrame(bool grab = false)
        {
            if(grab)
            {
                GrabFrame();
            }   

            if(lastFrame == null)
            {
                return new RawImageData(new byte[0], 0, 0);
            }

            var decompressed = DecompressJpgToRaw(lastFrame);
            var converter = PixelManipulationTools.GetConverter(PixelFormat.RGB888, ELFSharp.ELF.Endianess.BigEndian, RawImageData.PixelFormat, ELFSharp.ELF.Endianess.BigEndian); 
            var result = new byte[decompressed.Width * decompressed.Height * RawImageData.PixelFormat.GetColorDepth()];
            converter.Convert(decompressed.Data, ref result);

            return new RawImageData(result, decompressed.Width, decompressed.Height);
        }

        public void SaveLastFrame(string path, bool grab = false)
        {
            if(grab)
            {
                GrabFrame();
            }

            if(lastFrame == null)
            {
                throw new RecoverableException("There is no frame to save. Please grab some and try again");
            }

            try
            {
                File.WriteAllBytes(path, lastFrame);
            }
            catch(Exception e)
            {
                throw new RecoverableException($"There was a problem when saving the last frame: {e.Message}");
            }
        }

        public void SetImageSize(int width, int height)
        {
            VideoCapturer.SetImageSize(width, height);
        }

        public int Quality { get; set; } = -1;

        public int ForcedScaleDownFactor { get; set; } = -1;

        [PostDeserialization]
        private void InitCamera()
        {
            if(!VideoCapturer.Start(device, this))
            {
                throw new RecoverableException("Couldn't initialize host camera - see logs for details.");
            }
        }

        // the algorithm flow is based on:
        // https://bitmiracle.github.io/libjpeg.net/help/articles/KB/decompression-details.html
        private DecompressionResult DecompressJpgToRaw(byte[] image)
        {
            var cinfo = new jpeg_decompress_struct(new jpeg_error_mgr());

            using(var memoryStream = new MemoryStream(image))
            {
                cinfo.jpeg_stdio_src(memoryStream);
                cinfo.jpeg_read_header(true);

                cinfo.Out_color_space = J_COLOR_SPACE.JCS_RGB;

                cinfo.jpeg_start_decompress();

                // there are 3 components: R, G, B
                var rowStride = 3 * cinfo.Output_width;
                var result = new byte[rowStride * cinfo.Output_height];
                var resultOffset = 0;

                var buffer = new byte[1][];
                buffer[0] = new byte[rowStride];

                while(cinfo.Output_scanline < cinfo.Output_height)
                {
                    var ct = cinfo.jpeg_read_scanlines(buffer, 1);
                    if(ct > 0)
                    {
                        Array.Copy(buffer[0], 0, result, resultOffset, buffer[0].Length);
                        resultOffset += buffer[0].Length;
                    }
                }

                cinfo.jpeg_finish_decompress();
                return new DecompressionResult(result, cinfo.Output_width, cinfo.Output_height);
            }
        }

        // the algorithm flow is based on:
        // https://bitmiracle.github.io/libjpeg.net/help/articles/KB/compression-details.html
        private static byte[] CompressRawToJpeg(byte[] input, int width, int height, int scale, int quality)
        {
            jpeg_error_mgr errorManager = new jpeg_error_mgr();
            jpeg_compress_struct cinfo = new jpeg_compress_struct(errorManager);

            var memoryStream = new MemoryStream();
            cinfo.jpeg_stdio_dest(memoryStream);

            if(scale == -1)
            {
                scale = 1;
            }

            cinfo.Image_width = width / scale;
            cinfo.Image_height = height / scale;
            cinfo.Input_components = 3;
            cinfo.In_color_space = J_COLOR_SPACE.JCS_RGB;
            cinfo.jpeg_set_defaults();

            if(quality != -1)
            {
                cinfo.jpeg_set_quality(quality, true);
            }

            cinfo.jpeg_start_compress(true);

            int row_stride = cinfo.Image_width * 3; // physical row width in buffer
            byte[][] rowData = new byte[1][]; // single row
            rowData[0] = new byte[row_stride];

            int inputOffset = 0;

            while(cinfo.Next_scanline < cinfo.Image_height)
            {
                for(int i = 0; i < rowData[0].Length; i += 3)
                {
                    rowData[0][i] = input[inputOffset];
                    rowData[0][i + 1] = input[inputOffset + 1];
                    rowData[0][i + 2] = input[inputOffset + 2];

                    inputOffset += 3 * scale;
                }
                inputOffset += 3 * (scale - 1) * width;

                cinfo.jpeg_write_scanlines(rowData, 1);
            }

            cinfo.jpeg_finish_compress();

            var result = memoryStream.ToArray();
            memoryStream.Close();
            return result;
        }

        private byte[] lastFrame;

        private readonly string device;

        private struct DecompressionResult
        {
            public DecompressionResult(byte[] data, int width, int height)
            {
                Data = data;
                Width = width;
                Height = height;
            }

            public byte[] Data;
            public int Width;
            public int Height;
        }   
    }
}
