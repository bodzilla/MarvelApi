using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;

namespace MarvelApi.Api
{
    public class Request
    {
        /// <summary>
        /// Make HTTP request and get result as JSON object.
        /// </summary>
        /// <param name="url"></param>
        /// <returns>JSON object</returns>
        public JObject GetResult(string url)
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
        /// Formats the request string.
        /// </summary>
        /// <param name="ts"></param>
        /// <param name="apiPublicKey"></param>
        /// <param name="hash"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public string ToUrl(DateTime ts, string apiPublicKey, string hash, string url)
        {
            return $"{url}?ts={ts.ToString(CultureInfo.InvariantCulture)}&apikey={apiPublicKey}&hash={hash}";
        }
    }
}
