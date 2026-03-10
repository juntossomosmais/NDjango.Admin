using System;
using System.Collections.Generic;

using EasyData.AspNetCore.AdminDashboard.Authorization;

namespace EasyData.AspNetCore.AdminDashboard
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

        public string CookieName { get; set; } = ".EasyData.Admin.Auth";

        public TimeSpan CookieExpiration { get; set; } = TimeSpan.FromHours(24);

        public bool CreateDefaultAdminUser { get; set; } = false;

        public string DefaultAdminPassword { get; set; } = "admin";

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
