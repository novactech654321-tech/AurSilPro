using AurSil.Api.Models;
using AurSil.Api.Services;

namespace AurSil.Api.Mcp;

// The connector URL is /mcp/{key}. This runs before the MCP endpoint, rejects unknown or revoked keys
// and remembers whose account the request acts on.
public static class McpAuth
{
    private const string ItemKey = "mcp_user";

    public static UserRow User(HttpContext context) => (UserRow)context.Items[ItemKey]!;

    public static IApplicationBuilder UseMcpAuth(this IApplicationBuilder app) =>
        app.UseWhen(c => c.Request.Path.StartsWithSegments("/mcp"), branch => branch.Use(async (context, next) =>
        {
            var segments = context.Request.Path.Value!.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var key = segments.Length > 1 ? segments[1] : null;
            var user = await context.RequestServices.GetRequiredService<McpKeyService>().FindUserAsync(key);
            if (user is null)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new ApiMessage(
                    "This AurSil connector link is invalid or was disconnected. Create a new one in the AurSil app: Profile → AI Assistant."));
                return;
            }
            context.Items[ItemKey] = user;
            await next();
        }));
}
