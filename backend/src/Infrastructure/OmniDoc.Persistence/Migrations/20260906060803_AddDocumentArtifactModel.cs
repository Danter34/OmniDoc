using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OmniDoc.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentArtifactModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CanonicalArtifactId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DetectedFormat",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FailureCode",
                table: "Documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessingStage",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressPercentage",
                table: "Documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceArtifactId",
                table: "Documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DocumentArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Generation = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Producer = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentArtifacts", x => x.Id);
                    table.CheckConstraint("CK_DocumentArtifacts_Generation", "\"Generation\" >= 1");
                    table.ForeignKey(
                        name: "FK_DocumentArtifacts_Documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentArtifacts_DocumentId_Kind_Generation",
                table: "DocumentArtifacts",
                columns: new[] { "DocumentId", "Kind", "Generation" },
                unique: true);

            // Pre-migration upload validation allowed only PDFs. Both artifacts
            // intentionally share the existing bytes; SQL cannot hash file storage.
            migrationBuilder.Sql("""
                INSERT INTO "DocumentArtifacts"
                    ("Id", "DocumentId", "Kind", "Generation", "FileName", "ContentType",
                     "FileSizeBytes", "StoragePath", "Sha256", "Producer", "CreatedAtUtc")
                SELECT gen_random_uuid(), d."Id", k.kind, 1,
                       CASE WHEN k.kind = 1 THEN regexp_replace(d."FileName", '\.[^.]*$', '') || '.pdf'
                            ELSE d."FileName" END,
                       'application/pdf', d."FileSizeBytes", d."StoragePath", '',
                       CASE WHEN k.kind = 0 THEN 'Upload' ELSE 'PassThrough' END, d."CreatedAtUtc"
                FROM "Documents" d CROSS JOIN (VALUES (0), (1)) AS k(kind);

                UPDATE "Documents" d SET
                    "SourceArtifactId" = (SELECT a."Id" FROM "DocumentArtifacts" a WHERE a."DocumentId" = d."Id" AND a."Kind" = 0),
                    "CanonicalArtifactId" = (SELECT a."Id" FROM "DocumentArtifacts" a WHERE a."DocumentId" = d."Id" AND a."Kind" = 1),
                    "ProcessingStage" = CASE d."Status" WHEN 2 THEN 5 WHEN 3 THEN 6 WHEN 1 THEN 2 ELSE 0 END,
                    "ProgressPercentage" = CASE d."Status" WHEN 2 THEN 100 WHEN 3 THEN -1 WHEN 1 THEN 10 ELSE 0 END,
                    "FailureCode" = CASE WHEN d."Status" = 3 THEN 'LEGACY_PROCESSING_FAILED' ELSE NULL END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentArtifacts");

            migrationBuilder.DropColumn(
                name: "CanonicalArtifactId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "DetectedFormat",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "FailureCode",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProcessingStage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "ProgressPercentage",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "SourceArtifactId",
                table: "Documents");
        }
    }
}
