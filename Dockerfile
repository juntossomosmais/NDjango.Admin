ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/sdk:$DOTNET_VERSION

# Project targets net8.0 — install its runtime so dotnet test/run works
COPY --from=mcr.microsoft.com/dotnet/runtime:8.0 /usr/share/dotnet/shared /usr/share/dotnet/shared

WORKDIR /app

# Restores (downloads) all NuGet packages from all projects of the solution on a separate layer
COPY *.sln ./
COPY src/Directory.Build.props src/
COPY test/Directory.Build.props test/
COPY src/NDjango.Admin.AspNetCore/*.csproj src/NDjango.Admin.AspNetCore/packages.lock.json src/NDjango.Admin.AspNetCore/
COPY src/NDjango.Admin.AspNetCore.AdminDashboard/*.csproj src/NDjango.Admin.AspNetCore.AdminDashboard/packages.lock.json src/NDjango.Admin.AspNetCore.AdminDashboard/
COPY src/NDjango.Admin.Core/*.csproj src/NDjango.Admin.Core/packages.lock.json src/NDjango.Admin.Core/
COPY src/NDjango.Admin.EntityFrameworkCore.Relational/*.csproj src/NDjango.Admin.EntityFrameworkCore.Relational/packages.lock.json src/NDjango.Admin.EntityFrameworkCore.Relational/
COPY test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/*.csproj test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/packages.lock.json test/NDjango.Admin.AspNetCore.AdminDashboard.Tests/
COPY test/NDjango.Admin.AspNetCore.Tests/*.csproj test/NDjango.Admin.AspNetCore.Tests/packages.lock.json test/NDjango.Admin.AspNetCore.Tests/
COPY test/NDjango.Admin.Core.Tests/*.csproj test/NDjango.Admin.Core.Tests/packages.lock.json test/NDjango.Admin.Core.Tests/
COPY test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/*.csproj test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/packages.lock.json test/NDjango.Admin.EntityFrameworkCore.Relational.Tests/
RUN dotnet restore --locked-mode

# Tools used during development
COPY .config/dotnet-tools.json .config/
RUN dotnet tool restore

COPY . ./