//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Core;
using Antmicro.Renode.Time;

namespace Antmicro.Renode.Peripherals.UART
{
    public static class LINHubExtensions
    {
        public static void CreateLINHub(this Emulation emulation, string name)
        {
            emulation.ExternalsManager.AddExternal(new LINHub(), name);
        }
    }

    public sealed class LINHub : UARTHubBase<ILINDevice>
    {
        public LINHub() : base(true) {}

        public override void AttachTo(ILINDevice lin)
        {
            base.AttachTo(lin);
            if(lin is ILINController linController)
            {
                linController.BroadcastLINBreak += HandleLINBreak;
            }
        }

        public override void DetachFrom(ILINDevice lin)
        {
            if(lin is ILINController linController)
            {
                linController.BroadcastLINBreak -= HandleLINBreak;
            }
            base.DetachFrom(lin);
        }

        private void HandleLINBreak()
        {
            if(!started)
            {
                return;
            }

            lock(locker)
            {
                foreach(var item in uarts.Keys)
                {
                    item.GetMachine().HandleTimeDomainEvent<object>(_ => item.ReceiveLINBreak(), null, TimeDomainsManager.Instance.VirtualTimeStamp);
                }
            }
        }
    }
}
