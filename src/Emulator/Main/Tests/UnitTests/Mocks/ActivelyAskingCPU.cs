//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Threading;
using Antmicro.Renode.Core;

namespace Antmicro.Renode.UnitTests.Mocks
{
    public class ActivelyAskingCPU : EmptyCPU
    {
        public ActivelyAskingCPU(IMachine machine, ulong addressToAsk) : base(machine)
        {
            this.addressToAsk = addressToAsk;
            tokenSource = new CancellationTokenSource();
            finished = new ManualResetEventSlim();
        }

        protected override void OnResume()
        {
            finished.Reset();
            new Thread(() => AskingThread(tokenSource.Token))
            {
                IsBackground = true,
                Name = "AskingThread"
            }.Start();
        }

        protected override void OnPause()
        {
            tokenSource.Cancel();
            finished.Wait();
        }

        private void AskingThread(CancellationToken token)
        {
            while(!token.IsCancellationRequested)
            {
                machine.SystemBus.ReadDoubleWord(addressToAsk);
            }
            finished.Set();
        }

        private CancellationTokenSource tokenSource;
        private readonly ManualResetEventSlim finished;
        private readonly ulong addressToAsk;
    }
}

