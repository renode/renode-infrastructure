//
// Copyright (c) 2010-2022 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

namespace Antmicro.Renode.Utilities.Binding
{
    // This interface should be implemented by classes that export methods that can throw exceptions
    // using NativeBinder. If it is not implemented, an exception being thrown by an export will
    // cause the process to exit. For an example implementation, see tlib's unwind.h and tlib_unwind
    // (the C# implementation will usually consist of just a call to a native function.)
    public interface INativeUnwindable
    {
        void NativeUnwind();
    }
}
