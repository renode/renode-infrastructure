//
// Copyright (c) 2010-2022 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals;
using System.IO;
using Dynamitey;
using System.Text;
using System.Runtime.InteropServices;
using System.Linq.Expressions;
using System.Drawing;
using Antmicro.Renode.Network;
using System.Diagnostics;
using Antmicro.Renode.Core.Structure.Registers;
using System.Threading;
using Antmicro.Renode.Debugging;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Utilities
{
    public static class Misc
    {
        //TODO: isn't it obsolete?
        //TODO: what if memory_size should be long?
        public static List<UInt32> CreateAtags(string bootargs, uint memorySize)
        {
            var atags = new List<UInt32>
                            {
                                5u,
                                0x54410001u,
                                1u,
                                0x1000u,
                                0u,
                                4u,
                                0x54410002u,
                                memorySize,
                                0u,
                                (uint)((bootargs.Length >> 2) + 3),
                                0x54410009u
                            };

            //TODO: should be padded
            var ascii = new ASCIIEncoding();
            var bootargsByte = new List<byte>();
            bootargsByte.AddRange(ascii.GetBytes(bootargs));
            int i;
            if((bootargs.Length % 4) != 0)
            {
                for(i = 0; i < (4 - (bootargs.Length%4)); i++)
                {
                    bootargsByte.Add(0); // pad with zeros
                }
            }
            for(i = 0; i < bootargsByte.Count; i += 4)
            {
                atags.Add(BitConverter.ToUInt32(bootargsByte.ToArray(), i));
            }
            atags.Add(0u);

            atags.Add(0u); // ATAG_NONE
            return atags;
        }

        public static bool IsPeripheral(object o)
        {
            return o is IPeripheral;
        }

        public static bool IsPythonObject(object o)
        {
            return o.GetType().GetFields().Any(x => x.Name == ".class");
        }

        public static bool IsPythonType(Type t)
        {
            return t.GetFields().Any(x => x.Name == ".class");
        }

        public static string GetPythonName(object o)
        {
            var cls = Dynamic.InvokeGet(o, ".class");
            return cls.__name__;
        }

        public static IEnumerable<MethodInfo> GetAllMethods(this Type t, bool recursive = true)
        {
            if(t == null)
            {
                return Enumerable.Empty<MethodInfo>();
            }
            if(recursive)
            {
                return t.GetMethods(DefaultBindingFlags).Union(GetAllMethods(t.BaseType));
            }
            return t.GetMethods(DefaultBindingFlags);
        }

        public static IEnumerable<FieldInfo> GetAllFields(this Type t, bool recursive = true)
        {
            if(t == null)
            {
                return Enumerable.Empty<FieldInfo>();
            }
            if(recursive)
            {
                return t.GetFields(DefaultBindingFlags).Union(GetAllFields(t.BaseType));
            }
            return t.GetFields(DefaultBindingFlags);
        }


        public static byte[] ReadBytes(this Stream stream, int count)
        {
            var buffer = new byte[count];
            var read = 0;
            while(read < count)
            {
                var readInThisIteration = stream.Read(
                    buffer,
                    read,
                    count - read
                );
                if(readInThisIteration == 0)
                {
                    throw new EndOfStreamException(string.Format(
                        "End of stream encountered, only {0} bytes could be read.",
                        read
                    )
                    );
                }
                read += readInThisIteration;
            }
            return buffer;
        }

        public static int KB(this int value)
        {
            return 1024 * value;
        }

        public static int MB(this int value)
        {
            return 1024 * 1024 * value;
        }

        /// <summary>
        /// Computes which power of two is given number. You can only use this function if you know
        /// that this number IS a power of two.
        /// </summary>
        public static int Logarithm2(int value)
        {
            return MultiplyDeBruijnBitPosition2[(int)((uint)(value * 0x077CB531u) >> 27)];
        }

        public static byte[] AsRawBytes<T>(this T structure) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var result = new byte[size];
            var bufferPointer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(structure, bufferPointer, false);
            Marshal.Copy(bufferPointer, result, 0, size);
            Marshal.FreeHGlobal(bufferPointer);
            return result;
        }

        public static String NormalizeBinary(double what)
        {
            var prefix = (what < 0) ? "-" : "";
            if(what == 0)
            {
                return "0";
            }
            if(what < 0)
            {
                what = -what;
            }
            var power = (int)Math.Log(what, 2);
            var index = power / 10;
            if(index >= BytePrefixes.Length)
            {
                index = BytePrefixes.Length - 1;
            }
            what /= Math.Pow(2, 10 * index);
            return string.Format(
                "{0}{1:0.##}{2}",
                prefix,
                what,
                BytePrefixes[index]
            );
        }

        public static void ByteArrayWrite(long offset, uint value, byte[] array)
        {
            var index = (int)(offset);
            var bytes = BitConverter.GetBytes(value);
            for(var i = 0; i < 4; i++)
            {
                array[index + i] = bytes[i];
            }
        }

        public static uint ByteArrayRead(long offset, byte[] array)
        {
            var index = (int)(offset);
            var bytes = new byte[4];
            for(var i = 0; i < 4; i++)
            {
                bytes[i] = array[index + i];
            }
            return BitConverter.ToUInt32(bytes, 0);
        }

        public static String NormalizeDecimal(double what)
        {
            var prefix = (what < 0) ? "-" : "";
            if(what == 0)
            {
                return "0";
            }
            if(what < 0)
            {
                what = -what;
            }
            var digits = Convert.ToInt32(Math.Floor(Math.Log10(what)));
            var power = (long)(3 * Math.Round((digits / 3.0)));
            var index = power / 3 + ZeroPrefixPosition;
            if(index < 0)
            {
                index = 0;
                power = 3 * (1 + ZeroPrefixPosition - SIPrefixes.Length);
            } else if(index >= SIPrefixes.Length)
            {
                index = SIPrefixes.Length - 1;
                power = 3 * (SIPrefixes.Length - ZeroPrefixPosition - 1);
            }
            what /= Math.Pow(10, power);
            var unit = SIPrefixes[index];
            what = Math.Round(what, 2);
            return prefix + what + unit;
        }

        public static string GetShortName(object o)
        {
            if(Misc.IsPythonObject(o))
            {
                return Misc.GetPythonName(o);
            }
            var type = o.GetType();
            return type.Name;
        }

        public static bool IsPowerOfTwo(ulong value)
        {
            return (value != 0) && (value & (value - 1)) == 0;
        }

        public static int NextPowerOfTwo(int value)
        {
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value++;
            return value;
        }

        public static void Times(this int times, Action<int> action)
        {
            for(var i = 0; i < times; i++)
            {
                action(i);
            }
        }

        public static void Times(this int times, Action action)
        {
            for(var i = 0; i < times; i++)
            {
                action();
            }
        }

        public static StringBuilder AppendIf(this String value, bool condition, string what)
        {
            var builder = new StringBuilder(value);
            return AppendIf(builder, condition, what);
        }

        public static StringBuilder AppendIf(this StringBuilder value, bool condition, string what)
        {
            if(condition)
            {
                value.Append(what);
            }
            return value;
        }

        public static String Indent(this String value, int count)
        {
            return "".PadLeft(count) + value;
        }

        public static String Indent(this String value, int count, char fill)
        {
            return "".PadLeft(count, fill) + value;
        }

        public static String Outdent(this String value, int count)
        {
            return value + "".PadLeft(count);
        }

        public static Boolean StartsWith(this String source, char value)
        {
            if (source.Length > 0)
            {
                return source[0] == value;
            }
            return false;
        }

        public static Boolean EndsWith(this String source, char value)
        {
            if (source.Length > 0)
            {
                return source[source.Length - 1] == value;
            }
            return false;
        }

        public static String Trim(this String value, String toCut)
        {
            if (!value.StartsWith(toCut))
            {
                return value;
            }
            return value.Substring(toCut.Length);
        }

        public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var i = 0;
            foreach (var element in source)
            {
                if (predicate(element))
                    return i;

                i++;
            }
            return -1;
        }

        public static int LastIndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            var revSource = source.Reverse();
            var i = revSource.Count() - 1;
            foreach (var element in revSource)
            {
                if (predicate(element))
                    return i;

                i--;
            }
            return -1;
        }

        public static string Stringify<TSource>(this IEnumerable<TSource> source, string separator = " ", int limitPerLine = 0)
        {
            return Stringify(source.Select(x => x == null ? String.Empty : x.ToString()), separator, limitPerLine);
        }

        public static string Stringify(this IEnumerable<string> source, string separator = " ", int limitPerLine = 0)
        {
            int idx = 0;
            if(source.Any())
            {
                return source.Aggregate((x, y) => x + separator + y + (limitPerLine != 0 && (++idx % limitPerLine == 0) ? "\n" : string.Empty));
            }
            return String.Empty;
        }

        public static byte[] HexStringToByteArray(string hexString)
        {
            return Enumerable.Range(0, hexString.Length).Where(x => x%2 == 0).Select(x => Convert.ToByte(hexString.Substring(x, 2), 16)).ToArray();
        }

        // MoreLINQ - Extensions to LINQ to Objects
        // Copyright (c) 2008 Jonathan Skeet. All rights reserved.
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey> comparer = null)
        {
            if(source == null)
            {
                throw new ArgumentNullException("source");
            }
            if(keySelector == null)
            {
                throw new ArgumentNullException("keySelector");
            }
            var knownKeys = new HashSet<TKey>(comparer);
            foreach (var element in source)
            {
                if (knownKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }

        /// <summary>Adds elements to the end of an IEnumerable.</summary>
        /// <typeparam name="T">Type of enumerable to return.</typeparam>
        /// <returns>IEnumerable containing all the input elements, followed by the
        /// specified additional elements.</returns>
        public static IEnumerable<T> Append<T>(this IEnumerable<T> source, params T[] element)
        {
            if(source == null)
            {
                throw new ArgumentNullException("source");
            }
            return ConcatIterator(element, source, false);
        }

        /// <summary>Adds elements to the start of an IEnumerable.</summary>
        /// <typeparam name="T">Type of enumerable to return.</typeparam>
        /// <returns>IEnumerable containing the specified additional elements, followed by
        /// all the input elements.</returns>
        public static IEnumerable<T> Prepend<T>(this IEnumerable<T> tail, params T[] head)
        {
            if(tail == null)
            {
                throw new ArgumentNullException("tail");
            }
            return ConcatIterator(head, tail, true);
        }

        private static IEnumerable<T> ConcatIterator<T>(T[] extraElements,
            IEnumerable<T> source, bool insertAtStart)
        {
            if(insertAtStart)
            {
                foreach(var e in extraElements)
                {
                    yield return e;
                }
            }
            foreach(var e in source)
            {
                yield return e;
            }
            if(!insertAtStart)
            {
                foreach(var e in extraElements)
                {
                    yield return e;
                }
            }
        }

        public static byte HiByte(this UInt16 value)
        {
            return (byte)((value >> 8) & 0xFF);
        }

        public static byte LoByte(this UInt16 value)
        {
            return (byte)(value & 0xFF);
        }

        public static bool TryFromResourceToTemporaryFile(this Assembly assembly, string resourceName, out string outputFileFullPath, string nonstandardOutputFilename = null)
        {
            // `GetManifestResourceStream` is not supported by dynamic assemblies
            Stream libraryStream = assembly.IsDynamic
                ? null
                : assembly.GetManifestResourceStream(resourceName);

            if(libraryStream == null)
            {
                if(File.Exists(resourceName))
                {
                    libraryStream = new FileStream(resourceName, FileMode.Open, FileAccess.Read, FileShare.None);
                }
                if(libraryStream == null)
                {
                    outputFileFullPath = null;
                    return false;
                }
            }

            string libraryFile;
            if(nonstandardOutputFilename != null)
            {
                if(!TemporaryFilesManager.Instance.TryCreateFile(nonstandardOutputFilename, out libraryFile))
                {
                    Logging.Logger.Log(Logging.LogLevel.Error, "Could not unpack resource {0} to {1}. This likely signifies an internal error.", resourceName, nonstandardOutputFilename);
                    outputFileFullPath = null;
                    return false;
                }
            }
            else
            {
                libraryFile = TemporaryFilesManager.Instance.GetTemporaryFile(resourceName);

                if(String.IsNullOrEmpty(libraryFile))
                {
                    outputFileFullPath = null;
                    return false;
                }
            }
            outputFileFullPath = CopyToFile(libraryStream, libraryFile);
            return true;
        }

        public static string FromResourceToTemporaryFile(this Assembly assembly, string resourceName)
        {
            if(!TryFromResourceToTemporaryFile(assembly, resourceName, out var result))
            {
                throw new ArgumentException(string.Format("Cannot find library {0}", resourceName));
            }
            return result;
        }

        public static void Copy(this Stream from, Stream to)
        {
            var buffer = new byte[4096];
            int read;
            do
            {
                read = from.Read(buffer, 0, buffer.Length);
                if(read <= 0) // to workaround ionic zip's bug
                {
                    break;
                }
                to.Write(buffer, 0, read);
            }
            while(true);
        }

        public static void GetPixelFromPngImage(string fileName, int x, int y, out byte r, out byte g, out byte b)
        {
            Bitmap bitmap;
            if(fileName == LastBitmapName)
            {
                bitmap = LastBitmap;
            }
            else
            {
                bitmap = new Bitmap(fileName);
                LastBitmap = bitmap;
                LastBitmapName = fileName;
            }
            var color = bitmap.GetPixel(x, y);
            r = color.R;
            g = color.G;
            b = color.B;
        }

        private static string cachedRootDirectory;

        public static string GetRootDirectory()
        {
            if(cachedRootDirectory != null)
            {
                return cachedRootDirectory;
            }

            if(TryGetRootDirectory(out var result))
            {
                return result;
            }

            // fall-back to the current working directory
            var cwd = Directory.GetCurrentDirectory();
            Logger.Log(LogLevel.Warning, "Could not find '.renode-root' - falling back to current working directory ({0}) - might cause problems.", cwd);
            cachedRootDirectory = cwd;

            return cwd;
        }

        public static bool TryGetRootDirectory(out string directory)
        {
#if PLATFORM_LINUX
            if(AssemblyHelper.BundledAssembliesCount > 0)
            {
                // we are bundled, so we need a custom way of detecting the root directory
                var thisFile = new StringBuilder(2048);
                Mono.Unix.Native.Syscall.readlink("/proc/self/exe", thisFile);
                return TryGetRootDirectory(Path.GetDirectoryName(thisFile.ToString()), out directory);
            }
#endif

            return TryGetRootDirectory(AppDomain.CurrentDomain.BaseDirectory, out directory);
        }

        public static bool TryGetRootDirectory(string baseDirectory, out string directory)
        {
            if(cachedRootDirectory != null)
            {
                directory = cachedRootDirectory;
                return true;
            }

            directory = null;
            var currentDirectory = new DirectoryInfo(baseDirectory);
            while(currentDirectory != null)
            {
                var indicatorFiles = Directory.GetFiles(currentDirectory.FullName, ".renode-root");
                if(indicatorFiles.Length == 1)
                {
                    var content = File.ReadAllLines(Path.Combine(currentDirectory.FullName, indicatorFiles[0]));
                    if(content.Length == 1 && content[0] == "5344ec2a-1539-4017-9ae5-a27c279bd454")
                    {
                        directory = currentDirectory.FullName;
                        cachedRootDirectory = directory;
                        return true;
                    }
                }
                currentDirectory = currentDirectory.Parent;
            }
            return false;
        }

        public static TimeSpan Multiply(this TimeSpan multiplicand, int multiplier)
        {
            return TimeSpan.FromTicks(multiplicand.Ticks * multiplier);
        }

        public static TimeSpan Multiply(this TimeSpan multiplicand, double multiplier)
        {
            return TimeSpan.FromTicks((long)(multiplicand.Ticks * multiplier));
        }

        public static String FormatWith(this String @this, params object[] args)
        {
            if(@this == null)
            {
                throw new ArgumentNullException("this");
            }
            if(args == null)
            {
                throw new ArgumentNullException("args");
            }
            return String.Format(@this, args);

        }

        private static string CopyToFile(Stream libraryStream, string libraryFile)
        {
            try
            {
                using(var destination = new FileStream(libraryFile, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    libraryStream.Copy(destination);
                    Logger.Noisy(String.Format("Library copied to {0}.", libraryFile));
                }
                return libraryFile;
            }
            catch(IOException e)
            {
                throw new InvalidOperationException(String.Format("Error while copying file: {0}.", e.Message));
            }
            finally
            {
                if(libraryStream != null)
                {
                    libraryStream.Close();
                }
            }
        }

        private static ushort ComputeHeaderIpChecksum(byte[] header, int start, int length)
        {
            ushort word16;
            var sum = 0L;
            for (var i = start; i < (length + start); i+=2)
            {
                if(i - start == 10)
                {
                    //These are IP Checksum fields.
                    continue;
                }
                word16 = (ushort)(((header[i] << 8 ) & 0xFF00)
                    + (header[i + 1] & 0xFF));
                sum += (long)word16;
            }

            while ((sum >> 16) != 0)
            {
                sum = (sum & 0xFFFF) + (sum >> 16);
            }
            sum = ~sum;
            return (ushort)sum;
        }

        // Calculates the TCP checksum using the IP Header and TCP Header.
        // Ensure the TCPHeader contains an even number of bytes before passing to this method.
        // If an odd number, pad with a 0 byte just for checksumming purposes.
        static ushort GetPacketChecksum(byte[] packet, int startOfIp, int startOfPayload, bool withPseudoHeader)
        {
            var sum = 0u;
            // Protocol Header
            for (var x = startOfPayload; x < packet.Length - 1; x += 2)
            {
                sum += Ntoh(BitConverter.ToUInt16(packet, x));
            }
            if((packet.Length - startOfPayload) % 2 != 0)
            {
                //odd length
                sum += (ushort)((packet[packet.Length - 1] << 8) | 0x00);
            }
            if(withPseudoHeader)
            {
                // Pseudo header - Source Address
                sum += Ntoh(BitConverter.ToUInt16(packet, startOfIp + 12));
                sum += Ntoh(BitConverter.ToUInt16(packet, startOfIp + 14));
                // Pseudo header - Dest Address
                sum += Ntoh(BitConverter.ToUInt16(packet, startOfIp + 16));
                sum += Ntoh(BitConverter.ToUInt16(packet, startOfIp + 18));
                // Pseudo header - Protocol
                sum += Ntoh(BitConverter.ToUInt16(new byte[] { 0, packet[startOfIp + 9] }, 0));
                // Pseudo header - TCP Header length
                sum += (ushort)(packet.Length - startOfPayload);
            }
            // 16 bit 1's compliment
            while((sum >> 16) != 0)
            {
                sum = ((sum & 0xFFFF) + (sum >> 16));
            }
            return (ushort)~sum;
        }

        private static ushort Ntoh(UInt16 input)
        {
            int x = System.Net.IPAddress.NetworkToHostOrder(input);
            return (ushort) (x >> 16);
        }


        public enum TransportLayerProtocol
        {
            ICMP = 0x1,
            TCP = 0x6,
            UDP = 0x11
        }
        public enum PacketType
        {
            IP = 0x800,
            ARP = 0x806,
        }

        //TODO: Support for ipv6
        public static void FillPacketWithChecksums(IPeripheral source, byte[] packet, params TransportLayerProtocol[] interpretedProtocols)
        {
            if (packet.Length < MACLength) {
                source.Log(LogLevel.Error, String.Format("Expected packet of at least {0} bytes, got {1}.", MACLength, packet.Length));
                return;
            }
            var packet_type = (PacketType) ((packet[12] << 8) | packet[13]);
            if (packet_type == PacketType.ARP) {
                // ARP
                return;
            } else if (packet_type != PacketType.IP) {
                source.Log(LogLevel.Error, String.Format("Unknown packet type: 0x{0:X}. Supported are: 0x800 (IP) and 0x806 (ARP).", (ushort)packet_type));
                return;
            }
            if (packet.Length < (MACLength+12)) {
                source.Log(LogLevel.Error, "IP Packet is too short!");
                return;
            }
            // IPvX
            if ((packet[MACLength] >> 4) != 0x04) {
                source.Log(LogLevel.Error, String.Format("Only IPv4 packets are supported. Got IPv{0}", (packet[MACLength] >> 4)));
                return;
            }
            // IPv4
            var ipLength = (packet[MACLength] & 0x0F) * 4;
            if(ipLength != 0)
            {
                var ipChecksum = ComputeHeaderIpChecksum(packet, MACLength, ipLength);
                packet[MACLength + 10] = (byte)(ipChecksum >> 8);
                packet[MACLength + 11] = (byte)(ipChecksum & 0xFF);
            } else {
                source.Log(LogLevel.Error, "Something is wrong - IP packet of len 0");
            }
            if(interpretedProtocols != null && interpretedProtocols.Contains((TransportLayerProtocol)packet[MACLength + 9]))
            {
                var payloadStart = MACLength + ipLength;
                var protocol = (TransportLayerProtocol)packet[MACLength + 9];
                var checksum = GetPacketChecksum(packet, MACLength, payloadStart, protocol != TransportLayerProtocol.ICMP);
                switch(protocol)
                {
                case TransportLayerProtocol.ICMP:
                    packet[payloadStart + 2] = (byte)((checksum >> 8) & 0xFF);
                    packet[payloadStart + 3] = (byte)((checksum ) & 0xFF);
                    break;
                case TransportLayerProtocol.TCP:
                    packet[payloadStart + 16] = (byte)((checksum >> 8) & 0xFF);
                    packet[payloadStart + 17] = (byte)((checksum ) & 0xFF);
                    break;
                case TransportLayerProtocol.UDP:
                    packet[payloadStart + 6] = (byte)((checksum >> 8) & 0xFF);
                    packet[payloadStart + 7] = (byte)((checksum ) & 0xFF);
                    break;
                default:
                    throw new NotImplementedException();
                }
            }
        }

        public static string DumpPacket(EthernetFrame packet, bool isSend, Machine machine)
        {
            var builder = new StringBuilder();
            string machName;
            if(!EmulationManager.Instance.CurrentEmulation.TryGetMachineName(machine, out machName))
            {
                //probably the emulation is closing now, just return.
                return string.Empty;
            }
            if(isSend)
            {
                builder.AppendLine(String.Format("Sending packet from {0}, length: {1}", machName, packet.Bytes.Length));
            }
            else
            {
                builder.AppendLine(String.Format("Receiving packet on {0}, length: {1}", machName, packet.Bytes.Length));
            }
            builder.Append(packet.ToString());
            return builder.ToString();
        }

        public static void Swap<T>(ref T a, ref T b)
        {
            T temporary = a;
            a = b;
            b = temporary;
        }

        public static ushort SwapBytesUShort(ushort val)
        {
            return (ushort)((val << 8) | (val >> 8));
        }

        public static uint SwapBytesUInt(uint value)
        {
            return (value & 0xFF000000) >> 24
                 | (value & 0x00FF0000) >> 8
                 | (value & 0x0000FF00) << 8
                 | (value & 0x000000FF) << 24;
        }

        public static T SwapBytes<T>(T value)
        {
            var type = typeof(T);
            if(type == typeof(uint))
            {
                return (T)(object)SwapBytesUInt((uint)(object)value);
            }
            else if(type == typeof(ushort))
            {
                return (T)(object)SwapBytesUShort((ushort)(object)value);
            }
            else if(type == typeof(byte))
            {
                return value;
            }
            else
            {
                throw new ArgumentException($"Unhandled type {type}");
            }
        }

        public static void SwapElements<T>(T[] arr, int id1, int id2)
        {
            var tmp = arr[id1];
            arr[id1] = arr[id2];
            arr[id2] = tmp;
        }

        public static bool EndiannessSwapInPlace(byte[] input, int width)
        {
            if(input.Length % width != 0)
            {
                return false;
            }

            for(var i = 0; i < input.Length; i += width)
            {
                for(var j = 0; j < width / 2; j++)
                {
                    SwapElements(input, i + j, i + width - j - 1);
                }
            }

            return true;
        }

        public static bool CalculateUnitSuffix(double value, out double newValue, out string unit)
        {
            var units = new [] { "B", "KB", "MB", "GB", "TB" };

            var v = value;
            var i = 0;
            while(i < units.Length - 1 && Math.Round(v / 1024) >= 1)
            {
                v /= 1024;
                i++;
            }

            newValue = v;
            unit = units[i];

            return true;
        }

        public static string ToOrdinal(this int num)
        {
            if(num <= 0)
            {
                return num.ToString();
            }
            switch(num % 100)
            {
            case 11:
            case 12:
            case 13:
                return num + "th";
            }

            switch(num % 10)
            {
            case 1:
                return num + "st";
            case 2:
                return num + "nd";
            case 3:
                return num + "rd";
            default:
                return num + "th";
            }
        }

        private const int MACLength = 14;

        private static string LastBitmapName = "";
        private static Bitmap LastBitmap;

        private const BindingFlags DefaultBindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static readonly string[] SIPrefixes = {
                "p",
                "n",
                "µ",
                "m",
                "",
                "k",
                "M",
                "G",
                "T"
            };
        private static readonly string[] BytePrefixes = {
                "",
                "Ki",
                "Mi",
                "Gi",
                "Ti"
            };
        private const int ZeroPrefixPosition = 4;
        private static readonly int[] MultiplyDeBruijnBitPosition2 =
         {
           0, 1, 28, 2, 29, 14, 24, 3, 30, 22, 20, 15, 25, 17, 4, 8,
           31, 27, 13, 23, 21, 19, 16, 7, 26, 12, 18, 6, 11, 5, 10, 9
         };

        /// <summary>
        /// Checks if the current user is a root.
        /// </summary>
        /// <value><c>true</c> if is root; otherwise, <c>false</c>.</value>
        public static bool IsRoot
        {
            get { return Environment.UserName == "root"; }
        }

        public static bool IsOnOsX
        {
            get
            {
                if(Environment.OSVersion.Platform == PlatformID.MacOSX)
                {
                    return true;
                }
                return Directory.Exists("/Library") && Directory.Exists("/Applications");
            }
        }

        public static bool IsCommandAvaialble(string command)
        {
            var verifyProc = new Process();
            verifyProc.StartInfo.UseShellExecute = false;
            verifyProc.StartInfo.RedirectStandardError = true;
            verifyProc.StartInfo.RedirectStandardInput = true;
            verifyProc.StartInfo.RedirectStandardOutput = true;
            verifyProc.EnableRaisingEvents = false;
            verifyProc.StartInfo.FileName = "which";
            verifyProc.StartInfo.Arguments = command;

            verifyProc.Start();

            verifyProc.WaitForExit();
            return verifyProc.ExitCode == 0;
        }

        public static string PrettyPrintFlagsEnum(Enum enumeration)
        {
            var values = new List<string>();
            foreach(Enum value in Enum.GetValues(enumeration.GetType()))
            {
                if((Convert.ToUInt64(enumeration) & Convert.ToUInt64(value)) != 0)
                {
                    values.Add(value.ToString());
                }
            }
            return values.Count == 0 ? "-" : values.Aggregate((x, y) => x + ", " + y);
        }

        public static bool TryGetMatchingSignature(IEnumerable<Type> signatures, MethodInfo mi, out Type matchingSignature)
        {
            matchingSignature = signatures.FirstOrDefault(x => HasMatchingSignature(x, mi));
            return matchingSignature != null;
        }

        public static bool HasMatchingSignature(Type delegateType, MethodInfo mi)
        {
            var delegateMethodInfo = delegateType.GetMethod("Invoke");

            return mi.ReturnType == delegateMethodInfo.ReturnType &&
                mi.GetParameters().Select(x => x.ParameterType).SequenceEqual(delegateMethodInfo.GetParameters().Select(x => x.ParameterType));
        }

        public static int Clamp(this int value, int min, int max)
        {
            if(value < min)
            {
                return min;
            }
            if(value > max)
            {
                return max;
            }
            return value;
        }

        public static string[] Split(this string value, int size)
        {
            var ind = 0;
            return value.GroupBy(x => ind++ / size).Select(x => string.Join("", x)).ToArray();
        }

        public static IEnumerable<T[]> Split<T>(this IEnumerable<T> values, int size)
        {
            var i = 0;
            return values.GroupBy(_ => i++ / size).Select(chunk => chunk.ToArray());
        }

        public static void SetBit(this IValueRegisterField field, byte index, bool value)
        {
            var val =  field.Value;
            BitHelper.SetBit(ref val, index, value);
            field.Value = val;
        }

        public static ulong InMicroseconds(this TimeSpan ts)
        {
            return (ulong)(ts.Ticks / 10);
        }

        public static void WaitWhile(this object @this, Func<bool> condition, string reason)
        {
            @this.Trace($"Waiting for '{reason}'...");
            while(condition())
            {
                Monitor.Wait(@this);
            }
            @this.Trace($"Waiting for '{reason}' finished.");
        }

        public static void FlipFlag<TEnum>(ref TEnum value, TEnum flag, bool state)
        {
            if(!typeof(TEnum).IsEnum)
            {
                throw new ArgumentException("TEnum must be an enumerated type.");
            }
            var intValue = (long)(object)value;
            var intFlag = (long)(object)flag;
            if(state)
            {
                intValue |= intFlag;
            }
            else
            {
                intValue &= ~intFlag;
            }
            value = (TEnum)(object)intValue;
        }

        public static string PrettyPrintCollection<T>(IEnumerable<T> collection, Func<T, string> formatter = null)
        {
            return collection == null || !collection.Any()
                ? "[]"
                : $"[{(string.Join(", ", collection.Select(x => formatter == null ? x.ToString() : formatter(x))))}]";
        }

        public static string PrettyPrintCollectionHex<T>(IEnumerable<T> collection)
        {
            return PrettyPrintCollection(collection, x => "0x{0:X}".FormatWith(x));
        }

        public static UInt32 ToUInt32Smart(this byte[] @this)
        {
            if(@this.Length > 4)
            {
                throw new ArgumentException($"Bytes array is tool long. Expected at most 4 bytes, but got {@this.Length}");
            }
            var result = 0u;
            for(var i = 0; i < @this.Length; i++)
            {
                result |= ((uint)@this[i] << (8 * i));
            }
            return result;
        }

        public static DateTime With(this DateTime @this, int? year = null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null)
        {
            return new DateTime(
                year ?? @this.Year,
                month ?? @this.Month,
                day ?? @this.Day,
                hour ?? @this.Hour,
                minute ?? @this.Minute,
                second ?? @this.Second);
        }

        public static int EnqueueRange<T>(this Queue<T> @this, IEnumerable<T> data, int? limit = null)
        {
            var counter = 0;
            foreach(var e in data.Take(limit ?? int.MaxValue))
            {
                @this.Enqueue(e);
                counter++;
            }
            return counter;
        }

        public static T[] DequeueRange<T>(this Queue<T> @this, int limit)
        {
            var result = new T[Math.Min(@this.Count, limit)];
            for(var i = 0; i < result.Length; i++)
            {
                result[i] = @this.Dequeue();
            }
            return result;
        }

        public static T[] DequeueAll<T>(this Queue<T> @this)
        {
            return DequeueRange(@this, @this.Count);
        }

        public static bool TryDequeue<T>(this Queue<T> @this, out T result)
        {
            if(@this.Count == 0)
            {
                result = default(T);
                return false;
            }
            result = @this.Dequeue();
            return true;
        }

        public static IEnumerable<T> PopRange<T>(this Stack<T> @this, int limit)
        {
            while(@this.Count > 0 && limit > 0)
            {
                limit--;
                yield return @this.Pop();
            }
        }

        public static IEnumerable<T> PopAll<T>(this Stack<T> @this)
        {
            return PopRange(@this, @this.Count);
        }

        public static bool TryCreateFrameOrLogWarning(IEmulationElement source, byte[] data, out EthernetFrame frame, bool addCrc)
        {
            if(EthernetFrame.TryCreateEthernetFrame(data, addCrc, out frame))
            {
                return true;
            }
            source.Log(LogLevel.Warning, "Insufficient data to create an ethernet frame, expected {0} bytes but got {1} bytes.",
                    EthernetFrame.MinFrameSizeWithoutCRC + (addCrc ? 0 : EthernetFrame.CRCLength), data.Length);
            return false;
        }

        public static string StripNonSafeCharacters(this string input)
        {
            return Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(input).Where(x => (x >= 32 && x <= 126) || (x == '\n')).ToArray());
        }

        // allocate file of a given name
        // if it already exists - rename it using pattern path.III (III being an integer)
        // returns information if there was a rename and III of the last renamed file
        public static bool AllocateFile(string path, out int counter)
        {
            counter = 0;
            var renamed = false;
            var dstName = $"{path}.{counter}";

            while(!TryCreateEmptyFile(path))
            {
                while(File.Exists(dstName))
                {
                    counter++;
                    dstName = $"{path}.{counter}";
                }
                File.Move(path, dstName);
                renamed = true;
            }
            return renamed;
        }

        // Allows to have static initialization of a two-element tuple list.
        // To enable other arities of tuples, add more overloads.
        public static void Add<T1, T2>(this IList<Tuple<T1, T2>> list,
            T1 item1, T2 item2)
        {
            list.Add(Tuple.Create(item1, item2));
        }

        private static bool TryCreateEmptyFile(string p)
        {
            try
            {
                File.Open(p, FileMode.CreateNew).Dispose();
                return true;
            }
            catch(IOException)
            {
                // this is expected - the file already exists
                return false;
            }
        }

        public static bool TryParseBitPattern(string pattern, out ulong value, out ulong mask)
        {
            value = 0uL;
            mask = 0uL;

            if(pattern.Length > 64)
            {
                return false;
            }

            var currentBit = pattern.Length - 1;

            foreach(var p in pattern)
            {
                switch(p)
                {
                    case '0':
                        mask |= (1uL << currentBit);
                        break;

                    case '1':
                        mask |= (1uL << currentBit);
                        value |= (1uL << currentBit);
                        break;

                    default:
                        // all characters other than '0' or '1' are treated as 'any-value'
                        break;
                }

                currentBit--;
            }

            return true;
        }

        public static void SetBytesFromValue(this byte[] array, uint value, int startIndex)
        {
            foreach(var b in BitConverter.GetBytes(value))
            {
                array[startIndex++] = b;
            }
        }

        public static bool TryFindPreceedingEnumItem<T>(uint value, out T bestCandidate, out int offset) where T: IConvertible
        {
            var allValues = Enum.GetValues(typeof(T));
            var maxIndex = allValues.Length - 1;

            bestCandidate = default(T);
            offset = 0;

            if((int)allValues.GetValue(0) > value)
            {
                // there are no values preceeding given value
                return false;
            }

            int currentValue = 0;
            int lowBorder = 0;
            int highBorder = maxIndex;

            // binary search
            while(highBorder != lowBorder)
            {
                var currentIndex = lowBorder + ((highBorder - lowBorder) / 2);
                currentValue = (int)allValues.GetValue(currentIndex);

                if(currentValue == value)
                {
                    break;
                }
                else if(currentValue < value)
                {
                    lowBorder = currentIndex + 1;
                }
                else
                {
                    highBorder = currentIndex;
                }
            }
            bestCandidate = (T)Enum.ToObject(typeof(T), currentValue);
            offset = (int)(value - currentValue);
            return true;
        }

        public static int CountTrailingZeroes(uint value)
        {
            int count = 0;
            while((value & 0x1) == 0)
            {
                count += 1;
                value >>= 1;
                if(count == sizeof(uint) * 8)
                {
                    break;
                }
            }
            return count;
        }
    }
}

