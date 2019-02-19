//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using Antmicro.Renode.Peripherals;
using Antmicro.Renode.Core;
using Xwt;

namespace Antmicro.Renode.UI
{
    public abstract class GUIPeripheralBackendAnalyzer<T> : BasicPeripheralBackendAnalyzer<T>, IHasWidget where T: IAnalyzableBackend 
    {
        public override void Show()
        {
            string tabName;
            if(!EmulationManager.Instance.CurrentEmulation.TryGetEmulationElementName(Backend.AnalyzableElement, out tabName))
            {
                tabName = "?";
            }

            ApplicationExtensions.InvokeInUIThreadAndWait(() => Emulator.UserInterfaceProvider.ShowAnalyser(this, tabName));
        }

        public override void Hide()
        {
            ApplicationExtensions.InvokeInUIThreadAndWait(() => Emulator.UserInterfaceProvider.HideAnalyser(this));
        }

        public override void AttachTo(T backend)
        {
            base.AttachTo(backend);
            ApplicationExtensions.InvokeInUIThreadAndWait(() => OnAttach(backend));
        }

        /// <summary>
        /// This method is called when backend analyzer is attached to a peripheral.
        /// IT IS GUARANTEED THAT THIS METHOD IS CALLED FROM GUI THREAD, SO INITIALIZATION OF WIDGETS SHOULD BE MADE HERE.
        /// </summary>
        /// <param name="backend">Backend.</param>
        protected abstract void OnAttach(T backend);

        public abstract Widget Widget { get; }
    }
}

