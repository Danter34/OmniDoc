using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace OmniDoc.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ResizeDocumentChunkEmbeddingsTo768 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Embeddings produced by the previous 1536-dimensional provider cannot be
            // resized without changing their meaning. Keep the source chunks but clear
            // their vectors so they can be re-embedded with the configured provider.
            migrationBuilder.Sql(
                """UPDATE "DocumentChunks" SET "Embedding" = NULL WHERE "Embedding" IS NOT NULL;""");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "DocumentChunks",
                type: "vector(768)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "DocumentChunks" SET "Embedding" = NULL WHERE "Embedding" IS NOT NULL;""");

            migrationBuilder.AlterColumn<Vector>(
                name: "Embedding",
                table: "DocumentChunks",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(768)",
                oldNullable: true);
        }
    }
}
