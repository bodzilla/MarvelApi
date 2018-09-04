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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MarvelApi
{
    internal class Program
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static string _marvelApiPublicKey;
        private static string _marvelApiPrivateKey;
        private static string _googleAuthJsonPath;

        private static void Main(string[] args)
        {
            // Decrypt API key from config.
            try
            {
                _marvelApiPublicKey = ConfigurationManager.AppSettings["MarvelApiPublicKey"];
                _marvelApiPrivateKey = ConfigurationManager.AppSettings["MarvelApiPrivateKey"];
                _googleAuthJsonPath = ConfigurationManager.AppSettings["GoogleAuthJsonPath"];

                if (String.IsNullOrWhiteSpace(_marvelApiPublicKey) || String.IsNullOrWhiteSpace(_marvelApiPrivateKey) || String.IsNullOrWhiteSpace(_googleAuthJsonPath))
                {
                    throw new NullReferenceException();
                }
            }
            catch (Exception ex)
            {
                Log.Fatal("Could not retrieve API key(s).", ex);
                ShowTerminateMessage(1, "Could not retrieve API key(s).");
            }

            // Read argument(s) and action response.
            int characterId;
            switch (args.Length)
            {
                case 2 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    DisplayTopTenCharacters();
                    break;
                case 3 when args[0].Equals("marvel") && args[1].Equals("characters"):
                    if (!int.TryParse(args[2], out characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    DisplaySingleCharacter(characterId);
                    break;
                case 3 when args[0].Equals("marvel") && args[1].Equals("powers"):
                    if (!int.TryParse(args[2], out characterId)) ShowTerminateMessage(1, "Character ID is not a valid integer.");
                    JObject json = GetSingleCharacterPowers(characterId);
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
            JObject json = GetSingleCharacterPowers(characterId);

            Api api = new Api();
            string auth = File.ReadAllText(_googleAuthJsonPath);

            Stopwatch watch = Stopwatch.StartNew();
            string translatedDescription = api.TranslateText(auth, languageCode, json["character"]["description"].Value<string>());
            IList<string> translatedPowers = api.GetTranslatedFields(auth, languageCode, json);
            watch.Stop();
            long totalResponseTime = watch.ElapsedMilliseconds;
            long totalResponseSize = Encoding.UTF8.GetByteCount(translatedDescription) + Encoding.UTF8.GetByteCount(translatedPowers.ToString());

            // Update JSON.
            json["character"]["description"] = JToken.FromObject(translatedDescription);
            json["character"]["powers"] = JToken.FromObject(translatedPowers);

            // Total API calls + web requests + translate requests.
            json["requests"]["total"] = json["requests"]["total"].Value<int>() + translatedPowers.Count + 1;

            json["requests"]["total_response_time"] = json["requests"]["total_response_time"].Value<long>() + totalResponseTime;
            json["requests"]["average_response_time"] = json["requests"]["total_response_time"].Value<long>() / json["requests"]["total"].Value<int>();
            json["requests"]["total_response_size"] = json["requests"]["total_response_size"].Value<long>() + totalResponseSize;
            json["requests"]["average_response_size"] = json["requests"]["total_response_size"].Value<long>() / json["requests"]["total"].Value<int>();
            Console.WriteLine(json);
        }

        public static JObject GetSingleCharacterPowers(int characterId)
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
                string hash = api.GenerateHash(timeStamp, _marvelApiPublicKey, _marvelApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _marvelApiPublicKey, hash, requestString, characterId);

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
                string hash = api.GenerateHash(timeStamp, _marvelApiPublicKey, _marvelApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _marvelApiPublicKey, hash, requestString, characterId);

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

        private static void DisplayTopTenCharacters()
        {
            try
            {
                // Get settings.
                List<string> eTags = new List<string>();
                bool useCompression = bool.Parse(ConfigurationManager.AppSettings["UseCompression"]);
                bool useEtags = bool.Parse(ConfigurationManager.AppSettings["UseEtags"]);
                if (useEtags)
                {
                    string path = $@"{AppDomain.CurrentDomain.BaseDirectory}\Resources\TopTenCharactersEtags.txt";
                    if (File.Exists(path)) eTags.AddRange(File.ReadAllLines(path));
                }

                // Prepare and make request.
                Api api = new Api();
                DateTime timeStamp = DateTime.Now;
                int pageLimit = int.Parse(ConfigurationManager.AppSettings["PageLimit"]);
                int resultLimit = int.Parse(ConfigurationManager.AppSettings["ResultLimit"]);
                string requestString = ConfigurationManager.AppSettings["GetCharactersUrl"];
                string hash = api.GenerateHash(timeStamp, _marvelApiPublicKey, _marvelApiPrivateKey);
                string url = api.FormatCharactersUrl(timeStamp, _marvelApiPublicKey, hash, requestString);

                List<JToken> characters = new List<JToken>();
                int currentResult = 0;
                int totalRequests = 0;
                long totalResponseTime = 0;
                long totalResponseSize = 0;

                // Page through data and add to results.
                for (int i = 0; i < pageLimit; i++)
                {
                    string eTag = String.Empty;
                    if (useEtags && eTags.ElementAtOrDefault(i) != null) eTag = eTags[i];

                    totalRequests++;
                    Stopwatch watch = Stopwatch.StartNew();
                    JObject response = api.GetResults(out long size, useCompression, url, resultLimit, currentResult, eTag);
                    watch.Stop();
                    totalResponseTime += watch.ElapsedMilliseconds;
                    totalResponseSize += size;
                    currentResult += resultLimit;

                    // If the Etag exists and is the same as cache, load this and move on.
                    if (useEtags && response == null)
                    {
                        IList<JToken> charactersFromFile = LoadJsonCharacters(i);
                        characters.AddRange(charactersFromFile);
                        continue;
                    }

                    // Check if request is ok.
                    int code = (int)response["code"];
                    if (code != 200) throw new InvalidOperationException(response["status"].ToString());
                    IList<JToken> results = response["data"]["results"].ToList();
                    if (results.Count < 1) break; // This means we've reached the end of the results list.

                    string newEtag = response["etag"].Value<string>();
                    if (useEtags)
                    {
                        // Save the ETag to file.
                        if (eTags.ElementAtOrDefault(i) != null) eTags[i] = newEtag;
                        else eTags.Add(newEtag);
                        UpdateEtag(eTags);
                    }
                    characters.AddRange(results);
                    SaveCharacters(results, i);
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

        private static IList<JToken> LoadJsonCharacters(int page)
        {
            IList<JToken> characters = new List<JToken>();

            try
            {
                string path = $@"{AppDomain.CurrentDomain.BaseDirectory}\Resources\TopTenCharactersJson-{page}.json";
                if (!File.Exists(path))
                {
                    Log.Warn($"path doesn't exist: {path}");
                    return characters;
                }
                string charactersString = File.ReadAllText(path);
                characters = JObject.Parse(charactersString)["characters"].ToList();
            }
            catch (ConfigurationErrorsException ex)
            {
                Log.Fatal("Could not add/update characters.", ex);
                throw;
            }
            return characters;
        }

        private static void SaveCharacters(IList<JToken> characters, int page)
        {
            try
            {
                JObject json = JObject.Parse($@"
                {{
                    characters: {JToken.FromObject(characters)}
                }}");

                string path = $@"{AppDomain.CurrentDomain.BaseDirectory}\Resources";
                string file = $"TopTenCharactersJson-{page}.json";
                string fullPath = $@"{path}\{file}";
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.AppendAllText(fullPath, json.ToString());
            }
            catch (ConfigurationErrorsException ex)
            {
                Log.Fatal("Could not add/update characters.", ex);
                throw;
            }
        }

        public static void UpdateEtag(List<string> eTags)
        {
            try
            {
                string path = $@"{AppDomain.CurrentDomain.BaseDirectory}\Resources";
                string file = "TopTenCharactersEtags.txt";
                string fullPath = $@"{path}\{file}";
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);
                if (File.Exists(fullPath)) File.Delete(fullPath);
                File.WriteAllLines(fullPath, eTags);
            }
            catch (ConfigurationErrorsException ex)
            {
                Log.Fatal("Could not add/update ETags.", ex);
                throw;
            }
        }

        private static void ShowTerminateMessage(int exitCode, [Optional] string message)
        {
            if (!String.IsNullOrWhiteSpace(message)) Console.WriteLine(message);
            Environment.Exit(exitCode);
        }
    }
}
