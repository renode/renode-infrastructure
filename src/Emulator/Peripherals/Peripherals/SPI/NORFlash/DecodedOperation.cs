//
// Copyright (c) 2010-2023 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Logging;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Peripherals.SPI.NORFlash
{
    public struct DecodedOperation
    {
        public OperationType Operation;
        public OperationState State;
        public OperationEraseSize EraseSize;
        public uint Register;
        public uint ExecutionAddress;
        public int CommandBytesHandled;
        public int DummyBytesRemaining;
        public int AddressLength
        {
            get
            {
                return addressLength;
            }
            set
            {
                addressLength = value;
                if(addressLength <= 0)
                {
                    Logger.Log(LogLevel.Warning, "Tried to set address length to {0} bytes while decoding operation. Aborting.", addressLength);
                    return;
                }
                if(addressLength > 4)
                {
                    Logger.Log(LogLevel.Warning, "Tried to set address length to {0} bytes while decoding operation. Address length was trimmed to 4 bytes.", addressLength);
                    addressLength = 4;
                }
                AddressBytes = new byte[addressLength];
            }
        }

        public bool TryAccumulateAddress(byte data)
        {
            AddressBytes[currentAddressByte] = data;
            currentAddressByte++;
            if(currentAddressByte == AddressLength)
            {
                ExecutionAddress = BitHelper.ToUInt32(AddressBytes, 0, AddressLength, false);
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"Operation: {Operation}"
                .AppendIf(Operation == OperationType.ReadRegister || Operation == OperationType.WriteRegister, $", register: {Register}")
                .AppendIf(EraseSize != 0, $", erase size: {EraseSize}")
                .AppendIf(AddressLength != 0, $", address length: {AddressLength}")
                .ToString();
        }

        public enum OperationEraseSize : uint
        {
            Die = 1, // starting from 1 to leave 0 as explicitly unused
            Sector,
            Subsector32K,
            Subsector4K
        }

        public enum OperationType
        {
            None,
            Read,
            ReadFast,
            ReadID,
            ReadSerialFlashDiscoveryParameter,
            Program,
            Erase,
            ReadRegister,
            WriteRegister,
            WriteEnable
        }

        public enum OperationState
        {
            RecognizeOperation,
            AccumulateCommandAddressBytes,
            AccumulateNoDataCommandAddressBytes,
            HandleCommand,
            HandleNoDataCommand,
            HandleImmediateCommand
        }

        private byte[] AddressBytes;
        private int addressLength;
        private int currentAddressByte;
    }
}
