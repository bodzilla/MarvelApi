using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using MarvelApi.Api;
using MarvelApi.Security;
using Newtonsoft.Json;
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
                ShowTerminateMessage(1, $"Could not decrypt API keys - {ex}");
            }

            // Read argument(s) and action response.
            switch (args.Length)
            {
                case 1 when args[0].Equals("characters"):
                    DisplayTopCharacters();
                    break;
                case 1:
                    ShowTerminateMessage(1, "Invalid argument(s) given.");
                    break;
                case 2:
                    if (!args[0].Equals("characters")) ShowTerminateMessage(1, "Invalid argument(s) given.");
                    if (!int.TryParse(args[1], out int characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    DisplaySingleCharacter(characterId);
                    break;
                default:
                    ShowTerminateMessage(1, "Invalid argument(s) given.");
                    break;
            }
            ShowTerminateMessage(0);
        }

        private static void DisplaySingleCharacter(int characterId)
        {
            throw new NotImplementedException();
        }

        private static void DisplayTopCharacters()
        {
            try
            {
                Request request = new Request();
                DateTime timeStamp = DateTime.Now;

                // Prepare and make request.
                int pageLimit = int.Parse(ConfigurationManager.AppSettings["PageLimit"]);
                int resultLimit = int.Parse(ConfigurationManager.AppSettings["ResultLimit"]);
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
                string url = request.ToUrl(timeStamp, _decryptedApiPublicKey, hash, requestString);

                List<JToken> characters = new List<JToken>();
                int currentResult = 0;
                int totalRequests = 0;
                long totalResponseTime = 0;
                long totalResponseSize = 0;

                // Page through data and add to results.
                for (int i = 0; i < pageLimit; i++)
                {
                    totalRequests++;

                    Stopwatch watch = Stopwatch.StartNew();
                    JObject response = request.GetResult(url, resultLimit, currentResult);
                    watch.Stop();
                    totalResponseTime += watch.ElapsedMilliseconds;

                    totalResponseSize += Encoding.UTF8.GetByteCount(response.ToString());
                    currentResult += resultLimit;
                    IList<JToken> results = response["data"]["results"].ToList();
                    if (results.Count < 1) break; // This means we've reached the end of the results list.

                    characters.AddRange(results);
                }

                IDictionary<JToken, int> charactersDict = new Dictionary<JToken, int>();
                int totalComicsCharacters = 0;
                int totalStoriesCharacters = 0;

                foreach (JToken character in characters)
                {
                    int comics = (int)character["comics"]["available"];
                    int stories = (int)character["stories"]["available"];
                    charactersDict.Add(character, comics + stories);
                    if (comics > 0) totalComicsCharacters++;
                    if (stories > 0) totalStoriesCharacters++;
                }

                List<int> topTenCharacters = charactersDict.OrderByDescending(x => x.Value).Take(10).Select(y => y.Key["id"].ToObject<int>()).ToList();
                int totalCharacters = charactersDict.Count;

                // Parse into JSON.
                JObject json = JObject.Parse($@"
                {{
                    ""characters"":
                            {{
                                ""ids"": {JsonConvert.SerializeObject(topTenCharacters)},
                                ""total"": {totalCharacters},
                                ""total_in_comics"": {totalComicsCharacters},
                                ""total_in_stories"": {totalStoriesCharacters}
                            }},
                    ""requests"":
                            {{
                                ""total"": {totalRequests},
                                ""total_response_time"": {totalResponseTime},
                                ""average_response_time"": {totalResponseTime / totalRequests},
                                ""total_response_size"": {totalResponseSize},
                                ""average_response_size"": {totalResponseSize / totalRequests}
                            }}
                }}");

                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                ShowTerminateMessage(1, $"Could not compute results for displaying top 10 characters - {ex}");
            }
        }

        private static string GenerateHash(DateTime ts, string apiPublicKey, string apiPrivateKey) => new ApiKey().GenerateHash(ts, apiPublicKey, apiPrivateKey);

        private static string DecryptApiKey(string password, string encryptedApikey) => new ApiKey().Decrypt(password, encryptedApikey);

        private static void ShowTerminateMessage(int exitCode, [Optional] string message)
        {
            Console.WriteLine(Environment.NewLine);
            if (!String.IsNullOrWhiteSpace(message)) Console.WriteLine(message);
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
            Environment.Exit(exitCode);
        }
    }
}
