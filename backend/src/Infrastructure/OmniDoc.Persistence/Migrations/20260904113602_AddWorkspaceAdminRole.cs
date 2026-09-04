using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniDoc.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // WorkspaceRole.Member moved from 2 to 3 to reserve 2 for Admin.
            migrationBuilder.Sql(
                """
                UPDATE "WorkspaceMembers"
                SET "Role" = 3
                WHERE "Role" = 2;

                UPDATE "WorkspaceInvitations"
                SET "Role" = 3
                WHERE "Role" = 2;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The previous model only had Owner (1) and Member (2). Admins are
            // safely downgraded to Member when rolling back this migration.
            migrationBuilder.Sql(
                """
                UPDATE "WorkspaceMembers"
                SET "Role" = 2
                WHERE "Role" IN (2, 3);

                UPDATE "WorkspaceInvitations"
                SET "Role" = 2
                WHERE "Role" IN (2, 3);
                """);
        }
    }
}
