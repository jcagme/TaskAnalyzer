namespace BuildLogClassifier
{
    using System.Net.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System.Collections.Generic;

    public class Classifier
    {
        private static HttpClient _client = new HttpClient();

        public static List<Classification> GetClassifications(List<string> logs)
        {
            string functionKey = SettingsManager.GetStagingSetting("ClassifierFunctionKey");
            List<Classification> classification = new List<Classification>();

            string functionTriggerUrl = $"https://logclassifier.azurewebsites.net/api/classify_log?code={functionKey}";
            string body = JsonConvert.SerializeObject(logs, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            HttpContent content = new StringContent(body);
            HttpResponseMessage response = _client.PostAsync(functionTriggerUrl, content).Result;

            if (response.IsSuccessStatusCode)
            {
                string responseContent = response.Content.ReadAsStringAsync().Result;
                classification = JsonConvert.DeserializeObject<List<Classification>>(responseContent);
            }
            
            return classification;
        }
    }
}
