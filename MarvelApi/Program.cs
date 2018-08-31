using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
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
                var characters = GetTopTenMarvelCharacterIds();

            }
            catch (Exception ex)
            {
                ErrorTerminateMessage($"Could not get result for 1A - {ex}");
            }
        }

        /// <summary>
        /// 1A) The top 10 Marvel character IDs in an array of integers. A top character is the one with the most number of appearances on comics and stories.
        /// </summary>
        /// <returns>JSON object</returns>
        private static List<JToken> GetTopTenMarvelCharacterIds()
        {
            Request request = new Request();
            DateTime timeStamp = DateTime.Now;

            // Prepare and make request.
            int pageLimit = int.Parse(ConfigurationManager.AppSettings["PageLimit"]);
            int resultLimit = int.Parse(ConfigurationManager.AppSettings["ResultLimit"]);
            string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
            string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
            string url = request.ToUrl(timeStamp, _decryptedApiPublicKey, hash, requestString);

            // Page through data and add to results.
            List<JToken> characters = new List<JToken>();
            int currentResult = 0;
            for (int i = 0; i < pageLimit; i++)
            {
                JObject response = request.GetResult(url, resultLimit, currentResult);
                currentResult += resultLimit;
                IList<JToken> results = response["data"]["results"].ToList();
                if (results.Count < 1) break; // This means we've reached the end of the results list.
                characters.AddRange(results);
            }

            return characters;
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
