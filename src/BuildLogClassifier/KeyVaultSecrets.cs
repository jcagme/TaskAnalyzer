/*
 This is a copy from the class in Helix's common project. If this solution ends up in there
 we should remove this file and reference common project instead.
 */

namespace BuildLogClassifier
{
    using Microsoft.Azure.KeyVault;
    using Microsoft.Azure.KeyVault.Models;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using System;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Linq;
    using System.Collections.Generic;

    public sealed class KeyVaultSecrets
    {
        private static readonly Regex s_keyVaultReferenceRegex = new Regex(@"\[vault\((?<key>.*)\)\]");
        private string _clientId;
        private string _clientSecret;
        private string _url;
        private TokenCache _cache;
        private readonly Lazy<KeyVaultClient> _lazyClient;

        public KeyVaultSecrets(string adClient, string adSecret, string vaultUrl)
        {
            _clientId = adClient;
            _clientSecret = adSecret;
            _url = vaultUrl;
            _cache = new TokenCache();
            _lazyClient = !string.IsNullOrEmpty(_clientId) ? new Lazy<KeyVaultClient>(GetKeyVaultClient) : null;
        }

        private KeyVaultClient GetKeyVaultClient()
        {
            return new KeyVaultClient(Authenticate);
        }

        private async Task<string> Authenticate(string authority, string resource, string scope)
        {
            ClientCredential creds = new ClientCredential(_clientId, _clientSecret);
            AuthenticationContext context = new AuthenticationContext(authority, _cache);
            AuthenticationResult result = await context.AcquireTokenAsync(resource, creds).ConfigureAwait(false);

            if (result == null)
            {
                throw new InvalidOperationException("Failed to authenticate with KeyVault; please check your credentials");
            }
            else
            {
                return result.AccessToken;
            }
        }

        public async Task<string> GetSecretAsync(string secretName)
        {
            if (_lazyClient == null)
            {
                throw new InvalidOperationException("No clientId provided");
            }

            KeyVaultClient client = _lazyClient.Value;
            SecretBundle s = await client.GetSecretAsync(_url, secretName).ConfigureAwait(false);
            return s.Value;
        }

        public string GetSecret(string secretName)
        {
            return GetSecretAsync(secretName).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public string GetSetting(string value)
        {
            if (value == null)
                return null;

            value = s_keyVaultReferenceRegex.Replace(value, m =>
            {
                string k = m.Groups["key"].Value;
                return GetSecretAsync(k).ConfigureAwait(false).GetAwaiter().GetResult();
            });
            return value;
        }

        public async Task<string> GetSettingAsync(string value)
        {
            if (value == null)
                return null;

            MatchCollection allMatches = s_keyVaultReferenceRegex.Matches(value);
            List<string> matchList = allMatches.Cast<Match>().Select(m => m.Groups["key"].Value).ToList();
            string[] replacements = await Task.WhenAll(matchList.Select(GetSecretAsync));

            Dictionary<string, string> replacementDictionary = matchList.Select((key, i) => new { key, value = replacements[i] }).ToDictionary(p => p.key, p => p.value);

            return s_keyVaultReferenceRegex.Replace(value, m => replacementDictionary[m.Groups["key"].Value]);
        }
    }
}
