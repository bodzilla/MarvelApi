using System;
using MarvelApi.Security;

namespace MarvelApi.SecurityManager
{
    internal class Program
    {
        /// <summary>
        /// Generates an encrypted strings from the original API keys.
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            string password = args[0];
            string encryptedApiPublicKey = String.Empty;
            string encryptedApiPrivateKey = String.Empty;

            try
            {
                string unencryptedApiPublicKey = args[1];
                string unencryptedApiPrivateKey = args[2];
                encryptedApiPublicKey = EncryptApiKey(password, unencryptedApiPublicKey);
                encryptedApiPrivateKey = EncryptApiKey(password, unencryptedApiPrivateKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not run Security Manager - {ex}");
                Console.WriteLine("Press any key to exit Security Manager.");
                Console.ReadKey();
                Environment.Exit(1);
            }

            Console.WriteLine("Below is your password:");
            Console.WriteLine(password);
            Console.WriteLine(Environment.NewLine);

            Console.WriteLine("Below is your encrypted API public key:");
            Console.WriteLine(encryptedApiPublicKey);
            Console.WriteLine(Environment.NewLine);

            Console.WriteLine("Below is your encrypted API private key:");
            Console.WriteLine(encryptedApiPrivateKey);
            Console.WriteLine(Environment.NewLine);

            Console.WriteLine("Press any key to exit Security Manager.");
            Console.ReadKey();
            Environment.Exit(0);
        }

        private static string EncryptApiKey(string password, string unencryptedApikey) => new ApiKey().Encrypt(password, unencryptedApikey);
    }
}
