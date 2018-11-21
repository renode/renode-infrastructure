//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core.USB.MSC.BOT
{
    public enum CommandStatus : byte
    {
        Success = 0x0,
        Failure = 0x1,
        PhaseError = 0x2
    }
}