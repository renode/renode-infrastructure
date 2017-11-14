//
// Copyright (c) 2010-2017 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Peripherals
{
    public class BasicIAnalyzableBackend<T> : IAnalyzableBackend<T> where T: IAnalyzable
    {
        public void Attach(T analyzableElement)
        {
            AnalyzableElement = analyzableElement;
        }

        public IAnalyzable AnalyzableElement { get; private set; }
    }
}

