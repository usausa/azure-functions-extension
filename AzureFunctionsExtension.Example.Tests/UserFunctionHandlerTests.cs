namespace AzureFunctionsExtension.Example.Tests;

using System.Collections.Generic;
using System.Text;

using AzureFunctionsExtension.Example.Middleware;

using Microsoft.Extensions.Primitives;

public sealed class UserFunctionHandlerTests
{
    [Fact]
    public async Task MeHandlerAuthenticatedUserReturns200()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["Cookie"] = "theme=dark" });
        req.HttpContext.User = ClientPrincipalMiddleware.Parse(Encode("""{"auth_typ":"aad","claims":[{"typ":"sub","val":"user-1"},{"typ":"roles","val":"admin"}],"role_typ":"roles"}"""));

        var result = await UserFunction.Me_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal("""{"Id":"user-1","Admin":true,"Theme":"dark"}""", HandlerTestHost.Json(HandlerTestHost.ValueOf<object>(result)!));
    }

    [Fact]
    public async Task MeHandlerUnauthenticatedUserReturns401()
    {
        var services = HandlerTestHost.CreateServices();
        var req = HandlerTestHost.CreateRequest(
            services,
            headers: new Dictionary<string, StringValues> { ["Cookie"] = "theme=dark" });

        var result = await UserFunction.Me_Handler(req, HandlerTestHost.CreateContext(services));

        Assert.Equal(401, HandlerTestHost.StatusOf(result));
    }

    [Fact]
    public void ParseCreatesUserFromClientPrincipal()
    {
        var user = ClientPrincipalMiddleware.Parse(Encode("""{"auth_typ":"aad","claims":[{"typ":"sub","val":"user-1"},{"typ":"name","val":"Alice"},{"typ":"roles","val":"admin"},{"typ":"roles","val":"user"}],"name_typ":"name","role_typ":"roles"}"""));

        Assert.True(user.Identity!.IsAuthenticated);
        Assert.Equal("aad", user.Identity.AuthenticationType);
        Assert.Equal("Alice", user.Identity.Name);
        Assert.True(user.IsInRole("admin"));
        Assert.True(user.IsInRole("user"));
        Assert.Equal("user-1", user.FindFirst("sub")?.Value);
    }

    [Fact]
    public void ParseWithoutAuthenticationTypeIsUnauthenticated()
    {
        var user = ClientPrincipalMiddleware.Parse(Encode("""{"claims":[{"typ":"sub","val":"user-1"}]}"""));

        Assert.False(user.Identity!.IsAuthenticated);
        Assert.Equal("user-1", user.FindFirst("sub")?.Value);
    }

    private static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
}
