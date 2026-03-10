using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;

using Xunit;

using EasyData.AspNetCore.AdminDashboard.Authentication;
using EasyData.AspNetCore.AdminDashboard.Authentication.Storage;
using EasyData.AspNetCore.AdminDashboard.Tests.Fixtures;

namespace EasyData.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    public class SamlGroupSyncTests : IClassFixture<SamlEnabledFixture>
    {
        private readonly string _connectionString;

        public SamlGroupSyncTests(SamlEnabledFixture fixture)
        {
            _connectionString = fixture.GetConnectionString();
        }

        private AuthDbContext CreateAuthDbContext()
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer(_connectionString)
                .Options;
            return new AuthDbContext(options);
        }

        [Fact]
        public async Task CreateOrUpdateSamlUserAsync_NewUser_CreatesWithCorrectDefaultsAsync()
        {
            // Arrange
            var username = $"saml_new_{Guid.NewGuid():N}";
            using var dbContext = CreateAuthDbContext();
            var queries = new AuthStorageQueries(dbContext);

            // Act
            var userId = await queries.CreateOrUpdateSamlUserAsync(username);

            // Assert
            Assert.True(userId > 0);
            var user = await queries.GetUserByUsernameAsync(username);
            Assert.NotNull(user);
            Assert.False(user.Value.IsSuperuser);
            Assert.True(user.Value.IsActive);
            Assert.False(string.IsNullOrEmpty(user.Value.PasswordHash));
        }

        [Fact]
        public async Task CreateOrUpdateSamlUserAsync_ExistingUser_UpdatesLastLoginAsync()
        {
            // Arrange
            var username = $"saml_existing_{Guid.NewGuid():N}";
            using var dbContext = CreateAuthDbContext();
            var queries = new AuthStorageQueries(dbContext);

            // Create user first
            var userId1 = await queries.CreateOrUpdateSamlUserAsync(username);

            // Get initial last_login
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            DateTime initialLastLogin;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT last_login FROM dbo.auth_user WHERE id = @id";
                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = userId1;
                cmd.Parameters.Add(param);
                initialLastLogin = (DateTime)await cmd.ExecuteScalarAsync();
            }
            finally
            {
                await conn.CloseAsync();
            }

            // Wait briefly to ensure timestamp difference
            await Task.Delay(100);

            // Act - call again for same user
            using var dbContext2 = CreateAuthDbContext();
            var queries2 = new AuthStorageQueries(dbContext2);
            var userId2 = await queries2.CreateOrUpdateSamlUserAsync(username);

            // Assert
            Assert.Equal(userId1, userId2);

            var conn2 = dbContext2.Database.GetDbConnection();
            await conn2.OpenAsync();
            try
            {
                using var cmd = conn2.CreateCommand();
                cmd.CommandText = "SELECT last_login FROM dbo.auth_user WHERE id = @id";
                var param = cmd.CreateParameter();
                param.ParameterName = "@id";
                param.Value = userId2;
                cmd.Parameters.Add(param);
                var updatedLastLogin = (DateTime)await cmd.ExecuteScalarAsync();
                Assert.True(updatedLastLogin >= initialLastLogin);
            }
            finally
            {
                await conn2.CloseAsync();
            }
        }

        [Fact]
        public async Task SyncUserGroupsAsync_NewUser_CreatesCorrectMembershipsAsync()
        {
            // Arrange
            var username = $"saml_groups_{Guid.NewGuid():N}";
            using var dbContext = CreateAuthDbContext();
            var queries = new AuthStorageQueries(dbContext);
            var userId = await queries.CreateOrUpdateSamlUserAsync(username);

            var groupIds = new List<string> { SamlEnabledFixture.TestSamlGroupId };

            // Act
            await queries.SyncUserGroupsAsync(userId, groupIds);

            // Assert
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"SELECT COUNT(*) FROM auth_user_groups ug
                    JOIN auth_group g ON g.id = ug.group_id
                    WHERE ug.user_id = @userId AND g.name = @groupName";
                var userIdParam = cmd.CreateParameter();
                userIdParam.ParameterName = "@userId";
                userIdParam.Value = userId;
                cmd.Parameters.Add(userIdParam);

                var groupParam = cmd.CreateParameter();
                groupParam.ParameterName = "@groupName";
                groupParam.Value = SamlEnabledFixture.TestSamlGroupId;
                cmd.Parameters.Add(groupParam);

                var count = (int)await cmd.ExecuteScalarAsync();
                Assert.Equal(1, count);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        [Fact]
        public async Task SyncUserGroupsAsync_ExistingUser_ReplacesAllMembershipsAsync()
        {
            // Arrange
            var username = $"saml_replace_{Guid.NewGuid():N}";
            using var dbContext = CreateAuthDbContext();
            var queries = new AuthStorageQueries(dbContext);
            var userId = await queries.CreateOrUpdateSamlUserAsync(username);

            // First sync with the test group
            await queries.SyncUserGroupsAsync(userId, new List<string> { SamlEnabledFixture.TestSamlGroupId });

            // Act - sync with empty list (remove all)
            await queries.SyncUserGroupsAsync(userId, new List<string>());

            // Assert
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM auth_user_groups WHERE user_id = @userId";
                var param = cmd.CreateParameter();
                param.ParameterName = "@userId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                var count = (int)await cmd.ExecuteScalarAsync();
                Assert.Equal(0, count);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }

        [Fact]
        public async Task SyncUserGroupsAsync_NoMatchingGroups_RemovesAllMembershipsAsync()
        {
            // Arrange
            var username = $"saml_nomatch_{Guid.NewGuid():N}";
            using var dbContext = CreateAuthDbContext();
            var queries = new AuthStorageQueries(dbContext);
            var userId = await queries.CreateOrUpdateSamlUserAsync(username);

            // First assign a real group
            await queries.SyncUserGroupsAsync(userId, new List<string> { SamlEnabledFixture.TestSamlGroupId });

            // Act - sync with non-matching UUIDs
            var nonMatchingIds = new List<string>
            {
                "00000000-0000-0000-0000-000000000001",
                "00000000-0000-0000-0000-000000000002"
            };
            await queries.SyncUserGroupsAsync(userId, nonMatchingIds);

            // Assert
            var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM auth_user_groups WHERE user_id = @userId";
                var param = cmd.CreateParameter();
                param.ParameterName = "@userId";
                param.Value = userId;
                cmd.Parameters.Add(param);

                var count = (int)await cmd.ExecuteScalarAsync();
                Assert.Equal(0, count);
            }
            finally
            {
                await conn.CloseAsync();
            }
        }
    }
}
