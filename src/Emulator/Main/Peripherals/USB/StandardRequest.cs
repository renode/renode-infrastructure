//
// Copyright (c) 2010-2018 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//
namespace Antmicro.Renode.Core.USB
{
    public enum StandardRequest
    {
        GetStatus = 0,
        ClearFeature = 1,
        // value 2 reserved for the future use
        SetFeature = 3,
        // value 4 reserved for the future use
        SetAddress = 5,
        GetDescriptor = 6,
        SetDescriptor = 7,
        GetConfiguration = 8,
        SetConfiguration = 9,
        GetInterface = 10,
        SetInterface = 11,
        SynchronizeFrame = 12
    }
}