//
// Copyright (c) 2010-2026 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System;
using System.Collections.Generic;
using System.Linq;

using Antmicro.Migrant;
using Antmicro.Renode.Core;
using Antmicro.Renode.Core.USB;
using Antmicro.Renode.Extensions.Utilities.USB;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Peripherals.UART;

namespace Antmicro.Renode.Peripherals.USB;

public class USB_UART : USBHost, IUART, IExternal
{
    public USB_UART(IMachine machine)
    {
        this.machine = machine;
    }

    public void WriteChar(byte value)
    {
        // Chars have to be buffered so that eg. `Write Line To Uart` doesn't make multiple single-character packets at the exact same moment, which is something that can overwhelm Zephyr and lead to dropped inputs
        bufferedChars.Add(value);
        if(bufferedChars.Count == 1)
        {
            machine.LocalTimeSource.ExecuteInNearestSyncedState(_ => WriteBuffered());
        }
    }

    public override void Dispose()
    {
        if(readPipe != null)
        {
            readPipe.NewPacket -= OnUSBWritten;
        }
        base.Dispose();
    }

    public uint BaudRate { get; set; }

    public Bits StopBits { get; set; }

    public Parity ParityBit { get; set; }

    public byte DataBits { get; set; }

    [field: Transient]
    public event Action<byte> CharReceived;

    protected override void DeviceEnumerated(IUSBConnection conn)
    {
        var ep0 = conn.ConnectEndpointSetup(0);
        ep0.ReadConfigurationDescriptor(0, res =>
        {
            (_, var descs) = res;
            var (_, cdcDescriptors) = IDescriptor.EnumerateInterfaceDescriptors(descs)
                .Where(item => item.Item1.Class == (byte)USBClassCode.CommunicationsCDCControl)
                .FirstOrDefault();

            var (dataIface, dataDescriptors) = IDescriptor.EnumerateInterfaceDescriptors(descs)
                .Where(item => item.Item1.Class == (byte)USBClassCode.CDCData)
                .FirstOrDefault();

            if(cdcDescriptors == null || dataDescriptors == null)
            {
                this.WarningLog("Device doesn't have CDC or CDC-Data interface");
                return;
            }
            ep0.CDCControlLineState(dataIface.Number, rts: false, dtr: true, () =>
            {
                ListenOnInterface(conn, cdcDescriptors, dataDescriptors);
            });
        });
    }

    private static byte? FindEndpoint(IDescriptor[] descriptors, EndpointDirection direction)
    {
        var epDesc = descriptors
            .Where(desc =>
            {
                if(desc is not EndpointDescriptor ep)
                {
                    return false;
                }
                return ep.Direction == direction;
            })
            .FirstOrDefault() as EndpointDescriptor?;

        return epDesc?.EndpointNumber;
    }

    private void WriteBuffered()
    {
        if(writePipe == null)
        {
            return;
        }
        writePipe.Write(bufferedChars.ToArray());
        bufferedChars.Clear();
    }

    private void ListenOnInterface(IUSBConnection conn, IDescriptor[] cdcIfaceDescs, IDescriptor[] dataIfaceDescs)
    {
        if(readPipe != null)
        {
            readPipe.NewPacket -= OnUSBWritten;
        }
        if(interruptPipe != null)
        {
            interruptPipe.NewPacket -= OnUSBInterrupt;
        }

        var interruptEp = FindEndpoint(cdcIfaceDescs, EndpointDirection.In);

        interruptPipe = interruptEp != null ? conn.ConnectEndpointRead(interruptEp.Value) : null;

        if(interruptPipe == null)
        {
            this.WarningLog("CDC interface lacks interrupt endpoint");
        }
        else
        {
            interruptPipe.NewPacket += OnUSBInterrupt;
            // Clear out remnant data
            OnUSBInterrupt();
        }

        var readEp = FindEndpoint(dataIfaceDescs, EndpointDirection.In);

        readPipe = readEp != null ? conn.ConnectEndpointRead(readEp.Value) : null;

        if(readPipe == null)
        {
            this.WarningLog("CDC Data interface lacks read endpoint");
        }
        else
        {
            readPipe.NewPacket += OnUSBWritten;
            OnUSBWritten();
        }

        var writeEp = FindEndpoint(dataIfaceDescs, EndpointDirection.Out);

        writePipe = writeEp != null ? conn.ConnectEndpointWrite(writeEp.Value) : null;

        if(writePipe == null)
        {
            this.WarningLog("CDC Data interface lacks write endpoint");
        }
        WriteBuffered();
    }

    private void OnUSBWritten()
    {
        while(readPipe.TryRead(out var data))
        {
            foreach(var b in data)
            {
                CharReceived?.Invoke(b);
            }
        }
    }

    private void OnUSBInterrupt()
    {
        while(interruptPipe.TryRead(out var data))
        {
            // We don't care about interrupts, but devices will wait indefinitely for us to read them
        }
    }

    private IUSBPipeRead interruptPipe;
    private IUSBPipeRead readPipe;
    private IUSBPipeWrite writePipe;

    private readonly List<byte> bufferedChars = new();
    private readonly IMachine machine;
}
