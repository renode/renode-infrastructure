//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Exceptions;
#if !PLATFORM_WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using Antmicro.Migrant;
using Antmicro.Migrant.Hooks;
using AntShell.Terminal;
using Mono.Unix;
#endif

namespace Antmicro.Renode.Peripherals.Wireless
{
    public static class SlipRadioExtensions
    {
        public static void CreateSlipRadio(this Emulation emulation, string name, string fileName)
        {
#if !PLATFORM_WINDOWS
            emulation.ExternalsManager.AddExternal(new SlipRadio(fileName), name);
#else
            throw new RecoverableException("Creating SlipRadio is not supported on Windows.");
#endif
        }
    }

#if !PLATFORM_WINDOWS
    public class SlipRadio : ISlipRadio
    {
        public SlipRadio(string linkName)
        {
            this.linkName = linkName;
            buffer = new List<byte>();
            Initialize();
        }

        [PostDeserialization]
        private void Initialize()
        {
            ptyStream = new PtyUnixStream();
            io = new IOProvider { Backend = new StreamIOSource(ptyStream) };
            io.ByteRead += CharReceived;
            CreateSymlink(linkName);
        }

        public virtual void ReceiveFrame(byte[] frame)
        {
            EncapsulateAndSend(frame);
        }

        public void CharReceived(int value)
        {
            buffer.Add((byte)value);
            if((byte)value == END)
            {
                if(buffer.Count > 8)
                {
                    HandleFrame(buffer.ToArray());
                }
                buffer.Clear();
            }
        }

        public void Reset()
        {
            buffer.Clear();
        }

        public void Dispose()
        {
            io.Dispose();
            try
            {
                symlink.Delete();
            }
            catch(FileNotFoundException e)
            {
                throw new RecoverableException(string.Format("There was an error when removing symlink `{0}': {1}", symlink.FullName, e.Message));
            }
        }

        public event Action<IRadio, byte[]> FrameSent;

        public int Channel { get; set; }

        protected virtual void HandleFrame(byte[] frame)
        {
            var fs = FrameSent;
            if(fs != null)
            {
                fs.Invoke(this, frame);
            }
            else
            {
                this.Log(LogLevel.Warning, "FrameSent is not initialized. Am I connected to medium?");
            }
        }

        protected virtual byte[] Encapsulate(byte[] frame)
        {
            var result = new List<byte>();

            foreach(var value in frame)
            {
                switch(value)
                {
                    case END:
                        result.Add(ESC);
                        result.Add(ESC_END);
                        break;
                    case ESC:
                        result.Add(ESC);
                        result.Add(ESC_ESC);
                        break;
                    default:
                        result.Add(value);
                        break;
                }
            }
            var engine = new CRCEngine(CRCPolynomial.CRC32);
            var crc = engine.Calculate(result);
            result.AddRange(new byte[] {(byte)(crc & 0xFF), (byte)((crc >> 8) & 0xFF), (byte)((crc >> 16) & 0xFF), (byte)((crc >> 24) & 0xFF)});
            result.Add(END);
            return result.ToArray();
        }

        protected byte[] Decapsulate(byte[] frame)
        {
            var result = new List<byte>();
            bool isEscaped = false;

            foreach(var value in frame)
            {
                switch(value)
                {
                    case END:
                        return result.ToArray();
                    case ESC:
                        isEscaped = true;
                        continue;
                    case ESC_END:
                        if(isEscaped)
                        {
                            result.Add(END);
                            isEscaped = false;
                        }
                        else
                        {
                            result.Add(ESC_END);
                        }
                        break;
                    case ESC_ESC:
                        if(isEscaped)
                        {
                            result.Add(ESC);
                            isEscaped = false;
                        }
                        else
                        {
                            result.Add(ESC_ESC);
                        }
                        break;
                    default:
                        isEscaped = false;
                        result.Add(value);
                        break;
                }
            }

            Logger.Log(LogLevel.Error, "Received an unfinished frame of length {0}, dropping...", result.Count);
            return new byte[0]; //TODO or nul?
        }

        protected void EncapsulateAndSend(byte[] data)
        {
            var encoded = Encapsulate(data);
            ptyStream.Write(encoded, 0, encoded.Length);
        }

        private void CreateSymlink(string linkName)
        {
            if(File.Exists(linkName))
            {
                try
                {
                    File.Delete(linkName);
                }
                catch(Exception e)
                {
                    throw new RecoverableException(string.Format("There was an error when removing existing `{0}' symlink: {1}", linkName, e.Message));
                }
            }
            try
            {
                var slavePtyFile = new UnixFileInfo(ptyStream.SlaveName);
                symlink = slavePtyFile.CreateSymbolicLink(linkName);
            }
            catch(Exception e)
            {
                throw new RecoverableException(string.Format("There was an error when when creating a symlink `{0}': {1}", linkName, e.Message));
            }
            Logger.Log(LogLevel.Info, "Created a Slip Radio pty connection to {0}", linkName);
        }

        [Transient]
        protected PtyUnixStream ptyStream;
        [Transient]
        private IOProvider io;

        private readonly List<byte> buffer;
        private readonly string linkName;
        private UnixSymbolicLinkInfo symlink;

        private const byte END = 0xC0;
        private const byte ESC = 0xDB;
        private const byte ESC_END = 0xDC;
        private const byte ESC_ESC = 0xDD;
    }
#endif
}
