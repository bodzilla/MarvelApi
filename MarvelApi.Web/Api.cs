using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Translation.V2;
using Newtonsoft.Json.Linq;

namespace MarvelApi.Web
{
    public class Api
    {
        /// <summary>
        /// Translate a list of strings using Google Translate API.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="languageCode"></param>
        /// <param name="originalJson"></param>
        /// <returns></returns>
        public IList<string> GetTranslatedFields(string auth, string languageCode, JObject originalJson)
        {
            // Translate fields.
            IList<string> powers = originalJson["character"]["powers"].Values<string>().ToList();
            return powers.Select(power => TranslateText(auth, languageCode, power)).ToList();
        }

        /// <summary>
        /// Translate text using Google's Translate API.
        /// </summary>
        /// <param name="auth"></param>
        /// <param name="languageCode"></param>
        /// <param name="text"></param>
        /// <returns></returns>
        public string TranslateText(string auth, string languageCode, string text)
        {
            // Set up credentials.
            GoogleCredential credential = GoogleCredential.FromJson(auth);
            TranslationClient translationClient = TranslationClient.Create(credential);

            // Translate field.
            return translationClient.TranslateText(text, languageCode).TranslatedText;
        }

        /// <summary>
        /// Make HTTP request to Marvel Wiki and get result as JSON object with optional skip list, compression flag and etags.
        /// </summary>
        /// <param name="size"></param>
        /// <param name="useCompression"></param>
        /// <param name="url"></param>
        /// <param name="limit"></param>
        /// <param name="offset"></param>
        /// <param name="eTag"></param>
        /// <returns>JSON object</returns>
        public JObject GetResults(out long size, bool useCompression, string url, [Optional] int? limit, [Optional] int? offset, [Optional] string eTag)
        {
            // Apply skip list filters.
            size = 0;
            if (limit != null && offset != null) url += $"&limit={limit}&offset={offset}";

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.ReadWriteTimeout = int.Parse(ConfigurationManager.AppSettings["TimeoutMilliSecs"]);
            request.Method = "GET";

            // Allows better performance if true.
            if (!String.IsNullOrWhiteSpace(eTag)) request.Headers.Add("If-None-Match", eTag);
            if (useCompression) request.Headers.Add("Accept-Encoding", "gzip");

            string data = String.Empty;
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (Stream stream = useCompression
                        ? new GZipStream(response.GetResponseStream() ?? throw new InvalidOperationException(),
                            CompressionMode.Decompress)
                        : response.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                        {
                            data = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch (WebException ex)
            {
                if (ex.Response == null || ex.Status != WebExceptionStatus.ProtocolError || !ex.Message.Contains("304")) throw;
                return null;
            }

            size = Encoding.UTF8.GetByteCount(data);
            JObject result = JObject.Parse(data);
            return result;
        }

        /// <summary>
        /// Formats the request string for characters for Marvel API call.
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

        /// <summary>
        /// Generates hash for Marvel API call.
        /// </summary>
        /// <param name="timeStamp"></param>
        /// <param name="apiPublicKey"></param>
        /// <param name="apiPrivateKey"></param>
        /// <returns></returns>
        public string GenerateHash(DateTime timeStamp, string apiPublicKey, string apiPrivateKey)
        {
            if (String.IsNullOrWhiteSpace(apiPublicKey) || String.IsNullOrWhiteSpace(apiPrivateKey)) throw new ArgumentException("Argument(s) is empty");
            byte[] tsBytes = Encoding.ASCII.GetBytes(timeStamp.ToString(CultureInfo.InvariantCulture));
            byte[] apiPublicKeyBytes = Encoding.ASCII.GetBytes(apiPublicKey);
            byte[] apiPrivateKeyBytes = Encoding.ASCII.GetBytes(apiPrivateKey);

            // Must be in this order.
            byte[] bytes = CombineBytes(tsBytes, apiPrivateKeyBytes, apiPublicKeyBytes);

            string hash = GenerateHash(bytes);
            return hash.ToLower();
        }

        private static string GenerateHash(byte[] data)
        {
            StringBuilder hash = new StringBuilder();

            // Use input string to calculate MD5 hash.
            MD5 md5 = MD5.Create();
            byte[] hashBytes = md5.ComputeHash(data);

            // Convert the byte array to hexadecimal string.
            foreach (byte _byte in hashBytes) hash.Append(_byte.ToString("X2"));
            return hash.ToString();
        }

        private static byte[] CombineBytes(params byte[][] arrays)
        {
            // Combine multiple byte arrays.
            byte[] combinedBytes = new byte[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, combinedBytes, offset, array.Length);
                offset += array.Length;
            }
            return combinedBytes;
        }
    }
}
