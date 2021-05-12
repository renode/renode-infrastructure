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
            if(ForcedScaleDownFactor != 1 || Quality != -1 || cropToSize != null)
            {
                var decompressed = DecompressJpgToRaw(frame);
                frame = CompressRawToJpeg(decompressed.Data, decompressed.Width, decompressed.Height, ForcedScaleDownFactor, Quality, cropToSize);
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
            var result = VideoCapturer.SetImageSize(width, height);
            if(result == null)
            {
                throw new RecoverableException("There was an error when setting image size. See log for details");
            }

            if(result.Item1 != width || result.Item2 != height)
            {
                // image returned from the video capturer will not match the expected size precisely,
                // so we'll need to recompress and crop it manually
                cropToSize = Tuple.Create(width, height);
            }
            else
            {
                // image returned from the video capturer will be of the expected size,
                // so there is no need for manual cropping
                cropToSize = null;
            }
        }

        // this is to manually override the quality;
        // can be used to reduce the size of the returned JPEG image
        public int Quality { get; set; } = -1;

        // this is to manually scale the image down;
        // can be used to reduce the size of the returned JPEG image
        public int ForcedScaleDownFactor { get; set; } = 1;

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
        private static byte[] CompressRawToJpeg(byte[] input, int width, int height, int scale, int quality, Tuple<int, int> crop)
        {
            jpeg_error_mgr errorManager = new jpeg_error_mgr();
            jpeg_compress_struct cinfo = new jpeg_compress_struct(errorManager);

            var memoryStream = new MemoryStream();
            cinfo.jpeg_stdio_dest(memoryStream);

            var widthToSkip = 0;
            var widthToSkipFront = 0;
            var widthToSkipBack = 0;

            if(crop != null && width > crop.Item1)
            {
                widthToSkip = (width - crop.Item1);
                widthToSkipFront = widthToSkip / 2;
                widthToSkipBack = widthToSkip - widthToSkipFront; // to handle odd 'widthToSkip' values
            }

            var heightToSkip = 0;
            var heightToSkipTop = 0;

            if(crop != null && height > crop.Item2)
            {
                heightToSkip = (height - crop.Item2);
                heightToSkipTop = heightToSkip / 2;
            }

            cinfo.Image_width = (width - widthToSkip) / scale;
            cinfo.Image_height = (height - heightToSkip) / scale;
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

            int inputOffset = heightToSkipTop * width;

            while(cinfo.Next_scanline < cinfo.Image_height)
            {
                // crop pixels at the beginning of the line
                inputOffset += 3 * widthToSkipFront;

                for(int i = 0; i < rowData[0].Length - 2; i += 3)
                {
                    rowData[0][i] = input[inputOffset];
                    rowData[0][i + 1] = input[inputOffset + 1];
                    rowData[0][i + 2] = input[inputOffset + 2];

                    inputOffset += 3 * scale;
                }

                // crop pixels at the end of the line
                inputOffset += 3 * widthToSkipBack;

                // drop some lines due to scaling
                inputOffset += 3 * (scale - 1) * width;

                cinfo.jpeg_write_scanlines(rowData, 1);
            }

            cinfo.jpeg_finish_compress();

            var result = memoryStream.ToArray();
            memoryStream.Close();
            return result;
        }

        private byte[] lastFrame;
        private Tuple<int, int> cropToSize;

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
