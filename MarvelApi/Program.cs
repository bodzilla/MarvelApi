using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using log4net;
using MarvelApi.Api;
using MarvelApi.Security;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MarvelApi
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static readonly ApiKey ApiKey = new ApiKey();
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
                Log.Fatal("Could not resolve API key(s).", ex);
                ShowTerminateMessage(1, "Could not resolve API key(s).");
            }

            // Read argument(s) and action response.
            switch (args.Length)
            {
                case 2 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    DisplayTopCharacters();
                    break;
                case 3 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    if (!int.TryParse(args[2], out int characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
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
            try
            {
                Request request = new Request();
                DateTime timeStamp = DateTime.Now;

                // Prepare and make request.
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
                string url = request.FormatCharactersUrl(timeStamp, _decryptedApiPublicKey, hash, requestString, characterId);

                Stopwatch watch = Stopwatch.StartNew();
                JObject response = request.GetResults(url);
                watch.Stop();
                long totalResponseTime = watch.ElapsedMilliseconds;
                int totalResponseSize = Encoding.UTF8.GetByteCount(response.ToString());

                // Check if request is ok.
                int code = (int)response["code"];
                if (code != 200) throw new InvalidOperationException(response["status"].ToString());
                JToken character = response["data"]["results"][0];

                // Parse into JSON.
                JObject json = JObject.Parse($@"
                {{
                    ""character"":
                            {{
                                ""id"": {character["id"]},
                                ""name"": ""{character["name"]}"",
                                ""description"": ""{character["description"]}"",
                                ""thumbnail"": ""{character["thumbnail"]["path"]}.{character["thumbnail"]["extension"]}""
                            }},
                    ""requests"":
                            {{
                                ""total"": 1,
                                ""total_response_time"": {totalResponseTime},
                                ""average_response_time"": {totalResponseTime},
                                ""total_response_size"": {totalResponseSize},
                                ""average_response_size"": {totalResponseSize}
                            }}
                }}");

                Console.WriteLine(json);
            }
            catch (Exception ex)
            {
                Log.Fatal("Could not get/display single character.", ex);
                ShowTerminateMessage(1, "Could display single character.");
            }
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
                string url = request.FormatCharactersUrl(timeStamp, _decryptedApiPublicKey, hash, requestString);

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
                    JObject response = request.GetResults(url, resultLimit, currentResult);
                    watch.Stop();
                    totalResponseTime += watch.ElapsedMilliseconds;
                    totalResponseSize += Encoding.UTF8.GetByteCount(response.ToString());
                    currentResult += resultLimit;

                    // Check if request is ok.
                    int code = (int)response["code"];
                    if (code != 200) throw new InvalidOperationException(response["status"].ToString());

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
                Log.Fatal("Could not display top ten characters.", ex);
                ShowTerminateMessage(1, "Could not get/display top ten characters.");
            }
        }

        private static string GenerateHash(DateTime ts, string apiPublicKey, string apiPrivateKey) => ApiKey.GenerateHash(ts, apiPublicKey, apiPrivateKey);

        private static string DecryptApiKey(string password, string encryptedApikey) => ApiKey.Decrypt(password, encryptedApikey);

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
