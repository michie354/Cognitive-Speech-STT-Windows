using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Configuration;
using Newtonsoft.Json;

namespace SpeechToTextWPFSample
{
    public class TranslatorText
    {
        // TODO : 後々AppConfigに外出す
        static private string host = "https://api.cognitive.microsofttranslator.com";
        static private string path = "/translate?api-version=3.0";

        // Translate to japanese and English.
        static private string params_ = "&from=ja&to=en";


        // NOTE: Replace this example key with a valid subscription key.
        static private string subscriptionKey = ConfigurationManager.AppSettings["TranslatorSubscriptionKey"];

        static async public Task<string> Translate(string from)
        {
            System.Object[] body = new System.Object[] { new { Text = from } };
            var requestBody = JsonConvert.SerializeObject(body);

            string uri = host + path + params_;

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<List<TranslateResult>>(responseBody);

                return result[0].Translations[0].Text;
            }
        }
    }

    public class TranslateResult
    {
        //[JsonProperty("detectedLanguage")]
        //public IDictionary<string, string> detectedLanguage { get; set; }

        [JsonProperty("translations")]
        public IList<Translations> Translations { get; set; }

        public TranslateResult()
        {
            this.Translations = new List<Translations>();
            //this.detectedLaknguage = new Dictionary<string, string>();
        }
    }

    public class Translations
    {
        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("to")]
        public string To { get; set; }
    }
}
