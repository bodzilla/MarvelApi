using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;

namespace MarvelApi.Api
{
    public class Request
    {
        /// <summary>
        /// Make HTTP request and get result as JSON object with skip list.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="limit"></param>
        /// <param name="offset"></param>
        /// <returns>JSON object</returns>
        public JObject GetResults(string url, int limit, int offset)
        {
            url += $"&limit={limit}&offset={offset}";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.ReadWriteTimeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.Method = "GET";

            string data;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException("Stream returns null.")))
                    {
                        data = reader.ReadToEnd();
                    }
                }
            }

            JObject result = JObject.Parse(data);
            return result;
        }

        /// <summary>
        /// Make HTTP request and get a single result as JSON object.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>JSON object</returns>
        public JObject GetSingleResult(string url)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.ReadWriteTimeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.Method = "GET";

            string data;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException("Stream returns null.")))
                    {
                        data = reader.ReadToEnd();
                    }
                }
            }

            JObject result = JObject.Parse(data);
            return result;
        }

        /// <summary>
        /// Formats the request string for characters.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="apiPublicKey"></param>
        /// <param name="hash"></param>
        /// <param name="url"></param>
        /// <param name="characterId"></param>
        /// <returns></returns>
        public string FormatCharactersUrl(DateTime ts, string apiPublicKey, string hash, string url, [Optional] int characterId)
        {
            return characterId != 0 ? $"{url}/{characterId}?ts={ts.ToString(CultureInfo.InvariantCulture)}&apikey={apiPublicKey}&hash={hash}" : $"{url}?ts={ts.ToString(CultureInfo.InvariantCulture)}&apikey={apiPublicKey}&hash={hash}";
        }
    }
}
