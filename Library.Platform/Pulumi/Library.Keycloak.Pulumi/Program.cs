using Library.KeycloakIac;

// Точка входу Pulumi: pulumi up → LibraryStack.Create() створює realm, ресурс-сервери, ролі, політики, клієнтів і користувачів
return await Pulumi.Deployment.RunAsync(LibraryStack.Create);
