using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using EduGuardProject.Models;

#nullable disable

namespace EduGuardProject.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260915100507_AddExamParticipationNotificationReference")]
public partial class AddExamParticipationNotificationReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("ALTER TYPE reference_type_enum ADD VALUE IF NOT EXISTS 'EXAM_PARTICIPATION';");

    // PostgreSQL does not support removing enum values safely.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
