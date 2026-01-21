//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI.SFDP
{
    public class SFDPData
    {
        public static bool TryDecodeAsSFDP(IList<byte> buffer, out SFDPData frame)
        {
            frame = null;
            var paramDictionary = new Dictionary<uint, SFDPParameter>();
            var jedecParameter = (JedecParameter)null;
            var jedecParamPtr = 0u;

            // Parsing SFDP header
            var nph = (int)buffer[6];

            // Parsing Parameter headers
            for(var i = 0; i <= nph; i++)
            {
                var baseIndex = SfdpHeaderLength + i * ParameterHeaderLength;
                var dwSize = (uint)(buffer[baseIndex + 3]);
                var parameterId = (ushort)((buffer[baseIndex + DWord + 3] << 8) | (buffer[baseIndex]));
                var ptr = 0u;
                ptr |= (uint)(buffer[baseIndex + DWord]);
                ptr |= (uint)(buffer[baseIndex + DWord + 1]) << 8;
                ptr |= (uint)(buffer[baseIndex + DWord + 2]) << 16;
                var parameterBuffer = buffer.Skip((int)ptr).Take((int)dwSize * DWord).ToArray();

                if(!ParameterIdToSFDPParameterDictionary.TryGetValue(parameterId, out var factory))
                {
                    Logger.WarningLog(null, "Unsupported parameterID {0:X2} encountered.", parameterId);
                    paramDictionary.Add(ptr, new UnrecognizedParameter(parameterId, parameterBuffer));
                }
                var sfdpParameter = (SFDPParameter)null;
                try
                {
                    // Parse the SFDP parameter table using the factory registered
                    // for the corresponding Parameter ID.
                    sfdpParameter = factory.Invoke(parameterBuffer, dwSize);
                }
                catch
                {
                    Logger.WarningLog(null, "Cannot decode SFDP parameter (parameterId: {0:X2}).", parameterId);
                    paramDictionary.Add(ptr, new UnrecognizedParameter(parameterId, parameterBuffer));
                }
                if(sfdpParameter is JedecParameter jedecParam)
                {
                    jedecParameter = jedecParam;
                    jedecParamPtr = ptr;
                }
                else
                {
                    paramDictionary.Add(ptr, sfdpParameter);
                }
            }
            if(jedecParameter == null)
            {
                return false;
            }
            frame = new SFDPData(new KeyValuePair<uint, JedecParameter>(0x20, jedecParameter), paramDictionary);

            return true;
        }

        // Jedec parameter is obligatory, therefore we have to ensure its presence
        public SFDPData(KeyValuePair<uint, JedecParameter> jedecParameterKvp, IReadOnlyDictionary<uint, SFDPParameter> parameters = null, byte major = 1, byte minor = 0xC)
        {
            Major = major;
            Minor = minor;
            parameters = parameters ?? new Dictionary<uint, SFDPParameter>();

            ParametersDictionary = parameters
                .Append(
                    new KeyValuePair<uint, SFDPParameter>(jedecParameterKvp.Key, jedecParameterKvp.Value)
                )
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            JedecParameter = jedecParameterKvp.Value;
        }

        public FourByteCommandsSupportParameter Support4ByteCommandsParameter
        {
            get
            {
                if(support4ByteCommandsParameter == null)
                {
                    support4ByteCommandsParameter = GetSupport4ByteCommandsParameter();
                }
                return support4ByteCommandsParameter;
            }
        }

        public byte Major { get; }

        public byte Minor { get; }

        public IReadOnlyDictionary<uint, SFDPParameter> ParametersDictionary { get; }

        public byte NPH { get => (byte)(ParametersDictionary.Count - 1); }

        public byte[] Bytes
        {
            get
            {
                if(bytes == null)
                {
                    bytes = ToBytes();
                }
                return bytes;
            }
        }

        public JedecParameter JedecParameter { get; }

        public const uint PTPMaxValue = 0xFFFFFF;

        private byte[] ToBytes()
        {
            var totalSize = ParametersDictionary.Max(kvp => kvp.Key + (uint)kvp.Value.Bytes.Length);
            var result = new byte[totalSize];

            // SFDP Header
            // magic bytes "SFDP"
            var sfdpHeader = new byte[SfdpHeaderLength];
            var magicBytes = Encoding.ASCII.GetBytes("SFDP");

            Array.Copy(magicBytes, sfdpHeader, magicBytes.Length);
            sfdpHeader[4] = Minor;
            sfdpHeader[5] = Major;
            sfdpHeader[6] = NPH;
            sfdpHeader[7] = 0xFF; // Unused

            Array.Copy(sfdpHeader, result, sfdpHeader.Length);

            // Parameters Header
            var parametersHeader = new byte[ParameterHeaderLength * (ParametersDictionary.Count)];
            var index = 0;
            foreach(var kvp in ParametersDictionary)
            {
                var ptp = kvp.Key;
                var parameter = kvp.Value;
                var parameterHeader = parameter.Header(ptp);
                Array.Copy(parameterHeader, 0, parametersHeader, parameterHeader.Length * index, parameterHeader.Length);
                index++;
            }

            Array.Copy(parametersHeader, 0, result, sfdpHeader.Length, parametersHeader.Length);

            // Parameter Table
            foreach(var kvp in ParametersDictionary)
            {
                var ptp = kvp.Key;
                var parameter = kvp.Value;
                var paramBytes = parameter.Bytes;
                Array.Copy(paramBytes, 0, result, ptp, paramBytes.Length);
            }

            return result;
        }

        private FourByteCommandsSupportParameter GetSupport4ByteCommandsParameter()
        {
            return ParametersDictionary.Values.OfType<FourByteCommandsSupportParameter>().FirstOrDefault();
        }

        private byte[] bytes;
        private FourByteCommandsSupportParameter support4ByteCommandsParameter;

        private static readonly IReadOnlyDictionary<ushort, Func<IList<byte>, uint, SFDPParameter>> ParameterIdToSFDPParameterDictionary = new Dictionary<ushort, Func<IList<byte>, uint, SFDPParameter>>
        {
            [JedecParameter.ParameterId] = JedecParameter.DecodeAsJEDECParameter,
            [FourByteCommandsSupportParameter.ParameterId] = FourByteCommandsSupportParameter.DecodeAsSupport4ByteCommandsParameter,
        };

        private readonly JedecParameter jedecParameter;

        private const int DWord = 4;

        private const int SfdpHeaderLength = 2 * DWord;

        private const int ParameterHeaderLength = 2 * DWord;
    }
}