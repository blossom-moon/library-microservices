using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace Library.KeycloakIac;

/// <summary>Фабричні методи для типових клієнтів: прибирають повторення в LibraryStack.</summary>
public static class ClientExtensions
{
    /// <summary>Ресурс-сервер (API): confidential-клієнт із увімкненими Authorization Services; токени для нього не видаються напряму.</summary>
    public static Client CreateResourceServer(Output<string> realmId, string clientId, string description) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Description = description,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = true,    // потрібно для Authorization Services
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                AllowRemoteResourceManagement = true,
                KeepDefaults = false
            }
        });

    /// <summary>Клієнт для користувачів (Authorization Code + PKCE). Direct Access Grants (password) увімкнено лише для автоматичної перевірки матриці.</summary>
    public static Client CreateUserClient(Output<string> realmId, string clientId, string[] redirectUris, bool isPublic) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Enabled = true,
            AccessType = isPublic ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = true,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirectUris,
            WebOrigins = ["+"],
            FullScopeAllowed = true           // у токен потрапляють усі клієнтські ролі користувача (resource_access)
        });

    /// <summary>Машинний клієнт (Client Credentials, service account).</summary>
    public static Client CreateServiceClient(Output<string> realmId, string clientId) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            ServiceAccountsEnabled = true
        });
}
