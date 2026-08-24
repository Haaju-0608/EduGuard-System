using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class ReuseExamQuestionSetsAcrossSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "exam_question_name",
                table: "exam_slots",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE exam_slots AS slot
                SET exam_question_name = COALESCE(
                    (
                        SELECT question.exam_question_name
                        FROM exam_questions AS question
                        WHERE question.exam_slot_id = slot.id
                        ORDER BY question.display_order, question.created_at
                        LIMIT 1
                    ),
                    NULLIF(BTRIM(slot.exam_name), ''),
                    'Legacy Exam'
                );
                """);

            migrationBuilder.AlterColumn<string>(
                name: "exam_question_name",
                table: "exam_slots",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "exam_questions_exam_slot_id_fkey",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "idx_exam_questions_exam_slot",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "exam_slot_id",
                table: "exam_questions");

            migrationBuilder.CreateIndex(
                name: "idx_exam_slots_exam_question_name",
                table: "exam_slots",
                column: "exam_question_name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_exam_slots_exam_question_name",
                table: "exam_slots");

            migrationBuilder.AddColumn<Guid>(
                name: "exam_slot_id",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE exam_questions AS question
                SET exam_slot_id = (
                    SELECT slot.id
                    FROM exam_slots AS slot
                    INNER JOIN classes AS course_class ON course_class.id = slot.class_id
                    WHERE course_class.institution_id = question.institution_id
                      AND LOWER(slot.exam_question_name) = LOWER(question.exam_question_name)
                    ORDER BY slot.created_at, slot.id
                    LIMIT 1
                );
                """);

            migrationBuilder.DropColumn(
                name: "exam_question_name",
                table: "exam_slots");

            migrationBuilder.CreateIndex(
                name: "idx_exam_questions_exam_slot",
                table: "exam_questions",
                column: "exam_slot_id");

            migrationBuilder.AddForeignKey(
                name: "exam_questions_exam_slot_id_fkey",
                table: "exam_questions",
                column: "exam_slot_id",
                principalTable: "exam_slots",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
