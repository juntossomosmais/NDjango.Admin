using Microsoft.EntityFrameworkCore;

using NDjango.Admin.AspNetCore.AdminDashboard.Authentication;
using NDjango.Admin.AspNetCore.AdminDashboard.Authentication.Entities;
using NDjango.Admin.EntityFrameworkCore;

using Xunit;

namespace NDjango.Admin.AspNetCore.AdminDashboard.Tests.AuthenticationTests
{
    // Regression: the flag that decides whether someone can do anything in the dashboard has to be
    // editable in the dashboard itself.
    //
    // It was not. AuthDbContext declared IsSuperuser with HasDefaultValue, EF Core marks a property
    // with a store default as ValueGenerated.OnAdd, and DbContextMetaDataLoader turns anything that
    // is not ValueGenerated.Never into a read-only field — so the flag rendered greyed out on the
    // change form and was missing from the add form, with no error to explain it. The only way to
    // promote a user was an UPDATE against the database by hand.
    //
    // No database is touched here: LoadFromDbContext reads the EF model, so a connection string that
    // points nowhere is enough.
    public class AuthUserMetadataTests
    {
        private static MetaEntityAttr FindAttribute(string name)
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseSqlServer("Server=none;Database=none;")
                .Options;

            using var context = new AuthDbContext(options);
            var meta = new MetaData();
            meta.LoadFromDbContext(context);

            var entity = meta.FindEntity(e => e.ClrType == typeof(AuthUser));
            Assert.NotNull(entity);
            var attr = entity.FindAttribute(a => a.Id.EndsWith("." + name));
            Assert.NotNull(attr);
            return attr;
        }

        [Fact]
        public void IsSuperuser_IsEditableAndPresentOnBothForms()
        {
            // Act
            var attr = FindAttribute(nameof(AuthUser.IsSuperuser));

            // Assert
            Assert.True(attr.IsEditable, "is_superuser must be editable on the change form.");
            Assert.True(attr.ShowOnCreate, "is_superuser must be present on the add form.");
            Assert.True(attr.ShowOnEdit, "is_superuser must be present on the change form.");
        }

        [Theory]
        [InlineData(nameof(AuthUser.IsActive))]
        [InlineData(nameof(AuthUser.DateJoined))]
        public void GeneratedColumns_StayReadOnly(string propertyName)
        {
            // The other half of the contract, so that "make the flag editable" is not later widened
            // into "drop every store default" without the consequence being seen.
            //
            // DateJoined is genuinely generated (GETUTCDATE()), so read-only is simply correct.
            // IsActive is the interesting one: its CLR default (true) is the opposite of what an
            // absent form field binds to (false), so making it editable silently turns a create that
            // omits it into an INACTIVE user — which is exactly what
            // CreatePost_WithValueGeneratedBoolDefault_PreservesDatabaseDefaultAsync catches.
            // Making it editable is worth doing; it needs the add form to seed the checkbox from the
            // entity default first.
            // Act
            var attr = FindAttribute(propertyName);

            // Assert
            Assert.False(attr.IsEditable);
        }
    }
}
