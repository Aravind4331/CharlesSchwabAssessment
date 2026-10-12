using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CharlesSchwabAssessment.Security
{
    /// <summary>
    /// Configuration options for the API key authentication scheme.
    /// </summary>
    public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "ApiKey";

        /// <summary>
        /// The expected API key value. Requests must present this value via the
        /// <see cref="HeaderName"/> header to be authenticated.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// The HTTP header name that must carry the API key.
        /// </summary>
        public string HeaderName { get; set; } = "X-Api-Key";
    }

    /// <summary>
    /// A minimal API-key authentication handler used to protect high-impact
    /// orchestration and approval endpoints. This is intentionally simple for
    /// prototype purposes; production deployments should replace this with a
    /// proper identity provider (e.g., Entra ID / OAuth2).
    /// </summary>
    public sealed class ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (string.IsNullOrWhiteSpace(Options.ApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail(
                    "Orchestration API key is not configured on the server."));
            }

            if (!Request.Headers.TryGetValue(Options.HeaderName, out var providedKey) ||
                string.IsNullOrWhiteSpace(providedKey))
            {
                return Task.FromResult(AuthenticateResult.Fail(
                    $"Missing required '{Options.HeaderName}' header."));
            }

            if (!CryptographicEquals(providedKey.ToString(), Options.ApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));
            }

            var claims = new[] { new Claim(ClaimTypes.Name, "orchestration-client") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static bool CryptographicEquals(string left, string right)
        {
            var leftBytes = System.Text.Encoding.UTF8.GetBytes(left);
            var rightBytes = System.Text.Encoding.UTF8.GetBytes(right);

            if (leftBytes.Length != rightBytes.Length)
            {
                return false;
            }

            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
        }
    }
}
