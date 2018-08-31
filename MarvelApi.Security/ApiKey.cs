using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MarvelApi.Security
{
    public class ApiKey
    {
        public string Encrypt(string password, string unencryptedApiKey)
        {
            string encryptedKey = null;
            byte[][] keys = GetHashKeys(password);

            try
            {
                encryptedKey = EncryptStringToBytes_Aes(unencryptedApiKey, keys[0], keys[1]);
            }
            catch (CryptographicException) { }
            catch (ArgumentNullException) { }

            return encryptedKey;
        }

        public string Decrypt(string password, string encryptedApiKey)
        {
            string decryptedApiKey = null;
            byte[][] keys = GetHashKeys(password);

            try
            {
                decryptedApiKey = DecryptStringFromBytes_Aes(encryptedApiKey, keys[0], keys[1]);
            }
            catch (CryptographicException) { }
            catch (ArgumentNullException) { }

            return decryptedApiKey;
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

        private static string EncryptStringToBytes_Aes(string plainText, byte[] password, byte[] iv)
        {
            if (plainText == null || plainText.Length <= 0) throw new ArgumentNullException(nameof(plainText));
            if (password == null || password.Length <= 0) throw new ArgumentNullException(nameof(password));
            if (iv == null || iv.Length <= 0) throw new ArgumentNullException(nameof(iv));

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

        private static string DecryptStringFromBytes_Aes(string cipherTextString, byte[] password, byte[] iv)
        {
            byte[] cipherText = Convert.FromBase64String(cipherTextString);

            if (cipherText == null || cipherText.Length <= 0) throw new ArgumentNullException(nameof(cipherTextString));
            if (password == null || password.Length <= 0) throw new ArgumentNullException(nameof(password));
            if (iv == null || iv.Length <= 0) throw new ArgumentNullException(nameof(iv));

            string plaintext;

            using (Aes aesAlg = Aes.Create())
            {
                if (aesAlg == null) return null;
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