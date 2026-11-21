using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClerkAssistant.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditEntriesAppendOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE OR REPLACE FUNCTION audit_entries_append_only() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_entries is append-only';
                END;
                $$;

                CREATE TRIGGER audit_entries_no_update_or_delete
                    BEFORE UPDATE OR DELETE ON audit_entries
                    FOR EACH ROW EXECUTE FUNCTION audit_entries_append_only();

                CREATE TRIGGER audit_entries_no_truncate
                    BEFORE TRUNCATE ON audit_entries
                    FOR EACH STATEMENT EXECUTE FUNCTION audit_entries_append_only();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS audit_entries_no_truncate ON audit_entries;
                DROP TRIGGER IF EXISTS audit_entries_no_update_or_delete ON audit_entries;
                DROP FUNCTION IF EXISTS audit_entries_append_only();
                """);
        }
    }
}
