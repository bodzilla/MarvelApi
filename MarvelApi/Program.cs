using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using log4net;
using MarvelApi.Web;
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
            int characterId;
            switch (args.Length)
            {
                case 2 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    DisplayTopCharacters();
                    break;
                case 3 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    if (!int.TryParse(args[2], out characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    DisplaySingleCharacter(characterId);
                    break;
                case 3 when args[0].Equals("marvel") && args[1].Equals("powers"):
                    if (!int.TryParse(args[2], out characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    JObject json = DisplaySingleCharacterPowers(characterId);
                    Console.WriteLine(json);
                    break;
                case 4 when args[0].Equals("marvel") && args[1].Equals("powers"):
                    if (!int.TryParse(args[2], out characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    DisplayTranslatedSingleCharacterPowers(characterId, args[3]);
                    break;
                default:
                    ShowTerminateMessage(1, "Invalid argument(s) given.");
                    break;
            }
            ShowTerminateMessage(0);
        }

        private static void DisplayTranslatedSingleCharacterPowers(int characterId, string languageCode)
        {
            JObject json = DisplaySingleCharacterPowers(characterId);

            Api api = new Api();
            string googleAuthJsonPath = ConfigurationManager.AppSettings["GoogleAuthJsonPath"];
            string auth = File.ReadAllText(googleAuthJsonPath);

            Stopwatch watch = Stopwatch.StartNew();
            string translatedDescription = api.TranslateText(auth, languageCode, json["character"]["description"].Value<string>());
            IList<string> translatedPowers = api.GetTranslatedFields(auth, languageCode, json);
            watch.Stop();
            long totalResponseTime = watch.ElapsedMilliseconds;
            long totalResponseSize = Encoding.UTF8.GetByteCount(translatedPowers.ToString());

            // Update JSON.
            json["character"]["description"] = JToken.FromObject(translatedDescription);
            json["character"]["powers"] = JToken.FromObject(translatedPowers);
            json["requests"]["total"] = json["requests"]["total"].Value<int>() + translatedPowers.Count;
            json["requests"]["total_response_time"] = json["requests"]["total_response_time"].Value<long>() + totalResponseTime;
            json["requests"]["average_response_time"] = json["requests"]["total_response_time"].Value<long>() / json["requests"]["total"].Value<int>();
            json["requests"]["total_response_size"] = json["requests"]["total_response_size"].Value<long>() + totalResponseSize;
            json["requests"]["average_response_size"] = json["requests"]["total_response_size"].Value<long>() / json["requests"]["total"].Value<int>();
            Console.WriteLine(json);
        }

        public static JObject DisplaySingleCharacterPowers(int characterId)
        {
            JObject json = new JObject();
            string name = String.Empty;

            try
            {
                Api api = new Api();
                DateTime timeStamp = DateTime.Now;
                long totalResponseSize;
                long totalResponseTime = 0;

                // Prepare and make request.
                bool useCompression = bool.Parse(ConfigurationManager.AppSettings["UseCompression"]);
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _decryptedApiPublicKey, hash, requestString, characterId);

                Stopwatch w1 = Stopwatch.StartNew();
                JObject character = api.GetResults(out var apiRequestSize, useCompression, url);
                w1.Stop();
                totalResponseTime += w1.ElapsedMilliseconds;

                // Check if request is ok.
                int code = (int)character["code"];
                if (code != 200) throw new InvalidOperationException(character["status"].ToString());

                IList<string> powers;
                name = character["data"]["results"][0]["name"].ToString();

                try
                {
                    // Get wiki link and extract/format name.
                    string wikiUrl = character["data"]["results"][0]["urls"].Values<JObject>().FirstOrDefault(x => x["type"].Value<string>().Equals("wiki"))?["url"].ToString();

                    Scraper scraper = new Scraper();

                    Stopwatch w2 = Stopwatch.StartNew();
                    string profileUrl = scraper.GetProfileUrl(out var wikiPageSize, wikiUrl);
                    w2.Stop();
                    totalResponseTime += w2.ElapsedMilliseconds;

                    Stopwatch w3 = Stopwatch.StartNew();
                    powers = scraper.GetSingleCharacterPowers(out var profilePageSize, profileUrl);
                    w3.Stop();
                    totalResponseTime += w3.ElapsedMilliseconds;

                    totalResponseSize = apiRequestSize + wikiPageSize + profilePageSize;
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not reach single character wiki for {name}.", ex);
                    throw;
                }

                // Parse into JSON.
                json = JObject.Parse($@"
                {{
                    ""character"":
                            {{
                                ""id"": {character["data"]["results"][0]["id"]},
                                ""name"": ""{name}"",
                                ""description"": ""{character["data"]["results"][0]["description"]}"",
                                ""powers"": {JsonConvert.SerializeObject(powers)},
                                ""thumbnail"": ""{character["data"]["results"][0]["thumbnail"]["path"]}.{character["data"]["results"][0]["thumbnail"]["extension"]}""
                            }},
                    ""requests"":
                            {{
                                ""total"": 3,
                                ""total_response_time"": {totalResponseTime},
                                ""average_response_time"": {totalResponseTime / 3},
                                ""total_response_size"": {totalResponseSize},
                                ""average_response_size"": {totalResponseSize / 3}
                            }}
                }}");
            }
            catch (Exception ex)
            {
                Log.Fatal($"Could not get/display single character powers for {name}.", ex);
                ShowTerminateMessage(1, $"Could not get/display single character powers for {name}.");
            }
            return json;
        }

        private static void DisplaySingleCharacter(int characterId)
        {
            try
            {
                Api api = new Api();
                DateTime timeStamp = DateTime.Now;

                // Prepare and make request.
                bool useCompression = bool.Parse(ConfigurationManager.AppSettings["UseCompression"]);
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _decryptedApiPublicKey, hash, requestString, characterId);

                Stopwatch watch = Stopwatch.StartNew();
                JObject response = api.GetResults(out long totalResponseSize, useCompression, url);
                watch.Stop();
                long totalResponseTime = watch.ElapsedMilliseconds;

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
                Api api = new Api();
                DateTime timeStamp = DateTime.Now;

                // Prepare and make request.
                bool useCompression = bool.Parse(ConfigurationManager.AppSettings["UseCompression"]);
                int pageLimit = int.Parse(ConfigurationManager.AppSettings["PageLimit"]);
                int resultLimit = int.Parse(ConfigurationManager.AppSettings["ResultLimit"]);
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = GenerateHash(timeStamp, _decryptedApiPublicKey, _decryptedApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _decryptedApiPublicKey, hash, requestString);

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
                    JObject response = api.GetResults(out long size, useCompression, url, resultLimit, currentResult);
                    watch.Stop();
                    totalResponseTime += watch.ElapsedMilliseconds;
                    totalResponseSize += size;
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
