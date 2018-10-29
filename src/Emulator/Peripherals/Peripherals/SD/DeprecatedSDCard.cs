//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
﻿using System;
using Antmicro.Renode.Storage;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Logging;
using System.IO;
using Antmicro.Renode.Exceptions;

namespace Antmicro.Renode.Peripherals.SD
{
    public class DeprecatedSDCard : IPeripheral
    {
        public DeprecatedSDCard(string imageFile, long? cardSize, bool persistent)
        {
            if(String.IsNullOrEmpty(imageFile))
            {
                throw new ConstructionException("No card image file provided.");
            }
            else
            {
                if(!persistent)
                {
                    var tempFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                    FileCopier.Copy(imageFile, tempFileName, true);
                    imageFile = tempFileName;
                }
                file = new SerializableStreamView(new FileStream(imageFile, FileMode.OpenOrCreate));
            }

            CardSize = cardSize ?? file.Length;

            var cardIdentificationBytes = new byte[] {0x01, 0x00, 0x00, 0x00, // 1b always one + 7b CRC (ignored) + 12b manufacturing date + 4b reserved
                0x00, 0x00, 0x00, 0x00, // 32b product serial number + 8b product revision
                0x45, 0x44, 0x43, 0x42, 0x41, // Product name, 5 character string. "ABCDE" (backwards)
                0x00, 0x00, 0x00 // 16b application ID + 8b manufacturer ID
            };

            cardIdentification = new uint[4];
            cardIdentification[0] = BitConverter.ToUInt32(cardIdentificationBytes, 0);
            cardIdentification[1] = BitConverter.ToUInt32(cardIdentificationBytes, 4);
            cardIdentification[2] = BitConverter.ToUInt32(cardIdentificationBytes, 8);
            cardIdentification[3] = BitConverter.ToUInt32(cardIdentificationBytes, 12);

            cardSpecificData = new uint[4];
            uint deviceSize = (uint)(CardSize / 0x80000 - 1);
            cardSpecificData[0] = 0x0a4040af;
            cardSpecificData[1] = 0x3b377f80 | ((deviceSize & 0xffff) << 16);
            cardSpecificData[2] = 0x5b590000 | ((deviceSize >> 16) & 0x3f);
            cardSpecificData[3] = 0x400e0032;
        }

        public void Reset()
        {
            opAppInitialized = false;
            opCodeChecked = false;
            lastAppOpCodeNormal = false;
        }

        public void Dispose()
        {
            file.Dispose();
        }

        public uint GoIdleState()
        {
            if(!lastAppOpCodeNormal)
            {
                return 0x40ff8000;
            }
            else
            {
                return 0x900;
            }
        }

        public uint SendOpCond()
        {
            return 0x300000;  // supported voltage: 3.1-3.3V
        }

        public uint SendStatus(bool dataTransfer)
        {
            if(dataTransfer)
            {
                return 0x0; // initial card status - idle
            }
            else
            {
                return (1 << 9) | (1 << 8); // ready for data
            }
        }

        public ulong SendSdConfigurationValue()
        {
            return 0xf000ul;
        }

        public uint SetRelativeAddress()
        {
            return 0x520 | (cardAddress << 16); // first 16b - relative address of the card
        }

        public uint SelectDeselectCard()
        {
            return 0x700;
        }

        public uint SendExtendedCardSpecificData()
        {
            return 0x1aa;
        }

        public uint[] AllSendCardIdentification()
        {
            return cardIdentification;
        }

        public uint AppCommand(uint argument)
        {
            uint result = 0x120;
            if(!opAppInitialized)
            {
                result |= (1 << 22);
                opAppInitialized = true;
            }
            if(((argument >> 16) & 0xffff) == cardAddress)
            {
                result |= (1 << 11);
            }
            return result;
        }

        public uint Switch()
        {
            return 0x900;
        }

        public uint SendAppOpCode(uint args)
        {
            uint result = 0x40ff8000;
            lastAppOpCodeNormal = true;
            if(args == 0x40200000)
            {
                if(opCodeChecked)
                {
                    result |= 0x80000000;
                    lastAppOpCodeNormal = false;
                }
                else
                {
                    opCodeChecked = true;
                }
            }
            return result;
        }

        public uint[] SendCardSpecificData()
        {
            return cardSpecificData;
        }

        public byte[] ReadData(long offset, int size)
        {
            file.Seek(offset, SeekOrigin.Begin);
            this.Log(LogLevel.Info, "Reading {0} bytes from card finished.", size);
            return file.ReadBytes(size);
        }

        public void WriteData(long offset, int size, byte[] data)
        {
            file.Seek(offset, SeekOrigin.Begin);
            file.Write(data, 0, size);
            this.Log(LogLevel.Info, "Writing {0} bytes to card finished.", (int)size);
        }

        public long CardSize
        {
            get;
            set;
        }

        private readonly uint cardAddress = 0xaaaa;
        private uint[] cardSpecificData, cardIdentification;
        private bool opAppInitialized, opCodeChecked, lastAppOpCodeNormal;
        private readonly SerializableStreamView file;

    }
}

