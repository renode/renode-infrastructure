//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.Structure;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.Bus;
using Antmicro.Renode.Peripherals.UART;
using Antmicro.Renode.Time;
using Antmicro.Renode.Utilities.Packets;

namespace Antmicro.Renode.Peripherals.USB
{
    public static class CDCToUARTConverterExtensions
    {
        public static CDCToUARTConverter CreateCDCToUARTConverter(this Emulation emulation, string name)
        {
            var cdcToUARTConverter = new CDCToUARTConverter();
            emulation.BackendManager.TryCreateBackend(cdcToUARTConverter);
            emulation.ExternalsManager.AddExternal(cdcToUARTConverter, name);
            return cdcToUARTConverter;
        }

        public static void CreateAndAttachCDCToUARTConverter(this IUSBDevice attachTo, string name)
        {
            var emulation = EmulationManager.Instance.CurrentEmulation;
            var CDCUart = CreateCDCToUARTConverter(emulation, name);
            var usbConnector = new USBConnector();
            emulation.ExternalsManager.AddExternal(usbConnector, "usb_connector_cdc_acm_uart");
            emulation.Connector.Connect(attachTo, usbConnector);
            usbConnector.RegisterInController(CDCUart);
        }
    }

    public class CDCToUARTConverter : USBHost, IUART, IExternal
    {
        /*
         * For now the CDCToUARTConverter works like this:
         * 1. Wait for pullup
         * 2. Get output endpoint
         * 3. Set read callback after every read
        */
        public CDCToUARTConverter(ushort UARTDataInEndpoint = 2)
        {
            this.UARTDataInEndpoint = UARTDataInEndpoint;
            queue = new Queue<byte>();
            innerLock = new object();
        }
        
        public void WriteChar(byte value)
        {
            lock(innerLock)
            {
                CharReceived?.Invoke(value);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            EmulationManager.Instance.CurrentEmulation.BackendManager.HideAnalyzersFor(this);
        }

        public uint BaudRate { get; set; }

        public Bits StopBits { get; set; }

        public Parity ParityBit { get; set; }

        public byte DataBits { get; set; }

        public event Action<byte> CharReceived;

        protected override void DeviceEnumerated(IUSBDevice device)
        {
            SetDataReadCallback(device);
        }

        private void SetDataReadCallback(IUSBDevice device)
        {
            var ep = device.USBCore.GetEndpoint(UARTDataInEndpoint, Core.USB.Direction.DeviceToHost);
            ep.SetDataReadCallbackOneShot((dataEp, data) => ReadData(data, device));
        }

        private void ReadData(IEnumerable<byte> bytes, IUSBDevice device)
        {
            foreach(var data in bytes)
            {
                WriteChar(data);
            }
            var ep = device.USBCore.GetEndpoint(UARTDataInEndpoint, Core.USB.Direction.DeviceToHost);
            ep.SetDataReadCallbackOneShot((dataEp, data) => ReadData(data, device));
        }

        private readonly Queue<byte> queue;

        private readonly object innerLock;

        private readonly ushort UARTDataInEndpoint;
    }
}
