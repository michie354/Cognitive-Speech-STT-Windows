using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Threading;
using System.Configuration;
using Newtonsoft.Json;

namespace SpeechToTextWPFSample

{
    public class TextAnalytics
    {
        // TODO : 後々AppConfigに外出す
        static private string host = "https://japaneast.api.cognitive.microsoft.com/text/analytics/v2.0";
        static private string path = "/sentiment";

        // NOTE: Replace this example key with a valid subscription key.
        static private string subscriptionKey = ConfigurationManager.AppSettings["TextAnalyticsSubscriptionKey"];

        static async public Task<string> PostSentiment(string text)
        {
            var body = new TextAnalyticsRequest();
            body.Documents.Add(new DocumentsRequest("en", "1", text));

            var requestBody = JsonConvert.SerializeObject(body);
            

            string uri = host + path;

            using (var client = new HttpClient())
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.RequestUri = new Uri(uri);
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", subscriptionKey);

                var response = await client.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<TextAnalyticsResponse>(responseBody);

                return result.Documents[0].Score;
            }
        }

        [STAThread]
        static void Main()
        {

            taskRun();
        }

        static public void taskRun()
        {
            string txt = "It's sunny.";
            var task = Task.Run(() => PostSentiment(txt));
            string res = task.Result;
        }

    }
}

public class TextAnalyticsRequest
{
    [JsonProperty("documents")]
    public IList<DocumentsRequest> Documents { get; set; }

    public TextAnalyticsRequest()
    {
        this.Documents = new List<DocumentsRequest>();
    }
}

public class DocumentsRequest
{
    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("text")]
    public string Text { get; set; }

    public DocumentsRequest(string language, string id, string text)
    {
        this.Language = language;
        this.Id = id;
        this.Text = text;
    }
}

public class TextAnalyticsResponse
{
    [JsonProperty("documents")]
    public IList<DocumentsResponse> Documents { get; set; }

    [JsonProperty("errors")]
    public IList<string> Errors { get; set; }

    public TextAnalyticsResponse()
    {
        this.Documents = new List<DocumentsResponse>();
    }
}

public class DocumentsResponse
{
    [JsonProperty("score")]
    public string Score { get; set; }

    [JsonProperty("id")]
    public string Id { get; set; }
}

