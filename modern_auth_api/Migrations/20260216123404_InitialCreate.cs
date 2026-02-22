using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace modern_auth_api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying", nullable: false),
                    email = table.Column<string>(type: "character varying", nullable: false),
                    provider = table.Column<string>(type: "character varying", nullable: false),
                    create_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    update_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provider_key = table.Column<string>(type: "character varying", nullable: false),
                    picture = table.Column<string>(type: "character varying", nullable: true),
                    role = table.Column<string>(type: "character varying", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    auth_id = table.Column<string>(type: "text", nullable: false, comment: "supabase user id")
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_pkey", x => x.id);
                },
                comment: "Registration users on Moneyball Website");

            migrationBuilder.CreateTable(
                name: "users_token",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    token = table.Column<string>(type: "text", nullable: false),
                    create_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expire_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    users_id = table.Column<long>(type: "bigint", nullable: false),
                    type = table.Column<string>(type: "character varying", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("users_token_pkey", x => x.id);
                    table.ForeignKey(
                        name: "users_token_users_id_fkey",
                        column: x => x.users_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            // 迴圈掃描 public schema，全部設 eanbleRLS
            migrationBuilder.Sql(@"
                DO $$ 
                DECLARE 
                    r RECORD;
                BEGIN 
                    -- 迴圈：遍歷所有 public schema 的表
                    FOR r IN (SELECT tablename FROM pg_tables WHERE schemaname = 'public') 
                    LOOP 
                        -- 排除 __EFMigrationsHistory 表
                        IF r.tablename != '__EFMigrationsHistory' THEN
                            -- 使用 format() 函式，%I 會自動處理引號，不需手動串接
                            EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY;', r.tablename);
                        END IF;
                    END LOOP; 
                END $$;
            ");

            migrationBuilder.CreateIndex(
                name: "users_auth_id_key",
                table: "users",
                column: "auth_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_token_users_id",
                table: "users_token",
                column: "users_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "users_token");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
