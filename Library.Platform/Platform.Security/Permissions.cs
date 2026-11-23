namespace Platform.Security;

/// <summary>
/// Дозволи (permissions) платформи за матрицею з Частини 3. Формат «ресурс:дія». Предметна область — бібліотека:
/// Orders ≙ Loans (видачі), Products ≙ Catalog (книги), Payments — сервіс платежів (штрафи), заглушка.
/// </summary>
public static class Permissions
{
    public static class Loans
    {
        public const string Read = "loans:read";
        public const string Create = "loans:create";
        public const string Update = "loans:update";     // повернення книги
        public const string Delete = "loans:delete";     // скасування видачі
        public const string Approve = "loans:approve";   // зарезервовано матрицею (ендпоінта схвалення в домені немає)
    }

    public static class Catalog
    {
        public const string Read = "catalog:read";
        public const string Create = "catalog:create";
        public const string Update = "catalog:update";   // ціна, назва, списання
        public const string Delete = "catalog:delete";
        public const string Publish = "catalog:publish"; // зарезервовано матрицею
    }

    public static class Payments
    {
        public const string Read = "payments:read";
        public const string Process = "payments:process";
        public const string Refund = "payments:refund";
    }
}

/// <summary>
/// Scopes IdentityServer (OAuth 2.0) для M2M-викликів і user-токенів. Назви через крапку; scope «loans.read»
/// відповідає permission «loans:read» (див. PermissionAuthorizationHandler).
/// </summary>
public static class ApiScopes
{
    public const string LoansRead = "loans.read";
    public const string LoansCreate = "loans.create";
    public const string LoansUpdate = "loans.update";
    public const string LoansDelete = "loans.delete";

    public const string CatalogRead = "catalog.read";
    public const string CatalogCreate = "catalog.create";
    public const string CatalogUpdate = "catalog.update";
    public const string CatalogDelete = "catalog.delete";

    public const string ReviewsRead = "reviews.read";
    public const string ReviewsWrite = "reviews.write";

    public const string PaymentsRead = "payments.read";
    public const string PaymentsProcess = "payments.process";
    public const string PaymentsRefund = "payments.refund";

    public static readonly string[] All =
    [
        LoansRead, LoansCreate, LoansUpdate, LoansDelete,
        CatalogRead, CatalogCreate, CatalogUpdate, CatalogDelete,
        ReviewsRead, ReviewsWrite,
        PaymentsRead, PaymentsProcess, PaymentsRefund
    ];
}

/// <summary>Імена ApiResource (audience) в IdentityServer.</summary>
public static class ApiResourceNames
{
    public const string Loans = "loans_api";
    public const string Catalog = "catalog_api";
    public const string Reviews = "reviews_api";
    public const string Payments = "payments_api";
    public const string Aggregator = "aggregator_api";
}
