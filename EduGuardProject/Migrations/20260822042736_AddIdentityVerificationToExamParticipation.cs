using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityVerificationToExamParticipation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "identity_verified_at",
                table: "exam_participations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "identity_verified_by",
                table: "exam_participations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_participations_identity_verified_by",
                table: "exam_participations",
                column: "identity_verified_by");

            migrationBuilder.AddForeignKey(
                name: "exam_participations_identity_verified_by_fkey",
                table: "exam_participations",
                column: "identity_verified_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "exam_participations_identity_verified_by_fkey",
                table: "exam_participations");

            migrationBuilder.DropIndex(
                name: "IX_exam_participations_identity_verified_by",
                table: "exam_participations");

            migrationBuilder.DropColumn(
                name: "identity_verified_at",
                table: "exam_participations");

            migrationBuilder.DropColumn(
                name: "identity_verified_by",
                table: "exam_participations");
        }
    }
}
