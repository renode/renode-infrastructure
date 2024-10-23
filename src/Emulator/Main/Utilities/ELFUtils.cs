//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Exceptions;
using ELFSharp.ELF;

namespace Antmicro.Renode.Utilities
{
    public static class ELFUtils
    {
        public static IELF LoadELF(ReadFilePath fileName)
        {
            try
            {
                if(!ELFReader.TryLoad(fileName, out var result))
                {
                    throw new RecoverableException($"Could not load ELF from path: {fileName}");
                }
                return result;
            }
            catch(Exception e)
            {
                // ELF creating exception are recoverable in the sense of emulator state
                throw new RecoverableException($"Error while loading ELF: {e.Message}", e);
            }
        }
    }
}

