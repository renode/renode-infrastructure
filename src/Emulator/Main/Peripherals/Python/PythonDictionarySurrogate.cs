//
// Copyright (c) 2010-2018 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.Collections.Generic;
using IronPython.Runtime;

namespace Antmicro.Renode.Peripherals.Python
{
    public class PythonDictionarySurrogate : Dictionary<object, object>
    {
        public PythonDictionarySurrogate(PythonDictionary dictionary)
        {
            internalDictionary = new Dictionary<object, object>(dictionary);
        }

        public PythonDictionary Restore()
        {
            var pythonDictionary = new PythonDictionary();
            foreach(var item in internalDictionary)
            {
                pythonDictionary.Add(item);
            }
            return pythonDictionary;
        }

        private readonly Dictionary<object, object> internalDictionary;
    }
}

