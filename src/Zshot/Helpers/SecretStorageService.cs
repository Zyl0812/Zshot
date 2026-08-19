using System;
using Windows.Security.Credentials;

namespace Zshot.Helpers;

internal static class SecretStorageService
{
    private const string Resource = "Zshot.Translation";

    public static void Save(string key, string? secret)
    {
        var vault = new PasswordVault();
        try
        {
            foreach (var item in vault.FindAllByResource(Resource))
            {
                if (item.UserName == key)
                {
                    vault.Remove(item);
                }
            }
        }
        catch (Exception)
        {
            // no existing secret
        }

        if (!string.IsNullOrEmpty(secret))
        {
            vault.Add(new PasswordCredential(Resource, key, secret));
        }
    }

    public static string? Load(string key)
    {
        try
        {
            var vault = new PasswordVault();
            var cred = vault.Retrieve(Resource, key);
            cred.RetrievePassword();
            return cred.Password;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
