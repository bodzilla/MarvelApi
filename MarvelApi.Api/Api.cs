using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace MarvelApi.Web
{
    public class Api
    {
        /// <summary>
        /// Make HTTP request and get result as JSON object with optional skip list and compression flag.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="useCompression"></param>
        /// <param name="url"></param>
        /// <param name="limit"></param>
        /// <param name="offset"></param>
        /// <returns>JSON object</returns>
        public JObject GetResults(out long size, bool useCompression, string url, [Optional] int? limit, [Optional] int? offset)
        {
            // Apply skip list filters.
            size = 0;
            if (limit != null && offset != null) url += $"&limit={limit}&offset={offset}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.ReadWriteTimeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.Method = "GET";

            // Allows better performance if true.
            if (useCompression) request.Headers.Add("Accept-Encoding", "gzip");

            string data;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (Stream stream = useCompression ? new GZipStream(response.GetResponseStream() ?? throw new InvalidOperationException(), CompressionMode.Decompress) : response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                    {
                        data = reader.ReadToEnd();
                    }
                }
            }

            size = Encoding.UTF8.GetByteCount(data);
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
