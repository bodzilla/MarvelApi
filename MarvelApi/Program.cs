using System;
using System.Configuration;
using MarvelApi.Api;
using MarvelApi.Security;
using Newtonsoft.Json.Linq;

namespace MarvelApi
{
    internal class Program
    {
        private static string _decryptedApiPublicKey;
        private static string _decryptedApiPrivateKey;

        private static void Main(string[] args)
        {
            // Decrypt API key from config.
            try
            {
                string password = ConfigurationManager.AppSettings["Password"];
                string encryptedApiPublicKey = ConfigurationManager.AppSettings["EncryptedApiPublicKey"];
                string encryptedApiPrivateKey = ConfigurationManager.AppSettings["EncryptedApiPrivateKey"];
                _decryptedApiPublicKey = DecryptApiKey(password, encryptedApiPublicKey);
                _decryptedApiPrivateKey = DecryptApiKey(password, encryptedApiPrivateKey);
            }
            catch (Exception ex)
            {
                ErrorTerminateMessage($"Could not decrypt API keys - {ex}");
            }

            // Call API and get the results for the following queries.
            try
            {
                var result = GetTopTenMarvelCharacterIds();
            }
            catch (Exception ex)
            {
                ErrorTerminateMessage($"Could not result for 1A - {ex}");
            }
        }

        /// <summary>
        /// 1A) The top 10 Marvel character IDs in an array of integers. A top character is the one with the most number of appearances on comics and stories.
        /// </summary>
        /// <returns>JSON object</returns>
        private static JObject GetTopTenMarvelCharacterIds()
        {
            Request request = new Request();
            DateTime timeStamp = DateTime.Now;

            // Prepare and make request.
            string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
            string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
            string url = request.ToUrl(timeStamp, _decryptedApiPublicKey, hash, requestString);
            JObject response = request.GetResult(url);

            // Sort top 10 Marvel characters.
            JObject result = new JObject();
            return result;
        }

        private static string GenerateHash(DateTime ts, string apiPublicKey, string apiPrivateKey) => new ApiKey().GenerateHash(ts, apiPublicKey, apiPrivateKey);

        private static string DecryptApiKey(string password, string encryptedApikey) => new ApiKey().Decrypt(password, encryptedApikey);

        private static void ErrorTerminateMessage(string message)
        {
            Console.WriteLine(message);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
}
