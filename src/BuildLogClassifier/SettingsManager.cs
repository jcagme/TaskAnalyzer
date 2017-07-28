/*
 This is a copy from the class in Helix's common project. If this solution ends up in there
 we should remove this file and reference common project instead.
 */

namespace BuildLogClassifier
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure;

    public static class SettingsManager
    {
        private static Lazy<KeyVaultSecrets> s_stagingConfigurationManager = new Lazy<KeyVaultSecrets>(CreateStagingKeyVault);
        private static Lazy<KeyVaultSecrets> s_prodConfigurationManager = new Lazy<KeyVaultSecrets>(CreateProdKeyVault);

        private static KeyVaultSecrets CreateProdKeyVault()
        {
            string clientId = CloudConfigurationManager.GetSetting("prodKeyVaultAppId");
            string clientSecret = CloudConfigurationManager.GetSetting("prodKeyVaultSecret");
            string vaultUrl = CloudConfigurationManager.GetSetting("prodKeyVaultUrl");

            return GetKeyVaultSecrets(clientId, clientSecret, vaultUrl);
        }

        private static KeyVaultSecrets CreateStagingKeyVault()
        {
            string clientId = CloudConfigurationManager.GetSetting("stagingKeyVaultAppId");
            string clientSecret = CloudConfigurationManager.GetSetting("stagingKeyVaultSecret");
            string vaultUrl = CloudConfigurationManager.GetSetting("stagingKeyVaultUrl");

            return GetKeyVaultSecrets(clientId, clientSecret, vaultUrl);
        }

        private static KeyVaultSecrets GetKeyVaultSecrets(string clientId, string clientSecret, string vaultUrl)
        {
            if (String.IsNullOrEmpty(clientId) ||
                String.IsNullOrEmpty(clientSecret) ||
                String.IsNullOrEmpty(vaultUrl))
            {
                return null;
            }

            return new KeyVaultSecrets(clientId, clientSecret, vaultUrl);
        }

        public static string GetStagingSetting(string key)
        {
            KeyVaultSecrets manager = s_stagingConfigurationManager.Value;
            string value = CloudConfigurationManager.GetSetting(key);
            if (manager == null)
            {
                return value;
            }
            else
            {
                return manager.GetSetting(value);
            }
        }

        public static string GetProdSetting(string key)
        {
            KeyVaultSecrets manager = s_prodConfigurationManager.Value;
            string value = CloudConfigurationManager.GetSetting(key);
            if (manager == null)
            {
                return value;
            }
            else
            {
                return manager.GetSetting(value);
            }
        }

        public static Task<string> GetSettingAsync(string key)
        {
            KeyVaultSecrets manager = s_prodConfigurationManager.Value;
            string value = CloudConfigurationManager.GetSetting(key);
            if (manager == null)
            {
                return Task.FromResult(value);
            }
            else
            {
                return manager.GetSettingAsync(value);
            }
        }
    }
}
