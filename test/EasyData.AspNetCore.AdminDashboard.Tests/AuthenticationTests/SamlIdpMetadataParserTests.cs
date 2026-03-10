using System;

using Xunit;

using EasyData.AspNetCore.AdminDashboard.Authentication;

namespace EasyData.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class SamlIdpMetadataParserTests
    {
        private const string ValidMetadataXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<md:EntityDescriptor xmlns:md=""urn:oasis:names:tc:SAML:2.0:metadata"" entityID=""https://portal.sso.us-east-1.amazonaws.com/saml/assertion/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk"">
    <md:IDPSSODescriptor WantAuthnRequestsSigned=""false"" protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
        <md:KeyDescriptor use=""signing"">
            <ds:KeyInfo xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">
                <ds:X509Data>
                    <ds:X509Certificate>MIIDBzCCAe+gAwIBAgIFAMsG/EswDQYJKoZIhvcNAQELBQAwRTEWMBQGA1UEAwwNYW1hem9uYXdzLmNvbTENMAsGA1UECwwESURBUzEPMA0GA1UECgwGQW1hem9uMQswCQYDVQQGEwJVUzAeFw0yNjAzMTAxNDE0NDlaFw0zMTAzMTAxNDE0NDlaMEUxFjAUBgNVBAMMDWFtYXpvbmF3cy5jb20xDTALBgNVBAsMBElEQVMxDzANBgNVBAoMBkFtYXpvbjELMAkGA1UEBhMCVVMwggEiMA0GCSqGSIb3DQEBAQUAA4IBDwAwggEKAoIBAQDwfuM1KwTgkqO7RzYt8mbkFX8kgFj9MXUc36ZWJPpNGBfbz6DOSZ29zfRbUk0WyKrdGQrXo5w11drZHtE4VQQl/7iBx57Jvwl6xyINarfxy0i4whPp07BvdQjX2I3Hfq6CR3Z4lYkoc8cym8AKqvEiRm6y7JikUZeNMFBdO+/UP1F/tHEWjfcleHAAm8z5CBxgRbf/+SySJLvOd7H4wMBlC58wzmAwHwQp2K26tqYyz/OZWpdiGs8v5t+JR9BHzCptEKJnndJkvO2vN3HCIXmxU1q4miBUW8qGpaPe43dYwGzk08zSCZTs30IjS39PgVSraaRTUSqOGCcEaLVpPGjXAgMBAAEwDQYJKoZIhvcNAQELBQADggEBANO8pAn/Fc2Mg8FsNq7ZVw3Fw/PcmMHp3Fr0Wlti18f3YmcEUVLNJc0hdA4UShhQrWUVFrTRD+ITk0ar6VP7Iw8fEXJ3dzrtQ6uPR+Yuz90Mey2mbciGZYACwCOj0ECS3//cGSEgnlmXg2cc4+tlTN3YRIGj74oCSkjHLGUye+vWqEjmUuNxC94q7Wx1I56pnIK5JG9B5icXUj+erd450vuJBf94kGObhwiPdMrPwkS+akgFqfbyYy+hiWXPYutH8YK53oVOrJt8lIWk9O4WXmPSPEhai6KvM8r5IT7+XxLaYzNKp4tT91ioGz4PbVuM+VjS68kIPkqvNRJW/LeM1xc=</ds:X509Certificate>
                </ds:X509Data>
            </ds:KeyInfo>
        </md:KeyDescriptor>
        <md:SingleLogoutService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://portal.sso.us-east-1.amazonaws.com/saml/logout/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk""/>
        <md:SingleLogoutService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"" Location=""https://portal.sso.us-east-1.amazonaws.com/saml/logout/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk""/>
        <md:NameIDFormat/>
        <md:SingleSignOnService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"" Location=""https://portal.sso.us-east-1.amazonaws.com/saml/assertion/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk""/>
        <md:SingleSignOnService Binding=""urn:oasis:names:tc:SAML:2.0:bindings:HTTP-Redirect"" Location=""https://portal.sso.us-east-1.amazonaws.com/saml/assertion/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk""/>
    </md:IDPSSODescriptor>
</md:EntityDescriptor>";

        [Fact]
        public void Parse_ValidMetadataXml_ExtractsCertificateAsync()
        {
            // Arrange & Act
            var result = SamlIdpMetadataParser.Parse(ValidMetadataXml);

            // Assert
            Assert.StartsWith("MIIDBz", result.Certificate);
            Assert.Contains("AgMBAAEw", result.Certificate);
        }

        [Fact]
        public void Parse_ValidMetadataXml_ExtractsSsoUrlAsync()
        {
            // Arrange & Act
            var result = SamlIdpMetadataParser.Parse(ValidMetadataXml);

            // Assert
            Assert.Equal(
                "https://portal.sso.us-east-1.amazonaws.com/saml/assertion/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk",
                result.SsoUrl);
        }

        [Fact]
        public void Parse_ValidMetadataXml_ExtractsEntityIdAsync()
        {
            // Arrange & Act
            var result = SamlIdpMetadataParser.Parse(ValidMetadataXml);

            // Assert
            Assert.Equal(
                "https://portal.sso.us-east-1.amazonaws.com/saml/assertion/NjMzMDU2NjMyMDE2X2lucy03MjIzYTRlNzE5YjI0ZDZk",
                result.EntityId);
        }

        [Fact]
        public void Parse_InvalidXml_ThrowsAsync()
        {
            // Arrange
            var invalidXml = "<not valid xml><<<";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => SamlIdpMetadataParser.Parse(invalidXml));
            Assert.Contains("Failed to parse SAML IdP metadata XML", ex.Message);
        }

        [Fact]
        public void Parse_MissingEntityId_ThrowsAsync()
        {
            // Arrange
            var xml = @"<?xml version=""1.0""?>
<md:EntityDescriptor xmlns:md=""urn:oasis:names:tc:SAML:2.0:metadata"">
    <md:IDPSSODescriptor protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
    </md:IDPSSODescriptor>
</md:EntityDescriptor>";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => SamlIdpMetadataParser.Parse(xml));
            Assert.Contains("entityID", ex.Message);
        }

        [Fact]
        public void Parse_MissingSsoService_ThrowsAsync()
        {
            // Arrange
            var xml = @"<?xml version=""1.0""?>
<md:EntityDescriptor xmlns:md=""urn:oasis:names:tc:SAML:2.0:metadata"" entityID=""https://example.com"">
    <md:IDPSSODescriptor protocolSupportEnumeration=""urn:oasis:names:tc:SAML:2.0:protocol"">
        <md:KeyDescriptor use=""signing"">
            <ds:KeyInfo xmlns:ds=""http://www.w3.org/2000/09/xmldsig#"">
                <ds:X509Data>
                    <ds:X509Certificate>AAAA</ds:X509Certificate>
                </ds:X509Data>
            </ds:KeyInfo>
        </md:KeyDescriptor>
    </md:IDPSSODescriptor>
</md:EntityDescriptor>";

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => SamlIdpMetadataParser.Parse(xml));
            Assert.Contains("SingleSignOnService", ex.Message);
        }
    }
}
