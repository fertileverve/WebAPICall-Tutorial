using Microsoft.Extensions.Logging;

namespace APITest.Validator
{
    #region Configuration Classes
    public class ApiSettings
    {
        public string BaseUrl { get; set; } = "";
        public List<string> AllowedHosts { get; set; } = new();
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class DataverseSettings
    {
        public string Uri { get; set; } = "";
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";
        public List<string> AllowedHosts { get; set; } = new();
    }
    #endregion

    #region Host Validation Service
    public interface IHostValidator
    {
        bool IsHostAllowed(string url, List<string> allowedHosts);
        void ValidateHost(string url, List<string> allowedHosts, string serviceName);
    }

    public class HostValidator : IHostValidator
    {
        private readonly ILogger<HostValidator> _logger;

        public HostValidator(ILogger<HostValidator> logger)
        {
            _logger = logger;
        }

        public bool IsHostAllowed(string url, List<string> allowedHosts)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogWarning("URL is null or empty");
                return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                _logger.LogWarning("Invalid URL format: {Url}", url);
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                _logger.LogWarning("Non-HTTPS URL rejected: {Url}", url);
                return false;
            }

            string host = uri.Host.ToLowerInvariant();

            foreach (var allowedHost in allowedHosts)
            {
                string normalizedAllowed = allowedHost.ToLowerInvariant().Trim();

                if (host == normalizedAllowed)
                {
                    _logger.LogDebug("Host {Host} matched allowed host {AllowedHost}", host, normalizedAllowed);
                    return true;
                }

                if (normalizedAllowed.StartsWith("*."))
                {
                    string domain = normalizedAllowed.Substring(2);
                    if (host.EndsWith(domain) && (host == domain || host.EndsWith("." + domain)))
                    {
                        _logger.LogDebug("Host {Host} matched wildcard {AllowedHost}", host, normalizedAllowed);
                        return true;
                    }
                }
            }

            _logger.LogWarning("Host {Host} is not in the allowed list", host);
            return false;
        }

        public void ValidateHost(string url, List<string> allowedHosts, string serviceName)
        {
            if (!IsHostAllowed(url, allowedHosts))
            {
                throw new SecurityException($"Host validation failed for {serviceName}. The URL '{url}' is not in the allowed hosts list.");
            }
        }
    }

    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
    }
    #endregion
}