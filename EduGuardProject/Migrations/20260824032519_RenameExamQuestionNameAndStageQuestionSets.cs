using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EduGuardProject.Migrations
{
    /// <inheritdoc />
    public partial class RenameExamQuestionNameAndStageQuestionSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "exam_name",
                table: "exam_questions",
                newName: "exam_question_name");

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_slot_id",
                table: "exam_questions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "institution_id",
                table: "exam_questions",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE exam_questions AS question
                SET institution_id = course_class.institution_id
                FROM exam_slots AS slot
                INNER JOIN classes AS course_class ON course_class.id = slot.class_id
                WHERE question.exam_slot_id = slot.id;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "institution_id",
                table: "exam_questions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_exam_questions_institution_name_order",
                table: "exam_questions",
                columns: new[] { "institution_id", "exam_question_name", "display_order" });

            migrationBuilder.AddForeignKey(
                name: "exam_questions_institution_id_fkey",
                table: "exam_questions",
                column: "institution_id",
                principalTable: "institutions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "exam_questions_institution_id_fkey",
                table: "exam_questions");

            migrationBuilder.DropIndex(
                name: "idx_exam_questions_institution_name_order",
                table: "exam_questions");

            migrationBuilder.DropColumn(
                name: "institution_id",
                table: "exam_questions");

            migrationBuilder.RenameColumn(
                name: "exam_question_name",
                table: "exam_questions",
                newName: "exam_name");

            migrationBuilder.AlterColumn<Guid>(
                name: "exam_slot_id",
                table: "exam_questions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
