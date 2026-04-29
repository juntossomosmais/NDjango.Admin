using System;
using System.Runtime.CompilerServices;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests
{
    internal static class TestModuleInitializer
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NDJANGO_SECRET_KEY")))
            {
                Environment.SetEnvironmentVariable(
                    "NDJANGO_SECRET_KEY",
                    "ndjango-admin-tests-default-secret-32chars-min-len");
            }
        }
    }
}
