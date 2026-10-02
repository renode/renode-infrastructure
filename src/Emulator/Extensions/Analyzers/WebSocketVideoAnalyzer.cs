//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;

using Antmicro.Migrant;
using Antmicro.Renode.Backends.Display;
using Antmicro.Renode.Backends.Video;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Peripherals.Video;
using Antmicro.Renode.Utilities;

using ELFSharp.ELF;

namespace Antmicro.Renode.Analyzers
{
    // Wire format (little-endian), one binary WebSocket message each:
    //   Config: [u8 1][u16 width][u16 height][u8 tileSize][u8 n][n bytes: ASCII source PixelFormat name]
    //   Frame:  [u8 2][raw deflate: dirty tile bitmap, then RGBA8888 pixels of each dirty tile]
    //     bitmap - 1 bit per tile (LSB first), tiles in row-major order
    //     tile   - row-major, clipped at the right and bottom edges of the display
    //   Stats:  [u8 3][f32 frames rendered by the peripheral per host second]
    [Transient]
    public class WebSocketVideoAnalyzer : BasicPeripheralBackendAnalyzer<VideoBackend>, IExternal, IDisposable
    {
        public override void AttachTo(VideoBackend backend)
        {
            base.AttachTo(backend);
            video = backend.Video;
            displayNumber = Interlocked.Increment(ref DisplayCount);
            server = new WebSocketServerProvider($"/display/{displayNumber}", true);
            server.NewConnection += HandleNewConnection;
            server.Start();

            video.ConfigurationChanged += HandleConfigurationChanged;
            video.FrameRendered += HandleFrameRendered;
            if(backend.Frame != null)
            {
                HandleFrameRendered(backend.Frame);
            }

            statsWindowStart = Stopwatch.GetTimestamp();
            flushTimer = new Timer(FlushTick, null, FlushIntervalMs, FlushIntervalMs);
        }

        public override void Show()
        {
        }

        public override void Hide()
        {
        }

        public override void Clear()
        {
        }

        public void Dispose()
        {
            if(video != null)
            {
                video.ConfigurationChanged -= HandleConfigurationChanged;
                video.FrameRendered -= HandleFrameRendered;
            }
            flushTimer?.Dispose();
            server?.Dispose();
        }

        public int GetDisplayNumber()
        {
            return displayNumber;
        }

        public IVideo Video => video;

        private static byte[] BuildStatsMessage(float framesPerSecond)
        {
            var message = new byte[5];
            message[0] = MessageTypeStats;
            BinaryPrimitives.WriteSingleLittleEndian(message.AsSpan(1), framesPerSecond);
            return message;
        }

        private static int DisplayCount = 0;

        private void HandleNewConnection(WebSocketConnection sender, List<string> extraSegments)
        {
            lock(sync)
            {
                resyncPending = true;
            }
        }

        private void HandleConfigurationChanged(int width, int height, PixelFormat format, Endianess endianess)
        {
            lock(sync)
            {
                var isValid = width > 0 && height > 0;
                configWidth = width;
                configHeight = height;
                configFormat = format;
                latestFrame = isValid ? new byte[format.GetByteCount((ulong)(width * height))] : null;
                converter = isValid
                    ? PixelManipulationTools.GetConverter(format, endianess, PixelFormat.RGBA8888, Endianess.BigEndian, width, height)
                    : null;
                frameChanged = false;
                resyncPending = true;
            }
        }

        private void HandleFrameRendered(byte[] frame)
        {
            lock(sync)
            {
                if(frame == null || latestFrame == null || frame.Length < latestFrame.Length)
                {
                    return;
                }
                Buffer.BlockCopy(frame, 0, latestFrame, 0, latestFrame.Length);
                frameChanged = true;
                framesRendered++;
            }
        }

        private void FlushTick(object _)
        {
            if(!Monitor.TryEnter(flushGuard))
            {
                return;
            }
            try
            {
                Flush();
            }
            finally
            {
                Monitor.Exit(flushGuard);
            }
        }

        private void Flush()
        {
            if(server.ConnectionsCount == 0)
            {
                return;
            }

            if(TryTakeFrame(out var resync))
            {
                if(resync)
                {
                    tileSize = Math.Clamp(Math.Min(frameWidth, frameHeight) / 16 / 8 * 8, MinTileSize, MaxTileSize);
                    sentRgba = null;
                    server.Broadcast(BuildConfigMessage());
                }
                frameConverter.Convert(frameRaw, ref frameRgba);
                var frameMessage = BuildFrameMessage();
                if(frameMessage != null)
                {
                    server.Broadcast(frameMessage);
#if DEBUG
                    sentFrames++;
                    sentBytes += frameMessage.Length;
#endif
                }
            }

            if(TryTakeFramesPerSecond(out var framesPerSecond))
            {
                server.Broadcast(BuildStatsMessage(framesPerSecond));
#if DEBUG
                video.Log(LogLevel.Debug, "WebSocket stream over the last host second: rendered {0:F1} frames, sent {1} frames ({2} tiles, {3:F1} KiB)",
                    framesPerSecond, sentFrames, sentTiles, sentBytes / 1024.0);
                sentFrames = 0;
                sentTiles = 0;
                sentBytes = 0;
#endif
            }
        }

        private bool TryTakeFrame(out bool resync)
        {
            lock(sync)
            {
                resync = resyncPending;
                if(latestFrame == null || !(frameChanged || resyncPending))
                {
                    return false;
                }

                if(resyncPending)
                {
                    frameWidth = configWidth;
                    frameHeight = configHeight;
                    frameFormat = configFormat;
                    frameConverter = converter;
                    frameRaw = new byte[latestFrame.Length];
                    frameRgba = new byte[frameWidth * frameHeight * BytesPerPixel];
                }
                Buffer.BlockCopy(latestFrame, 0, frameRaw, 0, latestFrame.Length);
                frameChanged = false;
                resyncPending = false;
                return true;
            }
        }

        private bool TryTakeFramesPerSecond(out float framesPerSecond)
        {
            lock(sync)
            {
                var now = Stopwatch.GetTimestamp();
                var elapsedSeconds = (double)(now - statsWindowStart) / Stopwatch.Frequency;
                framesPerSecond = (float)(framesRendered / elapsedSeconds);
                if(elapsedSeconds < StatsIntervalSeconds)
                {
                    return false;
                }
                framesRendered = 0;
                statsWindowStart = now;
                return true;
            }
        }

        private byte[] BuildConfigMessage()
        {
            var formatName = Encoding.ASCII.GetBytes(frameFormat.ToString());
            var message = new byte[7 + formatName.Length];
            message[0] = MessageTypeConfig;
            BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(1), (ushort)frameWidth);
            BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(3), (ushort)frameHeight);
            message[5] = (byte)tileSize;
            message[6] = (byte)formatName.Length;
            Buffer.BlockCopy(formatName, 0, message, 7, formatName.Length);
            return message;
        }

        private byte[] BuildFrameMessage()
        {
            var tilesX = (frameWidth + tileSize - 1) / tileSize;
            var tilesY = (frameHeight + tileSize - 1) / tileSize;
            var stride = frameWidth * BytesPerPixel;
            var dirtyBitmap = new byte[(tilesX * tilesY + 7) / 8];
            var dirtyTiles = 0;

            for(var tile = 0; tile < tilesX * tilesY; tile++)
            {
                if(sentRgba == null || IsTileDirty(tile % tilesX, tile / tilesX, stride))
                {
                    dirtyBitmap[tile / 8] |= (byte)(1 << (tile % 8));
                    dirtyTiles++;
                }
            }

            if(dirtyTiles == 0)
            {
                return null;
            }
#if DEBUG
            sentTiles += dirtyTiles;
#endif

            using(var stream = new MemoryStream())
            {
                stream.WriteByte(MessageTypeFrame);
                using(var deflate = new DeflateStream(stream, CompressionLevel.Fastest, leaveOpen: true))
                {
                    deflate.Write(dirtyBitmap, 0, dirtyBitmap.Length);
                    for(var tile = 0; tile < tilesX * tilesY; tile++)
                    {
                        if((dirtyBitmap[tile / 8] & (1 << (tile % 8))) == 0)
                        {
                            continue;
                        }
                        GetTileBounds(tile % tilesX, tile / tilesX, out var x, out var y, out var width, out var height);
                        // Sending tiles XORed with sentRgba would shrink partial updates further,
                        // but a client that loses sync would then need periodic full keyframes
                        for(var row = y; row < y + height; row++)
                        {
                            deflate.Write(frameRgba, row * stride + x * BytesPerPixel, width * BytesPerPixel);
                        }
                    }
                }

                sentRgba ??= new byte[frameRgba.Length];
                Buffer.BlockCopy(frameRgba, 0, sentRgba, 0, frameRgba.Length);
                return stream.ToArray();
            }
        }

        private bool IsTileDirty(int tileX, int tileY, int stride)
        {
            GetTileBounds(tileX, tileY, out var x, out var y, out var width, out var height);
            for(var row = y; row < y + height; row++)
            {
                var offset = row * stride + x * BytesPerPixel;
                var length = width * BytesPerPixel;
                if(!frameRgba.AsSpan(offset, length).SequenceEqual(sentRgba.AsSpan(offset, length)))
                {
                    return true;
                }
            }
            return false;
        }

        private void GetTileBounds(int tileX, int tileY, out int x, out int y, out int width, out int height)
        {
            x = tileX * tileSize;
            y = tileY * tileSize;
            width = Math.Min(tileSize, frameWidth - x);
            height = Math.Min(tileSize, frameHeight - y);
        }

        private int displayNumber;
        private IVideo video;
        private WebSocketServerProvider server;
        private Timer flushTimer;

        private int configWidth;
        private int configHeight;
        private PixelFormat configFormat;
        private IPixelConverter converter;
        private byte[] latestFrame;
        private bool frameChanged;
        private bool resyncPending;
        private int framesRendered;
        private long statsWindowStart;

        private int frameWidth;
        private int frameHeight;
        private PixelFormat frameFormat;
        private IPixelConverter frameConverter;
        private byte[] frameRaw;
        private byte[] frameRgba;
        private byte[] sentRgba;
        private int tileSize;
#if DEBUG
        private int sentFrames;
        private int sentTiles;
        private long sentBytes;
#endif

        private readonly object sync = new object();
        private readonly object flushGuard = new object();

        private const byte MessageTypeConfig = 1;
        private const byte MessageTypeFrame = 2;
        private const byte MessageTypeStats = 3;
        private const int BytesPerPixel = 4;
        private const int MinTileSize = 8;
        private const int MaxTileSize = 64;
        private const int FlushIntervalMs = 33;
        private const double StatsIntervalSeconds = 1.0;
    }
}
