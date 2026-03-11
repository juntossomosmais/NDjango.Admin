using System;
using System.Collections.Generic;

using NDjango.Admin.AspNetCore.AdminDashboard.Authorization;

namespace NDjango.Admin.AspNetCore.AdminDashboard
{
    public class AdminDashboardOptions
    {
        public IEnumerable<IAdminDashboardAuthorizationFilter> Authorization { get; set; }
            = new[] { new LocalRequestsOnlyAuthorizationFilter() };

        public string DashboardTitle { get; set; } = "Admin";

        public string AppPath { get; set; } = "/";

        public int DefaultRecordsPerPage { get; set; } = 25;

        public bool IsReadOnly { get; set; } = false;

        public Dictionary<string, string[]> EntityGroups { get; set; }

        public bool RequireAuthentication { get; set; } = false;

        public string CookieName { get; set; } = ".NDjango.Admin.Auth";

        public TimeSpan CookieExpiration { get; set; } = TimeSpan.FromHours(24);

        public bool CreateDefaultAdminUser { get; set; } = false;

        public string DefaultAdminPassword { get; set; } = "admin";

        /// <summary>
        /// Maximum time in milliseconds to wait for the COUNT query during pagination.
        /// If exceeded, returns a fallback count. Default: 200ms.
        /// Set to -1 to disable the timeout (wait indefinitely).
        /// </summary>
        public int PaginationCountTimeoutMs { get; set; } = 200;

        /// <summary>
        /// When true, skips auth table creation, permission seeding, and default admin user
        /// creation at startup. Useful for tests or externally managed schemas.
        /// </summary>
        public bool SkipStorageInitialization { get; set; } = false;

        // SAML SSO configuration
        public bool EnableSaml { get; set; } = false;

        /// <summary>
        /// IdP metadata URL. When set, certificate and SSO URL are auto-extracted at startup.
        /// </summary>
        public string SamlMetadataUrl { get; set; }

        /// <summary>
        /// IdP SSO endpoint URL. Extracted from metadata if SamlMetadataUrl is set.
        /// </summary>
        public string SamlIdpSsoUrl { get; set; }

        /// <summary>
        /// IdP X.509 signing certificate (base64). Extracted from metadata if SamlMetadataUrl is set.
        /// </summary>
        public string SamlCertificate { get; set; }

        /// <summary>
        /// SP entity ID / SAML audience (e.g., "http://localhost:8000").
        /// </summary>
        public string SamlIssuer { get; set; }

        /// <summary>
        /// Full ACS callback URL (e.g., "http://localhost:8000/api/security/saml/callback").
        /// </summary>
        public string SamlAcsUrl { get; set; }

        /// <summary>
        /// SAML attribute name containing group UUIDs. Defaults to "groups".
        /// </summary>
        public string SamlGroupsAttribute { get; set; } = "groups";
    }
}
