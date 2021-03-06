﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SocketFactory.Encrypt
{
    internal class Encryption
    {
        #region global variables

        public const int BUFFER_SEGMENT_SIZE = 65536;

        private static readonly byte[] _localIvArray =
            {   1, 26, 103, 66, 166, 3, 241, 199, 65, 60, 170, 186, 114, 222, 44, 132 };


        private const string SALT = "aog827a71e3if5TT9e";
        private const int KEY_SIZE = 128;
        private const int HASH_SIZE = KEY_SIZE / 8;
        #endregion

        private byte[] _password;

        /// <summary>
        /// Constructor
        /// </summary>
        public Encryption(string password)
        {
            _password = HashPassword(password);
        }

        /// <summary>
        /// Salts and hashes the given password with SHA512 encryption.
        /// </summary>
        /// <param name="password">The password to be salted and hashed.
        /// </param>
        /// <returns>The hashed and salted password.
        /// </returns>
        private byte[] HashPassword(string password)
        {
            if(password == null)
            {
                password = "";
            }
            SHA512Managed shaM = new SHA512Managed();
            // Salt and hash the given password string into a byte array.
            string saltedPass = password + SALT;
            byte[] passwordArray = Encoding.ASCII.GetBytes(saltedPass);
            byte[] computedHash = shaM.ComputeHash(passwordArray);
            byte[] destination = new byte[HASH_SIZE];
            Array.Copy(computedHash, destination, destination.Length);
            return destination;
        }

        /// <summary>
        /// Initialize the AesManaged instance that is used for network encryption
        /// </summary>
        private static AesManaged InitNetworkAes(byte[] key)
        {
            AesManaged netAes = new AesManaged();
            netAes.KeySize = KEY_SIZE;
            netAes.Padding = PaddingMode.PKCS7;
            netAes.IV = _localIvArray;
            netAes.Key = key;
            return netAes;
        }

        public CryptoStreamWrapper CreateEncrypt(Stream innerStream)
        {
            return CreateEncrypt(innerStream, _password);
        }

        internal static CryptoStreamWrapper CreateEncrypt(Stream innerStream, byte[] key)
        {
            using (AesManaged aes = InitNetworkAes(key))
            {
                return new CryptoStreamWrapper(innerStream,
                    aes.CreateEncryptor(),
                    CryptoStreamMode.Write);
            }
        }

        public CryptoStreamWrapper CreateDecrypt(Stream innerStream)
        {
            return CreateDecrypt(innerStream, _password);
        }

        internal static CryptoStreamWrapper CreateDecrypt(Stream innerStream, byte[] key)
        {
            using (AesManaged aes = InitNetworkAes(key))
            {
                return new CryptoStreamWrapper(innerStream,
                    aes.CreateDecryptor(),
                    CryptoStreamMode.Read);
            }
        }

        /// <summary>
        /// Encrypts a given object with either the local or network aes.
        /// </summary>
        /// <param name="data">Data to be encrypted.</param>
        /// <param name="key">Key used to initialize the Aes.</param>
        /// <param name="message">Error message</param>
        /// <returns>
        /// Returns instance of the DataTransport class that contains the encrypted
        /// object.
        /// </returns>
        internal DataTransport Encrypt(Packet data, out string message)
        {
            message = "";
            List<byte> encryptedData = null;
            ICryptoTransform encryptor;

            using (AesManaged netAes = InitNetworkAes(_password))
            {
                encryptor = netAes.CreateEncryptor();
            }
            IFormatter formatter = new BinaryFormatter();

            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    using (CryptoStream crypStream = new CryptoStream(memStream, encryptor, CryptoStreamMode.Write))
                    {
                        formatter.Serialize(crypStream, data);
                    }
                    encryptedData = new List<byte>(memStream.ToArray());
                }
            }
            catch (Exception ex) // return error message and correct status code
            {
                message = ex.Message; // rrr handle the padding exception and output, could be an invalid password
                return null;
            }
            return new DataTransport(encryptedData);
        }

        /// <summary>
        /// Decryption method that decrypts data with either the local or network Aes.
        /// </summary>
        /// <param name="encryptedData">DataTranport containing data to be decrypted.</param>
        /// <param name="key">Key used to initialize the Aes.</param>
        /// <param name="message">Error Message</param>
        /// <returns>
        /// Returns an unecrypted object.
        /// </returns>
        internal T Decrypt<T>(DataTransport encryptedData, out string message)
        {
            message = "";
            T decryptedData = default(T);
            ICryptoTransform decryptor;
            using (AesManaged netAes = InitNetworkAes(_password))
            {
                decryptor = netAes.CreateDecryptor();
            }

            IFormatter formatter = new BinaryFormatter();

            try
            {
                using (MemoryStream memStream = new MemoryStream(encryptedData.Data.ToArray()))
                {
                    using (CryptoStream crypStream = new CryptoStream(memStream, decryptor, CryptoStreamMode.Read))
                    {
                        decryptedData = (T)formatter.Deserialize(crypStream);
                    }
                }
            }
            catch (Exception ex)
            {
                message = ex.Message; // rrr handle the padding exception and output, could be an invalid password
                return default(T);
            }
            return decryptedData;
        }
    }
}
