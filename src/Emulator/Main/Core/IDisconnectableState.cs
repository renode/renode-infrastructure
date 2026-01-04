//
// Copyright (c) 2010-2025 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core
{
    public interface IDisconnectableState : IPreservable
    {
        // Destructive action - instance should be considered unusable after disconnecting state from object.
        void DisconnectState();
    }
}