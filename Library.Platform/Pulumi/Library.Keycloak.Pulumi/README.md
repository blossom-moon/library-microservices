# Library.Keycloak.Pulumi

Infrastructure as Code для Keycloak: realm `LibraryRealm`, ресурс-сервери (`loans-api`, `catalog-api`, `payments-api`) зі scope-ами,
реалм-ролі `admin`/`manager`/`user`/`guest` (composite), політики та дозволи, клієнти (`postman`, `swagger`, `aggregator-service`)
і тестові користувачі `alice`/`bob`/`carol`/`dave`. Матриця «роль → дозволи» — у `Matrix.cs`.

```powershell
# 1. Запустити AppHost (піднімає Keycloak на :8080), дочекатися Healthy
# 2. У цій теці:
pulumi login --local
pulumi stack init dev
pulumi config set --secret keycloak:password "<keycloak-admin-password>"   # parameter keycloak-admin-password з AppHost
pulumi up
pulumi stack output PostmanClientSecret --show-secrets
```
