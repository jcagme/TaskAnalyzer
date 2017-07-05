namespace BuildTaskAnalyzer
{
    using System.Net.Http;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;
    using System.Collections.Generic;

    public class Classifier
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly string FunctionKey = "";

        public static List<Classification> GetClassifications(List<string> logs)
        {
            List<Classification> classification = new List<Classification>();

            string functionTriggerUrl = $"https://logclassifier.azurewebsites.net/api/classify_log?code={FunctionKey}";
            string body = JsonConvert.SerializeObject(logs, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            HttpContent content = new StringContent(body);
            HttpResponseMessage response = client.PostAsync(functionTriggerUrl, content).Result;

            if (response.IsSuccessStatusCode)
            {
                string responseContent = response.Content.ReadAsStringAsync().Result;
                classification = JsonConvert.DeserializeObject<List<Classification>>(responseContent);
            }
            
            return classification;
        }
    }
}
