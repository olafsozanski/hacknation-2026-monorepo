using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace ClerkAssistant.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequestEmbeddingsAndExtensionKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_requests_embedding",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "embedding",
                table: "requests");

            migrationBuilder.AddColumn<string>(
                name: "extension_kind",
                table: "requests",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "request_embeddings",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vector = table.Column<Vector>(type: "vector(1024)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_request_embeddings", x => x.request_id);
                    table.ForeignKey(
                        name: "fk_request_embeddings_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_request_embeddings_vector",
                table: "request_embeddings",
                column: "vector")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_embeddings");

            migrationBuilder.DropColumn(
                name: "extension_kind",
                table: "requests");

            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                table: "requests",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_requests_embedding",
                table: "requests",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }
    }
}
