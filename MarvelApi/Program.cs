using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarvelApi.Security;

namespace MarvelApi
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string password = ConfigurationManager.AppSettings["Password"];
            string encryptedApiKey = ConfigurationManager.AppSettings["EncryptedApiKey"];

            ApiKey apiKey = new ApiKey();
            string decryptedApiKey = apiKey.Decrypt(password, encryptedApiKey);
        }
    }
}
