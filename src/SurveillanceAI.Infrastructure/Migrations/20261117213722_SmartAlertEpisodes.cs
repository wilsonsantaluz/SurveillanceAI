using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SurveillanceAI.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SmartAlertEpisodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CameraId",
                table: "Events");

            migrationBuilder.AddColumn<string>(
                name: "TemplateId",
                table: "Rules",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AlterColumn<float>(
                name: "Confidence",
                table: "Events",
                type: "REAL",
                nullable: true,
                oldClrType: typeof(float),
                oldType: "REAL");

            migrationBuilder.AddColumn<DateTime>(
                name: "AcknowledgedUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedUtc",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetectorId",
                table: "Events",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EpisodeOpen",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "HumanConfirmed",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyEmail",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SaveEvidence",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SoundAlert",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TemplateId",
                table: "Events",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "VisualAlert",
                table: "Events",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Preserve the detector identity of all pre-existing YOLO events.
            migrationBuilder.Sql("UPDATE Events SET DetectorId = 'yolo', VisualAlert = 1, NotifyEmail = 1 WHERE TemplateId IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Events_CameraId_TemplateId",
                table: "Events",
                columns: new[] { "CameraId", "TemplateId" },
                unique: true,
                filter: "\"EpisodeOpen\" = 1 AND \"TemplateId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Events_CameraId_TemplateId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Rules");

            migrationBuilder.DropColumn(
                name: "AcknowledgedUtc",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "ClosedUtc",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "DetectorId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "EpisodeOpen",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "HumanConfirmed",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "NotifyEmail",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SaveEvidence",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "SoundAlert",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "VisualAlert",
                table: "Events");

            migrationBuilder.AlterColumn<float>(
                name: "Confidence",
                table: "Events",
                type: "REAL",
                nullable: false,
                defaultValue: 0f,
                oldClrType: typeof(float),
                oldType: "REAL",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_CameraId",
                table: "Events",
                column: "CameraId");
        }
    }
}
