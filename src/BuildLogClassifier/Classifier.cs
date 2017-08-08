namespace BuildLogClassifier
{
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    public class Classifier
    {
        private static HttpClient _client = new HttpClient();

        public static async Task<List<Classification>> GetClassificationsAsync(List<string> logs)
        {
            string functionKey = SettingsManager.GetStagingSetting("ClassifierFunctionKey");
            List<Classification> classification = new List<Classification>();

            string functionTriggerUrl = $"https://logclassifier.azurewebsites.net/api/classify_log?code={functionKey}";
            string body = JsonConvert.SerializeObject(logs, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            HttpContent content = new StringContent(body);
            HttpResponseMessage response = await _client.PostAsync(functionTriggerUrl, content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                classification = JsonConvert.DeserializeObject<List<Classification>>(responseContent);
            }
            
            return classification;
        }
    }
}
