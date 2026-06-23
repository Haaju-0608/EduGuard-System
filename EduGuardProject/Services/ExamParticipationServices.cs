using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace EduGuardProject.Services;

public class ExamParticipationServices : IExamParticipationService
{
    private readonly IExamParticipationRepository _repo;
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRealtimeEventDispatcher _realtime;
    private readonly IStorageService _storage;

    public ExamParticipationServices(
        IExamParticipationRepository repo,
        AppDbContext context,
        ICurrentUserService currentUser,
        IRealtimeEventDispatcher realtime,
        IStorageService storage)
    {
        _repo = repo;
        _context = context;
        _currentUser = currentUser;
        _realtime = realtime;
        _storage = storage;
    }

    public async Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllExamparticipationsAsync(string? search, string? sort, int page, int pageSize)
    {
        return await _repo.GetAllAsync(search, sort, page, pageSize);
    }

    public async Task<ExamParticipationResponseDto?> GetByIdAsync(Guid id)
    {
        var entity = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (entity == null)
            return null;

        var user = await _currentUser.GetRequiredUserAsync();
        var hasAccess =
            user.Role == AppRole.SuperAdmin ||
            (user.Role == AppRole.SchoolAdmin &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId) ||
            (user.Role == AppRole.Lecturer &&
             user.InstitutionId == entity.ExamSlot.Class.InstitutionId &&
             user.Id == entity.ExamSlot.Class.LecturerId) ||
            (user.Role == AppRole.Student && user.Id == entity.StudentId);

        if (!hasAccess)
            throw new UnauthorizedAccessException("Access denied.");

        return MapToResponseDto(entity);
    }

    public async Task<ExamParticipation> CreateAsync(CreateExamParticipationDto dto)
    {
        //await _currentUser.EnsureRoleAsync(AppRole.SchoolAdmin, AppRole.SuperAdmin);
        //var user = await _currentUser.GetRequiredUserAsync();

        //if (user.Role != AppRole.SchoolAdmin && user.Role != AppRole.SuperAdmin)
        //	throw new UnauthorizedAccessException("Only school admins and super admins can create exam participations.");
        var entity = new ExamParticipation
        {
            Id = Guid.NewGuid(),
            ExamSlotId = dto.ExamSlotId,
            StudentId = dto.StudentId,
            BillingTransId = dto.BillingTransId,
            ActualStart = dto.ActualStart,
            ActualEnd = dto.ActualEnd,
            Status = dto.Status,
            DisqualifiedReason = dto.DisqualifiedReason,
            RecordingVideoPath = dto.RecordingVideoPath,
            IdentitySnapshotPath = dto.IdentitySnapshotPath,
        };

        await _repo.AddAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "created");
        return entity;
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateExamParticipationDto dto)
    {
        var entity = await GetManageableParticipationAsync(id);
        if (entity == null) return false;

        // update allowed fields
        entity.ActualStart = dto.ActualStart;
        entity.ActualEnd = dto.ActualEnd;
        entity.Status = dto.Status;
        entity.DisqualifiedReason = dto.DisqualifiedReason;
        entity.RecordingVideoPath = dto.RecordingVideoPath;
        entity.IdentitySnapshotPath = dto.IdentitySnapshotPath;

        await _repo.UpdateAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "updated");
        return true;
    }
    public async Task<bool> UpdateAsyncOnlyExamPartipationStatus(Guid examSlotId, UpdateExamParticipationStatusDto dto)
    {
        var entity = await GetManageableParticipationAsync(examSlotId);
        if (entity == null) return false;

        entity.Status = dto.Status;

        await _repo.UpdateAsync(entity);
        await PublishParticipationChangedAsync(entity.Id, "status-updated");
        return true;
    }
    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await GetManageableParticipationAsync(id);
        if (entity == null) return false;
        await PublishParticipationChangedAsync(entity.Id, "deleted");
        if (!string.IsNullOrWhiteSpace(entity.IdentitySnapshotPath))
            await _storage.DeleteAsync(StorageService.ExamIdentityBucket, entity.IdentitySnapshotPath);
        if (!string.IsNullOrWhiteSpace(entity.RecordingVideoPath))
            await _storage.DeleteAsync(StorageService.ExamRecordingsBucket, entity.RecordingVideoPath);
        await _repo.DeleteAsync(entity);
        return true;
    }

    private async Task<ExamParticipation?> GetManageableParticipationAsync(Guid participationId)
    {
        var entity = await _context.ExamParticipations
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId);
        if (entity == null)
            return null;

        var user = await _currentUser.GetRequiredUserAsync();
        if (user.Role == AppRole.SuperAdmin)
            return entity;

        if (user.Role == AppRole.SchoolAdmin &&
            user.InstitutionId == entity.ExamSlot.Class.InstitutionId)
        {
            return entity;
        }

        throw new UnauthorizedAccessException("Access denied.");
    }

    private async Task PublishParticipationChangedAsync(Guid participationId, string action)
    {
        var participation = await _context.ExamParticipations
            .AsNoTracking()
            .Include(p => p.Student)
            .Include(p => p.ExamSlot)
            .ThenInclude(e => e.Class)
            .FirstOrDefaultAsync(p => p.Id == participationId);

        if (participation == null)
            return;

        await _realtime.PublishDataChangedAsync(
            "exam-participations",
            action,
            institutionId: participation.ExamSlot.Class.InstitutionId,
            lecturerId: participation.ExamSlot.Class.LecturerId,
            userId: participation.StudentId,
            data: new
            {
                participationId = participation.Id,
                participation.ExamSlotId,
                participation.StudentId,
                participation.Student.FullName,
                participation.Status,
                participation.ActualStart,
                participation.ActualEnd,
                participation.DisqualifiedReason
            });
    }

    private static ExamParticipationResponseDto MapToResponseDto(ExamParticipation entity) => new()
    {
        Id = entity.Id,
        ExamSlotId = entity.ExamSlotId,
        StudentId = entity.StudentId,
        BillingTransId = entity.BillingTransId,
        ActualStart = entity.ActualStart,
        ActualEnd = entity.ActualEnd,
        Status = entity.Status,
        DisqualifiedReason = entity.DisqualifiedReason,
        RecordingVideoPath = entity.RecordingVideoPath,
        IdentitySnapshotPath = entity.IdentitySnapshotPath
    };
}
