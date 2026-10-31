using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Alerts.Api.Infrastructure.Identity.Migrations
{
    /// <inheritdoc />
    public partial class DomainPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserChannelSelections",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserChannelSelections", x => new { x.UserId, x.ChannelId });
                    table.ForeignKey(
                        name: "FK_UserChannelSelections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserChannelSelections_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AlertEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalEventId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertEvents_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserTopicSelections",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    TopicId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTopicSelections", x => new { x.UserId, x.TopicId });
                    table.ForeignKey(
                        name: "FK_UserTopicSelections_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTopicSelections_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ChannelId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.CheckConstraint("CK_Deliveries_AttemptCount", "\"AttemptCount\" BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_Deliveries_Status", "\"Status\" IN ('Pending', 'Succeeded', 'Failed')");
                    table.ForeignKey(
                        name: "FK_Deliveries_AlertEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "AlertEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliveries_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Deliveries_Channels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "Channels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Channels",
                columns: new[] { "Id", "Code", "DisplayName", "IsEnabled" },
                values: new object[,]
                {
                    { 1, "email", "Email", true },
                    { 2, "slack", "Slack", true }
                });

            migrationBuilder.InsertData(
                table: "Topics",
                columns: new[] { "Id", "Code", "DisplayName", "IsEnabled" },
                values: new object[,]
                {
                    { 1, "finance", "Finance", true },
                    { 2, "politics", "Politics", true },
                    { 3, "weather", "Weather", true }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_ExternalEventId",
                table: "AlertEvents",
                column: "ExternalEventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertEvents_TopicId",
                table: "AlertEvents",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_Code",
                table: "Channels",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_ChannelId",
                table: "Deliveries",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_EventId_UserId_ChannelId",
                table: "Deliveries",
                columns: new[] { "EventId", "UserId", "ChannelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_Status_AttemptCount",
                table: "Deliveries",
                columns: new[] { "Status", "AttemptCount" });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_UserId",
                table: "Deliveries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Code",
                table: "Topics",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserChannelSelections_ChannelId",
                table: "UserChannelSelections",
                column: "ChannelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserTopicSelections_TopicId",
                table: "UserTopicSelections",
                column: "TopicId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "UserChannelSelections");

            migrationBuilder.DropTable(
                name: "UserTopicSelections");

            migrationBuilder.DropTable(
                name: "AlertEvents");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}
