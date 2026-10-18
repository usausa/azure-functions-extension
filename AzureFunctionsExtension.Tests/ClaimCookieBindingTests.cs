namespace AzureFunctionsExtension.Tests;

using System.Reflection;
using System.Security.Claims;
using System.Text.Json;

using AzureFunctionsExtension.Filters;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

public sealed class ClaimCookieBindingTests
{
    private const string Head =
        """
        namespace Test;

        using System.Linq;
        using System.Security.Claims;
        using System.Threading.Tasks;
        using AzureFunctionsExtension.Annotations;
        using AzureFunctionsExtension.Filters;
        using Microsoft.AspNetCore.Mvc;

        public static class Describe
        {
            public static string Of(ClaimsPrincipal user) =>
                $"{user.Identity?.IsAuthenticated}|{user.Identity?.Name}|{user.FindFirst("sub")?.Value}|{string.Join(",", user.FindAll("role").Select(static x => x.Value))}|{user.IsInRole("admin")}";
        }

        public sealed class PassFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next) => next(context);
        }

        public sealed class UserFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "user-2"), new Claim("name", "Bob"), new Claim("role", "admin") }, "Custom", "name", "role"));
                return next(context);
            }
        }

        public sealed class DescribeFilter : IFunctionFilter
        {
            public ValueTask InvokeAsync(FunctionInvocationContext context, FunctionFilterDelegate next)
            {
                context.Result = new OkObjectResult("filter-" + Describe.Of(context.User));
                return ValueTask.CompletedTask;
            }
        }

        """;

    private static readonly Dictionary<string, Lazy<Assembly>> Assemblies = new(StringComparer.Ordinal)
    {
        [string.Empty] = new(static () => CompilationHelper.LoadAssembly(Source(string.Empty))),
        ["[Filter<PassFilter>]"] = new(static () => CompilationHelper.LoadAssembly(Source("[Filter<PassFilter>]")))
    };

    private static readonly Lazy<Assembly> CookieAssembly = new(static () => CompilationHelper.LoadAssembly(Head + """
        [AzureFunction]
        public sealed partial class Function
        {
            [HttpEndpoint("get", "session")]
            public IActionResult Session([FromCookie("session")] string? value) => new OkObjectResult(value);

            [HttpEndpoint("get", "session-upper")]
            public IActionResult SessionUpper([FromCookie("SESSION")] string? value) => new OkObjectResult(value);

            [HttpEndpoint("get", "a")]
            public IActionResult A([FromCookie("a")] string? value) => new OkObjectResult(value);

            [HttpEndpoint("get", "a-space")]
            public IActionResult ASpace([FromCookie("a b")] string? value) => new OkObjectResult(value);

            [HttpEndpoint("get", "a-encoded")]
            public IActionResult AEncoded([FromCookie("a%20b")] string? value) => new OkObjectResult(value);

            [HttpEndpoint("get", "b")]
            public IActionResult B([FromCookie("b")] string? value) => new OkObjectResult(value);
        }
        """));

    private static readonly Dictionary<string, string> CookieHandlers = new(StringComparer.Ordinal)
    {
        ["session"] = "Session_Handler",
        ["SESSION"] = "SessionUpper_Handler",
        ["a"] = "A_Handler",
        ["a b"] = "ASpace_Handler",
        ["a%20b"] = "AEncoded_Handler",
        ["b"] = "B_Handler"
    };

    private static string Source(string filter) =>
        Head + $$"""
            [AzureFunction]
            {{filter}}
            public sealed partial class Function
            {
                [HttpEndpoint("get", "me")]
                public IActionResult Me(
                    ClaimsPrincipal user,
                    [FromCookie("rt")] string? token = null,
                    [FromCookie("count")] int count = 0) => new OkObjectResult($"{Describe.Of(user)}|{token}|{count}");

                [HttpEndpoint("get", "principal")]
                public IActionResult Principal(ClaimsPrincipal user) => new OkObjectResult(user);
            }
            """;

    //--------------------------------------------------------------------------------
    // Binding
    //--------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task ClaimsAndCookiesAreBound(string filter)
    {
        var user = CreateJwtBearerUser("""{"sub":"user-1","name":"Alice","role":["admin","user"]}""");

        var result = await Invoke(Assemblies[filter].Value, "Me_Handler", "a=1; rt=abc; count=5", user);

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal("True|Alice|user-1|admin,user|True|abc|5", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task MissingValuesUseDefaults(string filter)
    {
        var result = await Invoke(Assemblies[filter].Value, "Me_Handler", null);

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal("False||||False||0", HandlerTestHost.ValueOf<string>(result));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task InvalidCookieReturnsBadRequest(string filter)
    {
        var result = await Invoke(Assemblies[filter].Value, "Me_Handler", "count=x");

        Assert.Equal(400, HandlerTestHost.StatusOf(result));
        Assert.Equal("Invalid parameter: count", HandlerTestHost.ValueOf<string>(result));
    }

    //--------------------------------------------------------------------------------
    // User
    //--------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task HttpContextUserIsPassed(string filter)
    {
        var user = CreateJwtBearerUser("""{"sub":"user-1"}""");

        var received = await InvokePrincipal(filter, user);

        Assert.Same(user, received);
    }

    [Fact]
    public async Task FilterReadsUserFromHttpContext()
    {
        var assembly = CompilationHelper.LoadAssembly(Source("[Filter<DescribeFilter>]"));

        var result = await Invoke(assembly, "Me_Handler", null, CreateJwtBearerUser("""{"sub":"user-1","role":"admin"}"""));

        Assert.Equal("filter-True||user-1|admin|True", HandlerTestHost.ValueOf<string>(result));
    }

    [Fact]
    public async Task FilterCanReplaceUser()
    {
        var assembly = CompilationHelper.LoadAssembly(Source("[Filter<UserFilter>]"));
        var services = HandlerTestHost.CreateServices();
        var request = HandlerTestHost.CreateRequest(services);
        request.HttpContext.User = CreateJwtBearerUser("""{"sub":"user-1"}""");

        var result = await CompilationHelper.InvokeAsync(assembly, "Me_Handler", request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));

        Assert.Equal("True|Bob|user-2|admin|True||0", HandlerTestHost.ValueOf<string>(result));
        Assert.Equal("Bob", request.HttpContext.User.Identity?.Name);
    }

    //--------------------------------------------------------------------------------
    // FunctionInvocationContext
    //--------------------------------------------------------------------------------

    [Fact]
    public void ContextUserReadsAndWritesHttpContextUser()
    {
        var request = new DefaultHttpContext().Request;
        var context = new FunctionInvocationContext { Request = request };
        var user = CreateJwtBearerUser("""{"sub":"user-1"}""");

        Assert.Same(request.HttpContext.User, context.User);

        context.User = user;

        Assert.Same(user, request.HttpContext.User);
        Assert.Same(user, context.User);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("message")]
    public void ContextUserWithoutHttpRequestIsUnauthenticated(string? request)
    {
        var context = new FunctionInvocationContext { Request = request };

        var user = context.User;

        Assert.False(user.Identity!.IsAuthenticated);
        Assert.Null(user.Identity.AuthenticationType);
        Assert.Null(user.Identity.Name);
        Assert.Empty(user.Claims);
        Assert.Same(user, context.User);
    }

    [Fact]
    public void ContextUserWithoutHttpRequestCanBeReplaced()
    {
        var context = new FunctionInvocationContext { Request = "message" };
        var user = CreateJwtBearerUser("""{"sub":"user-1"}""");

        context.User = user;

        Assert.Same(user, context.User);
    }

    //--------------------------------------------------------------------------------
    // JwtBearer
    //--------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserUsesNameAndRoleClaims(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"sub":"user-1","name":"Alice","role":"admin"}"""));

        Assert.True(user.Identity!.IsAuthenticated);
        Assert.Equal("AuthenticationTypes.Federation", user.Identity.AuthenticationType);
        Assert.Equal("Alice", user.Identity.Name);
        Assert.True(user.IsInRole("admin"));
        Assert.False(user.IsInRole("user"));
        Assert.Equal("user-1", user.FindFirst("sub")?.Value);
        Assert.True(user.HasClaim("role", "admin"));
        Assert.Equal(ClaimsIdentity.DefaultIssuer, user.FindFirst("sub")?.Issuer);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserHasArrayClaims(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"sub":"user-1","role":["admin","user"]}"""));

        Assert.Equal(["admin", "user"], user.FindAll("role").Select(static x => x.Value));
        Assert.Equal("admin", user.FindFirst("role")?.Value);
        Assert.True(user.IsInRole("user"));
        Assert.True(user.HasClaim("role", "user"));
        Assert.Null(user.Identity!.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserHasSingleElementArray(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"role":["admin"]}"""));

        Assert.Equal(["admin"], user.FindAll("role").Select(static x => x.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserSkipsEmptyArray(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"sub":"user-1","role":[]}"""));

        Assert.True(user.Identity!.IsAuthenticated);
        Assert.Empty(user.FindAll("role"));
        Assert.Null(user.FindFirst("role"));
        Assert.False(user.IsInRole("admin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserKeepsOtherValues(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"level":3,"active":true,"email":"","scope":"read write"}"""));

        Assert.Equal("3", user.FindFirst("level")?.Value);
        Assert.Equal("true", user.FindFirst("active")?.Value);
        Assert.True(user.HasClaim("email", string.Empty));
        Assert.Equal(["read write"], user.FindAll("scope").Select(static x => x.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserUsesIssuerClaim(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"iss":"https://issuer.example","sub":"user-1"}"""));

        Assert.Equal("https://issuer.example", user.FindFirst("sub")?.Issuer);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task JwtBearerUserUsesConfiguredClaimTypes(string filter)
    {
        var user = await InvokePrincipal(filter, CreateJwtBearerUser("""{"preferred_username":"carol","roles":["admin"]}""", "preferred_username", "roles"));

        Assert.Equal("carol", user.Identity!.Name);
        Assert.True(user.IsInRole("admin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[Filter<PassFilter>]")]
    public async Task UserWithoutTokenIsUnauthenticated(string filter)
    {
        var user = await InvokePrincipal(filter, null);

        Assert.False(user.Identity!.IsAuthenticated);
        Assert.Null(user.Identity.AuthenticationType);
        Assert.Null(user.Identity.Name);
        Assert.Empty(user.Claims);
        Assert.False(user.IsInRole("admin"));
    }

    //--------------------------------------------------------------------------------
    // Cookie
    //--------------------------------------------------------------------------------

    [Theory]
    [InlineData("Session=abc", "session", "abc")]
    [InlineData("Session=abc", "SESSION", "abc")]
    [InlineData("a=x%20y", "a", "x y")]
    [InlineData("a=%E3%81%82", "a", "あ")]
    [InlineData("a=x+y", "a", "x+y")]
    [InlineData("a=%zz", "a", "%zz")]
    [InlineData("a%20b=1", "a b", null)]
    [InlineData("a%20b=1", "a%20b", "1")]
    [InlineData("a=1; a=2", "a", "2")]
    [InlineData("a=1; A=2", "a", "2")]
    [InlineData("a=", "a", null)]
    [InlineData("a=1; a=", "a", "1")]
    [InlineData("a=x y", "a", null)]
    [InlineData("a=x y; b=2", "b", "2")]
    [InlineData("a=x,y", "a", null)]
    [InlineData("a=x\\y", "a", null)]
    [InlineData("a=x\"y", "a", null)]
    [InlineData("a=あ", "a", null)]
    [InlineData("a =1", "a", null)]
    [InlineData("a= 1", "a", null)]
    [InlineData(" a=1 ", "a", "1")]
    [InlineData("\r\n a=1", "a", "1")]
    [InlineData("a=b=c", "a", "b=c")]
    [InlineData("a=\"x\"", "a", "\"x\"")]
    [InlineData("a=\"%41\"", "a", "\"A\"")]
    [InlineData("a=\"x y\"", "a", null)]
    [InlineData("a=\"x", "a", null)]
    [InlineData("a=\"\"", "a", "\"\"")]
    public async Task CookiesAreReadLikeRequestCookies(string header, string name, string? expected)
    {
        var result = await Invoke(CookieAssembly.Value, CookieHandlers[name], header);

        Assert.Equal(200, HandlerTestHost.StatusOf(result));
        Assert.Equal(expected, HandlerTestHost.ValueOf<string>(result));
    }

    private static Task<IActionResult> Invoke(Assembly assembly, string handler, string? cookie, ClaimsPrincipal? user = null)
    {
        var services = HandlerTestHost.CreateServices();
        var request = HandlerTestHost.CreateRequest(
            services,
            headers: cookie is null ? null : new Dictionary<string, StringValues> { ["Cookie"] = cookie });
        if (user is not null)
        {
            request.HttpContext.User = user;
        }

        return CompilationHelper.InvokeAsync(assembly, handler, request, HandlerTestHost.CreateContext(services, TestContext.Current.CancellationToken));
    }

    private static async Task<ClaimsPrincipal> InvokePrincipal(string filter, ClaimsPrincipal? user)
    {
        var result = await Invoke(Assemblies[filter].Value, "Principal_Handler", null, user);
        return HandlerTestHost.ValueOf<ClaimsPrincipal>(result)!;
    }

    private static ClaimsPrincipal CreateJwtBearerUser(string payload, string nameClaimType = "name", string roleClaimType = "role")
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var issuer = root.TryGetProperty("iss", out var iss) ? iss.GetString()! : ClaimsIdentity.DefaultIssuer;
        var claims = new List<Claim>();
        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in property.Value.EnumerateArray())
                {
                    claims.Add(CreateClaim(property.Name, element, issuer));
                }
            }
            else
            {
                claims.Add(CreateClaim(property.Name, property.Value, issuer));
            }
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "AuthenticationTypes.Federation", nameClaimType, roleClaimType));
    }

    private static Claim CreateClaim(string type, JsonElement value, string issuer) =>
        value.ValueKind switch
        {
            JsonValueKind.String => new Claim(type, value.GetString()!, ClaimValueTypes.String, issuer, issuer),
            JsonValueKind.Number => new Claim(type, value.GetRawText(), ClaimValueTypes.Integer32, issuer, issuer),
            JsonValueKind.True or JsonValueKind.False => new Claim(type, value.GetRawText(), ClaimValueTypes.Boolean, issuer, issuer),
            _ => throw new NotSupportedException()
        };
}
