
using System.IO;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;

namespace Antmicro.Renode.Storage
{
    public static class DataStorage
    {
        public static Stream Create(string imageFile, long? size = null, bool persistent = false)
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

            return new SerializableStreamView(new FileStream(imageFile, FileMode.OpenOrCreate), size);
        }

        public static Stream Create(long size, byte paddingByte = 0)
        {
            return new SerializableStreamView(new FileStream(TemporaryFilesManager.Instance.GetTemporaryFile(), FileMode.OpenOrCreate), size, paddingByte);
        }
    }
}