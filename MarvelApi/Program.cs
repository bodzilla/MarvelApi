using System;
using System.Configuration;
using MarvelApi.Security;

namespace MarvelApi
{
    internal class Program
    {
        private static string _decryptedApiKey;

        private static void Main(string[] args)
        {
            // Decrypt API key from config.
            try
            {
                string password = ConfigurationManager.AppSettings["Password"];
                string encryptedApiKey = ConfigurationManager.AppSettings["EncryptedApiKey"];
                _decryptedApiKey = DecryptApiKey(password, encryptedApiKey);
            }
            catch (Exception ex)
            {
                ErrorTerminateMessage($"Could not decrypt API key - {ex}");
            }

            // 1A) The top 10 Marvel character IDs in an array of integers.
            // A top character is the one with the most number of appearances on comics and stories;

        }

        private static string DecryptApiKey(string password, string encryptedApikey) => new ApiKey().Decrypt(password, encryptedApikey);

        private static void ErrorTerminateMessage(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
}
