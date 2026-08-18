using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class AddReadingPassages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "passage_id",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "reading_passages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    exam_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    passage_text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("reading_passages_pkey", x => x.id);
                    table.ForeignKey(
                        name: "reading_passages_exam_slot_id_fkey",
                        column: x => x.exam_slot_id,
                        principalTable: "exam_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_exam_questions_passage",
                table: "exam_questions",
                column: "passage_id");

            migrationBuilder.CreateIndex(
                name: "idx_reading_passages_exam_slot",
                table: "reading_passages",
                column: "exam_slot_id");

            migrationBuilder.AddForeignKey(
                name: "exam_questions_passage_id_fkey",
                table: "exam_questions",
                column: "passage_id",
                principalTable: "reading_passages",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "exam_questions_passage_id_fkey",
                table: "exam_questions");

            migrationBuilder.DropTable(
                name: "reading_passages");

            migrationBuilder.DropIndex(
                name: "idx_exam_questions_passage",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "passage_id",
                table: "exam_questions");
        }
    }
}
