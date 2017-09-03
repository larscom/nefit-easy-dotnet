using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NefitEasyDotNet
{
    class NefitEasyEncryption
    {
        readonly byte[] _chatKey;
        readonly RijndaelManaged _rijndael;

        public NefitEasyEncryption(string access, string password)
        {
            _rijndael = new RijndaelManaged
            {
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            };
            _chatKey = GenerateKey(new byte[] { 0x58, 0xf1, 0x8d, 0x70, 0xf6, 0x67, 0xc9, 0xc7, 0x9e, 0xf7, 0xde, 0x43, 0x5b, 0xf0, 0xf9, 0xb1, 0x55, 0x3b, 0xbb, 0x6e, 0x61, 0x81, 0x62, 0x12, 0xab, 0x80, 0xe5, 0xb0, 0xd3, 0x51, 0xfb, 0xb1 }, access, password);
        }

        byte[] Combine(byte[] inputBytes1, byte[] inputBytes2)
        {
            var inputBytes = new byte[inputBytes1.Length + inputBytes2.Length];
            Buffer.BlockCopy(inputBytes1, 0, inputBytes, 0, inputBytes1.Length);
            Buffer.BlockCopy(inputBytes2, 0, inputBytes, inputBytes1.Length, inputBytes2.Length);
            return inputBytes;
        }

        byte[] GenerateKey(byte[] magicKey, string idKeyUuid, string password)
        {
            var md5 = MD5.Create();
            return Combine(md5.ComputeHash(Combine(Encoding.Default.GetBytes(idKeyUuid), magicKey)), md5.ComputeHash(Combine(magicKey, Encoding.Default.GetBytes(password))));
        }

        public string Decrypt(string cipherData)
        {
            var base64Str = new List<byte>(Convert.FromBase64String(cipherData));
            var num = base64Str.Count % 8;
            for (var i = 0; i < num; i++)
            {
                base64Str.Add(0x00);
            }
            _rijndael.Key = _chatKey;
            var reader = new StreamReader(new CryptoStream(new MemoryStream(base64Str.ToArray()), _rijndael.CreateDecryptor(), CryptoStreamMode.Read));
            return reader.ReadToEnd().Trim('\0');
        }

        public string Encrypt(string data)
        {
            var hexString = new List<byte>(Encoding.Default.GetBytes(data));
            while (hexString.Count % 16 != 0)
            {
                hexString.Add(0x00);
            }
            _rijndael.Key = _chatKey;

            var stream = new CryptoStream(new MemoryStream(hexString.ToArray()), _rijndael.CreateEncryptor(), CryptoStreamMode.Read);
            var textBytes = new MemoryStream();
            stream.CopyTo(textBytes);
            return Convert.ToBase64String(textBytes.ToArray());
        }
    }
}
