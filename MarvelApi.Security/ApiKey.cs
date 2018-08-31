using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MarvelApi.Security
{
    /// <summary>
    /// Adapted from https://www.codeproject.com/Tips/1156169/Encrypt-Strings-with-Passwords-AES-SHA
    /// </summary>
    public class ApiKey
    {
        /// <summary>
        /// Encrypts API key using a password.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="unencryptedApiKey"></param>
        /// <returns></returns>
        public string Encrypt(string password, string unencryptedApiKey)
        {
            byte[][] keys = GetHashKeys(password);
            string encryptedKey = EncryptStringToBytes(unencryptedApiKey, keys[0], keys[1]);
            return encryptedKey;
        }

        /// <summary>
        /// Decrypts encrypted API key using a password.
        /// </summary>
        /// <param name="password"></param>
        /// <param name="encryptedApiKey"></param>
        /// <returns></returns>
        public string Decrypt(string password, string encryptedApiKey)
        {
            byte[][] keys = GetHashKeys(password);
            string decryptedApiKey = DecryptStringFromBytes(encryptedApiKey, keys[0], keys[1]);
            return decryptedApiKey;
        }

        /// <summary>
        /// Generate hash for request calls.
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <param name="apiPublicKey"></param>
        /// <param name="apiPrivateKey"></param>
        /// <returns></returns>
        public string GenerateHash(DateTime timeStamp, string apiPublicKey, string apiPrivateKey)
        {
            byte[] tsBytes = Encoding.ASCII.GetBytes(timeStamp.ToString(CultureInfo.InvariantCulture));
            byte[] apiPublicKeyBytes = Encoding.ASCII.GetBytes(apiPublicKey);
            byte[] apiPrivateKeyBytes = Encoding.ASCII.GetBytes(apiPrivateKey);

            // Must be in this order.
            byte[] bytes = CombineBytes(tsBytes, apiPrivateKeyBytes, apiPublicKeyBytes);

            string hash = GenerateHash(bytes);
            return hash.ToLower();
        }

        private static string GenerateHash(byte[] data)
        {
            StringBuilder hash = new StringBuilder();

            // Use input string to calculate MD5 hash.
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(data);

            // Convert the byte array to hexadecimal string.
            foreach (byte _byte in hashBytes) hash.Append(_byte.ToString("X2"));
            return hash.ToString();
        }

        private static byte[] CombineBytes(params byte[][] arrays)
        {
            // Combine multiple byte arrays.
            byte[] combinedBytes = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, combinedBytes, offset, array.Length);
                offset += array.Length;
            }
            return combinedBytes;
        }

        private static byte[][] GetHashKeys(string password)
        {
            byte[][] result = new byte[2][];
            Encoding enc = Encoding.UTF8;
            SHA256 sha2 = new SHA256CryptoServiceProvider();
            byte[] rawKey = enc.GetBytes(password);
            byte[] rawIv = enc.GetBytes(password);
            byte[] hashKey = sha2.ComputeHash(rawKey);
            byte[] hashIv = sha2.ComputeHash(rawIv);
            Array.Resize(ref hashIv, 16);
            result[0] = hashKey;
            result[1] = hashIv;
            return result;
        }

        private static string EncryptStringToBytes(string plainText, byte[] password, byte[] iv)
        {
            byte[] encrypted;

            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = password;
                aesAlg.IV = iv;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }
            return Convert.ToBase64String(encrypted);
        }

        private static string DecryptStringFromBytes(string cipherTextString, byte[] password, byte[] iv)
        {
            byte[] cipherText = Convert.FromBase64String(cipherTextString);
            string plaintext;

            using (Aes aesAlg = Aes.Create())
            {
                if (aesAlg == null) throw new ArgumentNullException();
                aesAlg.Key = password;
                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
            return plaintext;
        }
    }
}