using System.Security.Claims;
using Platform.Security;
using Platform.Security.Authorization;

namespace Platform.Security.Tests;

public class PermissionEvaluatorTests
{
    private static ClaimsPrincipal User(params (string Type, string Value)[] claims) =>
        new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "test"));

    [Fact]
    public void Local_Admin_HasEveryPermission() =>
        Assert.True(PermissionEvaluator.HasPermission(User(("role", "Admin")), Permissions.Loans.Delete));

    [Theory]
    [InlineData("loans:read", true)]
    [InlineData("catalog:create", true)]
    [InlineData("loans:update", false)]
    [InlineData("catalog:delete", false)]
    public void Local_User_CanReadAndCreateOnly(string permission, bool expected) =>
        Assert.Equal(expected, PermissionEvaluator.HasPermission(User(("role", "User")), permission));

    [Fact]
    public void NoClaims_NoPermissions() =>
        Assert.False(PermissionEvaluator.HasPermission(User(), Permissions.Catalog.Read));

    [Fact]
    public void ClientCredentials_ScopeMapsToPermission()
    {
        var principal = User(("scope", "loans.read catalog.read"), ("client_id", "aggregator_service"));
        Assert.True(PermissionEvaluator.HasPermission(principal, Permissions.Loans.Read));
        Assert.False(PermissionEvaluator.HasPermission(principal, Permissions.Loans.Create));
    }

    [Fact]
    public void Scope_DotAndColonAreEquivalent() =>
        Assert.True(PermissionEvaluator.HasScope(User(("scope", "loans.read")), "loans:read"));

    [Fact]
    public void Keycloak_UmaPermissions_AreChecked()
    {
        const string json = """{"permissions":[{"rsname":"loans-api","scopes":["loans:read","loans:create"]}]}""";
        var principal = User(("authorization", json));
        Assert.True(PermissionEvaluator.HasPermission(principal, "loans:create"));
        Assert.False(PermissionEvaluator.HasPermission(principal, "loans:delete"));
    }

    [Fact]
    public void Keycloak_ClientRoles_AreChecked()
    {
        const string json = """{"loans-api":{"roles":["loans:read","loans:update"]},"account":{"roles":["view-profile"]}}""";
        var principal = User(("resource_access", json));
        Assert.True(PermissionEvaluator.HasPermission(principal, "loans:update"));
        Assert.False(PermissionEvaluator.HasPermission(principal, "loans:approve"));
    }

    [Fact]
    public void MalformedJsonClaims_DoNotGrantAnything() =>
        Assert.False(PermissionEvaluator.HasPermission(User(("authorization", "{not json"), ("resource_access", "[")), "loans:read"));
}
