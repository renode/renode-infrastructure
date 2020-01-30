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
        public static DisposableWrapper New(Action a)
        {
            return new DisposableWrapper().RegisterDisposeAction(a);
        }

        public DisposableWrapper()
        {
            disposeActions = new List<Action>();
        }

        public DisposableWrapper RegisterDisposeAction(Action a)
        {
            disposeActions.Add(a);
            return this;
        }

        public void Disable()
        {
            disabled = true;
        }

        public void Dispose()
        {
            if(disabled)
            {
                return;
            }

            foreach(var a in disposeActions)
            {
                a();
            }
        }

        private bool disabled;

        private readonly List<Action> disposeActions;
    }
}
