using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniDoc.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthenticationAndWorkspaceMembers : Migration
    {
        private static readonly Guid LegacyOwnerId =
            new("00000000-0000-0000-0000-000000000001");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Legacy memberships used arbitrary string ids (including "system-user") and
            // cannot be attached safely to authenticated Guid users. Rebuild the join table
            // and assign existing workspaces to a disabled migration-only owner account.
            migrationBuilder.DropTable(
                name: "WorkspaceMembers");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Id", "Email", "PasswordHash", "FullName", "CreatedAtUtc" },
                values: new object[]
                {
                    LegacyOwnerId,
                    "legacy-owner@omnidoc.invalid",
                    "ACCOUNT_DISABLED",
                    "Legacy Workspace Owner",
                    new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc)
                });

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Workspaces",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                $"""UPDATE "Workspaces" SET "OwnerId" = '{LegacyOwnerId}';""");

            migrationBuilder.AlterColumn<Guid>(
                name: "OwnerId",
                table: "Workspaces",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "WorkspaceMembers",
                columns: table => new
                {
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_WorkspaceMembers",
                        x => new { x.WorkspaceId, x.UserId });
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                $"""
                INSERT INTO "WorkspaceMembers" ("WorkspaceId", "UserId", "Role", "JoinedAtUtc")
                SELECT "Id", '{LegacyOwnerId}', 1, "CreatedAtUtc"
                FROM "Workspaces";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Workspaces_OwnerId",
                table: "Workspaces",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_UserId",
                table: "WorkspaceMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Workspaces_Users_OwnerId",
                table: "Workspaces",
                column: "OwnerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Workspaces_Users_OwnerId",
                table: "Workspaces");

            migrationBuilder.DropTable(
                name: "WorkspaceMembers");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Workspaces");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.CreateTable(
                name: "WorkspaceMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceMembers_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceMembers_WorkspaceId_UserId",
                table: "WorkspaceMembers",
                columns: new[] { "WorkspaceId", "UserId" },
                unique: true);
        }
    }
}
