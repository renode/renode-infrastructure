//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;

namespace Antmicro.Renode.Utilities
{
    public abstract class TypeSorter
    {
        public static void Sort(Type[] t)
        {
            Array.Sort(t, Compare);
        }

        public static int Compare(Type tone, Type ttwo)
        {
            var t1tot2 = tone.IsAssignableFrom(ttwo);
            var t2tot1 = ttwo.IsAssignableFrom(tone);

            if(t1tot2 && !t2tot1)
            {
                return 1;
            }
            else if(t2tot1 && !t1tot2)
            {
                return -1;
            }

            return 0;
        }
    }
}

