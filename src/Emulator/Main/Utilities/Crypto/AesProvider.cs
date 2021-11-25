//
// Copyright (c) 2010-2021 Antmicro
//
//  This file is licensed under the MIT License.
//  Full license text is available in 'licenses/MIT.txt'.
//  
using System;
using System.Security.Cryptography;

namespace Antmicro.Renode.Utilities.Crypto
{
    public class AesProvider : IDisposable
    {
        public static AesProvider GetCbcMacProvider(byte[] key)
        {
            // CBC-MAC is just CBC with empty IV (all zeros)
            return new AesProvider(CipherMode.CBC, PaddingMode.Zeros, key, new byte[AesBlockSizeInBytes]);
        }

        public static AesProvider GetEcbProvider(byte[] key)
        {
            // ECB does not use IV at all
            return new AesProvider(CipherMode.ECB, PaddingMode.Zeros, key);
        }

        public static AesProvider GetCbcProvider(byte[] key, byte[] iv)
        {
            return new AesProvider(CipherMode.CBC, PaddingMode.None, key, iv);
        }
        
        public AesProvider(CipherMode mode, PaddingMode padding, byte[] key, byte[] iv = null)
        {
            aesEngine = Aes.Create();
            aesEngine.Mode = mode;
            aesEngine.Padding = padding;
            lastBlock = Block.OfSize(AesBlockSizeInBytes);

            this.key = key;
            this.iv = iv;
        }

        public void Dispose()
        {
            if(encryptor != null)
            {
                encryptor.Dispose();
            }
            if(decryptor != null)
            {
                decryptor.Dispose();
            }
            aesEngine.Dispose();
        }

        public void EncryptBlockInSitu(Block b)
        {
            EncryptBlock(b, b);
        }

        public void EncryptBlock(Block b, Block result)
        {
            if(encryptor == null)
            {
                encryptor = aesEngine.CreateEncryptor(key, iv);
            }

            encryptor.TransformBlock(b.Buffer, 0, b.Buffer.Length, result.Buffer, 0);
            lastBlock.CopyFrom(result);
        }

        public void DecryptBlockInSitu(Block b)
        {
            DecryptBlock(b, b);
        }

        public void DecryptBlock(Block b, Block result)
        {
            if(decryptor == null)
            {
                decryptor = aesEngine.CreateDecryptor(key, iv);
            }

            decryptor.TransformBlock(b.Buffer, 0, b.Buffer.Length, result.Buffer, 0);
            lastBlock.CopyFrom(result);
        }

        public Block LastBlock { get { return lastBlock; } }

        private ICryptoTransform encryptor;
        private ICryptoTransform decryptor;

        private readonly Block lastBlock;
        private readonly Aes aesEngine;
        private readonly byte[] key;
        private readonly byte[] iv;
        
        private const int AesBlockSizeInBytes = 16;
    }
    
    public class Block
    {
        public static Block WithCopiedBytes(byte[] bytes)
        {
            var result = new Block(bytes.Length);
            result.UpdateBytes(bytes);
            return result;
        }

        public static Block UsingBytes(byte[] bytes)
        {
            return new Block(bytes);
        }

        public static Block OfSize(int size)
        {
            return new Block(size);
        }

        public void UpdateByte(byte b)
        {
            buffer[Index++] = b;
        }

        public Block XorWith(Block b)
        {
            for(int i = 0; i < Math.Min(buffer.Length, b.buffer.Length); i++)
            {
                buffer[i] = (byte)(buffer[i] ^ b.buffer[i]);
            }

            return this;
        }

        public void UpdateBytes(byte[] bytes, int offset = 0, int length = -1)
        {
            if(length == -1)
            {
                length = bytes.Length - offset;
            }

            Array.Copy(bytes, offset, buffer, Index, length);
            Index += length;
        }

        public void PadSpaceLeft(byte value)
        {
            for(int i = Index; i < buffer.Length; i++)
            {
                buffer[i] = value;
            }
            Index = buffer.Length;
        }

        public void CopyTo(byte[] buffer)
        {
            this.buffer.CopyTo(buffer, 0);
        }

        public void CopyFrom(Block b)
        {
            b.buffer.CopyTo(buffer, 0);
            Index = b.Index;
        }

        public int SpaceLeft { get { return buffer.Length - Index; } }
        public byte[] Buffer { get { return buffer; } }
        public int Index { get; set; }

        private Block(byte[] buffer)
        {
            this.buffer = buffer;
        }

        private Block(int size)
        {
            buffer = new byte[size];
        }

        protected readonly byte[] buffer;
    }
}
