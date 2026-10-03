using System.Security.Claims;
using System.Text.Json;
using Elsa.Studio.Login.Contracts;
using Microsoft.AspNetCore.WebUtilities;

namespace ElsaStudio;

/// <summary>
/// Replaces the JWT parser of Elsa.Studio.Login.BlazorWasm 3.8.x, which keeps the JSON quotes around string claim values
/// (e.g. <c>permissions</c> becomes <c>"*"</c> instead of <c>*</c>), hiding permission-gated menus such as Users and Roles.
/// </summary>
public class JwtParser : IJwtParser
{
    public IEnumerable<Claim> Parse(string jwt)
    {
        using var document = JsonDocument.Parse(WebEncoders.Base64UrlDecode(jwt.Split('.')[1]));
        var claims = new List<Claim>();

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
                claims.AddRange(property.Value.EnumerateArray().Select(item => CreateClaim(property.Name, item)));
            else
                claims.Add(CreateClaim(property.Name, property.Value));
        }

        return claims;
    }

    private static Claim CreateClaim(string type, JsonElement value) =>
        new(type, value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText());
}
