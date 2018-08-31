using System;
using MarvelApi.Security;

namespace MarvelApi.SecurityManager
{
    internal class Program
    {
        /// <summary>
        /// Generates an encrypted string from the original API Key.
        /// </summary>
        /// <param name="args"></param>
        private static void Main(string[] args)
        {
            string encryptedApiKey = String.Empty;

            try
            {
                string password = args[0];
                string unencryptedApiKey = args[1];
                encryptedApiKey = new ApiKey().Encrypt(password, unencryptedApiKey);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not run Security Manager - {ex}");
                Console.WriteLine("Press any key to exit Security Manager.");
                Console.ReadKey();
                Environment.Exit(1);
            }

            Console.WriteLine("Below is your encrypted API Key, store this in your main application configuration file.");
            Console.WriteLine(encryptedApiKey);
            Console.WriteLine(Environment.NewLine);
            Console.WriteLine("Press any key to exit Security Manager.");
            Console.ReadKey();
            Environment.Exit(0);
        }
    }
}
