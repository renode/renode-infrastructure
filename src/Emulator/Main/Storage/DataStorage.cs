//
// Copyright (c) 2010-2024 Antmicro
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//

using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Storage
{
    public static class DataStorage
    {
        public static Stream Create(string imageFile, long? size = null, bool persistent = false, byte paddingByte = 0)
        {
            if(string.IsNullOrEmpty(imageFile))
            {
                throw new ConstructionException("No image file provided.");
            }

            if(!persistent)
            {
                var tempFileName = TemporaryFilesManager.Instance.GetTemporaryFile();
                FileCopier.Copy(imageFile, tempFileName, true);
                imageFile = tempFileName;
            }

            return new SerializableStreamView(new FileStream(imageFile, FileMode.OpenOrCreate), size, paddingByte: paddingByte);
        }

        public static Stream Create(long size, byte paddingByte = 0)
        {
            return new SerializableStreamView(new FileStream(TemporaryFilesManager.Instance.GetTemporaryFile(), FileMode.OpenOrCreate), size, paddingByte);
        }
    }
}
