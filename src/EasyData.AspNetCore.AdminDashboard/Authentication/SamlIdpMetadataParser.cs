using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace EasyData.AspNetCore.AdminDashboard.Authentication
{
    internal class SamlIdpMetadataResult
    {
        public string Certificate { get; set; }
        public string SsoUrl { get; set; }
        public string EntityId { get; set; }
    }

    internal static class SamlIdpMetadataParser
    {
        private static readonly XNamespace Md = "urn:oasis:names:tc:SAML:2.0:metadata";
        private static readonly XNamespace Ds = "http://www.w3.org/2000/09/xmldsig#";
        private const string HttpRedirectBinding = "urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect";

        public static SamlIdpMetadataResult Parse(string xml)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to parse SAML IdP metadata XML.", ex);
            }

            var entityDescriptor = doc.Root;
            if (entityDescriptor == null || entityDescriptor.Name.LocalName != "EntityDescriptor")
            {
                throw new InvalidOperationException("SAML metadata XML does not contain an EntityDescriptor root element.");
            }

            var entityId = entityDescriptor.Attribute("entityID")?.Value;
            if (string.IsNullOrEmpty(entityId))
            {
                throw new InvalidOperationException("SAML metadata XML is missing the entityID attribute.");
            }

            var idpDescriptor = entityDescriptor.Element(Md + "IDPSSODescriptor");
            if (idpDescriptor == null)
            {
                throw new InvalidOperationException("SAML metadata XML does not contain an IDPSSODescriptor element.");
            }

            // Extract signing certificate
            var certificate = idpDescriptor
                .Elements(Md + "KeyDescriptor")
                .Where(kd => kd.Attribute("use")?.Value == "signing")
                .SelectMany(kd => kd.Descendants(Ds + "X509Certificate"))
                .Select(cert => cert.Value.Trim())
                .FirstOrDefault();

            if (string.IsNullOrEmpty(certificate))
            {
                throw new InvalidOperationException("SAML metadata XML does not contain a signing certificate.");
            }

            // Extract SSO URL (HTTP-Redirect binding)
            var ssoUrl = idpDescriptor
                .Elements(Md + "SingleSignOnService")
                .Where(sso => sso.Attribute("Binding")?.Value == HttpRedirectBinding)
                .Select(sso => sso.Attribute("Location")?.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(ssoUrl))
            {
                throw new InvalidOperationException("SAML metadata XML does not contain a SingleSignOnService with HTTP-Redirect binding.");
            }

            return new SamlIdpMetadataResult
            {
                Certificate = certificate,
                SsoUrl = ssoUrl,
                EntityId = entityId
            };
        }

        public static async Task<SamlIdpMetadataResult> FetchAndParseAsync(string metadataUrl)
        {
            using var httpClient = new HttpClient();
            var xml = await httpClient.GetStringAsync(metadataUrl);
            return Parse(xml);
        }
    }
}
