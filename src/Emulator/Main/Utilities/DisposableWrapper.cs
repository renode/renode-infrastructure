//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;

namespace Antmicro.Renode.Utilities
{
    public class DisposableWrapper : IDisposable
    {
        public DisposableWrapper()
        {
            disposeActions = new List<Action>();
        }

        public DisposableWrapper RegisterDisposeAction(Action a)
        {
            disposeActions.Add(a);
            return this;
        }

        public void Dispose()
        {
            foreach(var a in disposeActions)
            {
                a();
            }
        }

        private readonly List<Action> disposeActions;
    }
}
