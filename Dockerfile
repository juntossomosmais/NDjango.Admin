ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:$DOTNET_VERSION

# The packages multi-target net8.0/net9.0/net10.0 and the suite runs once per framework, so every
# targeted runtime has to be here — the SDK image ships only its own. These come from the aspnet
# images, not the runtime ones: the dashboard tests spin up a TestHost and so ask for
# Microsoft.AspNetCore.App, which mcr.microsoft.com/dotnet/runtime does not carry.
COPY --from=mcr.microsoft.com/dotnet/aspnet:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared
COPY --from=mcr.microsoft.com/dotnet/aspnet:9.0 /usr/share/dotnet/shared /usr/share/dotnet/shared

WORKDIR /app

# Restores (downloads) all NuGet packages from all projects of the solution on a separate layer
COPY *.sln ./
COPY src/Directory.Build.props src/
COPY test/Directory.Build.props test/
COPY src/NDjango.Admin.AspNetCore/*.csproj src/NDjango.Admin.AspNetCore/packages.lock.json src/NDjango.Admin.AspNetCore/
COPY src/NDjango.Admin.AspNetCore.AdminDashboard/*.csproj src/NDjango.Admin.AspNetCore.AdminDashboard/packages.lock.json src/NDjango.Admin.AspNetCore.AdminDashboard/
COPY src/NDjango.Admin.Core/*.csproj src/NDjango.Admin.Core/packages.lock.json src/NDjango.Admin.Core/
COPY src/NDjango.Admin.AspNetCore.AdminDashboard.Core/*.csproj src/NDjango.Admin.AspNetCore.AdminDashboard.Core/packages.lock.json src/NDjango.Admin.AspNetCore.AdminDashboard.Core/
COPY src/NDjango.Admin.EntityFrameworkCore.Relational/*.csproj src/NDjango.Admin.EntityFrameworkCore.Relational/packages.lock.json src/NDjango.Admin.EntityFrameworkCore.Relational/
COPY src/NDjango.Admin.MongoDB/*.csproj src/NDjango.Admin.MongoDB/packages.lock.json src/NDjango.Admin.MongoDB/
COPY test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/*.csproj test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/packages.lock.json test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/
COPY test/NDjango.Admin.MongoDB.Tests/*.csproj test/NDjango.Admin.MongoDB.Tests/packages.lock.json test/NDjango.Admin.MongoDB.Tests/
COPY test/NDjango.Admin.AspNetCore.Tests/*.csproj test/NDjango.Admin.AspNetCore.Tests/packages.lock.json test/NDjango.Admin.AspNetCore.Tests/
COPY test/NDjango.Admin.Core.Tests/*.csproj test/NDjango.Admin.Core.Tests/packages.lock.json test/NDjango.Admin.Core.Tests/
COPY test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/*.csproj test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/packages.lock.json test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/
RUN dotnet restore --locked-mode

# Tools used during development
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore

COPY . ./