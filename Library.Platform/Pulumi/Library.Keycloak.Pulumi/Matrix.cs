namespace Library.KeycloakIac;

/// <summary>Єдине джерело правди про дозволи: ресурс-сервери (Keycloak clients), їхні scope-и та матриця «роль → дозволи».</summary>
public static class Matrix
{
    public const string RealmName = "LibraryRealm";

    // Ресурс-сервер (client) → scope-и (UMA authorization scopes = client roles = permissions у токені)
    public static readonly IReadOnlyDictionary<string, string[]> ResourceServers = new Dictionary<string, string[]>
    {
        ["loans-api"] = ["loans:read", "loans:create", "loans:update", "loans:delete", "loans:approve"],
        ["catalog-api"] = ["catalog:read", "catalog:create", "catalog:update", "catalog:delete", "catalog:publish"],
        ["payments-api"] = ["payments:read", "payments:process", "payments:refund"]
    };

    // Реалм-роль → дозволи (scope-и). Composite-роль складається з відповідних клієнтських ролей.
    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["admin"] = ResourceServers.Values.SelectMany(s => s).ToArray(),
        ["manager"] =
        [
            "loans:read", "loans:create", "loans:update", "loans:approve",
            "catalog:read", "catalog:create", "catalog:update", "catalog:publish",
            "payments:read", "payments:process"
        ],
        ["user"] = ["loans:read", "loans:create", "catalog:read", "payments:read"],
        ["guest"] = ["catalog:read"]
    };

    // Тестові користувачі: логін → (роль, email). Початкові паролі тимчасові (змінюються при першому вході).
    public static readonly (string UserName, string Role, string Email)[] Users =
    [
        ("alice", "admin", "alice@library.local"),
        ("bob", "manager", "bob@library.local"),
        ("carol", "user", "carol@library.local"),
        ("dave", "guest", "dave@library.local")
    ];

    public static string ServerOf(string scope) =>
        ResourceServers.First(kv => kv.Value.Contains(scope)).Key;
}
