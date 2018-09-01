using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using HtmlAgilityPack;

namespace MarvelApi.Web
{
    public class Scraper
    {
        public IList<string> GetSingleCharacterPowers(out long size, string profileUrl)
        {
            // Go to character profile page.
            HtmlDocument profilePage = new HtmlDocument();
            string profileHtml = MakeRequest(out size, profileUrl);
            profilePage.LoadHtml(profileHtml);

            // Scrape fields.
            var powerNodes = profilePage.DocumentNode.SelectNodes(@"//*[@id=""sets-2""]/div/div[2]/div/a") ?? profilePage.DocumentNode.SelectNodes(@"//*[@id=""sets-3""]/div/div[2]/div/a");
            IList<string> powers = powerNodes.Select(powerNode => WebUtility.HtmlDecode(powerNode.InnerText.Trim())).ToList();
            return powers;
        }

        public string GetProfileUrl(out long size, string wikiUrl)
        {
            // Goto wiki page.
            HtmlDocument wikiPage = new HtmlDocument();
            string wikiHtml = MakeRequest(out size, wikiUrl);
            wikiPage.LoadHtml(wikiHtml);

            // Get link for profile page.
            string profileLink = wikiPage.DocumentNode.SelectSingleNode(@"//*[@id=""masthead-1""]/div/div[2]/div/a[2]").Attributes["href"].Value.Trim();
            string profileUrl = profileLink.Insert(0, "https://www.marvel.com");
            return profileUrl;
        }

        private static string MakeRequest(out long size, string url)
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
                    using (StreamReader reader = new StreamReader(stream ?? throw new InvalidOperationException()))
                    {
                        data = reader.ReadToEnd();
                    }
                }
            }
            size = Encoding.UTF8.GetByteCount(data);
            return data;
        }
    }
}
