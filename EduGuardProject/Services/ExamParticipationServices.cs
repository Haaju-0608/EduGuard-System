using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace EduGuardProject.Services;
		public class ExamParticipationServices : IExamParticipationService
    {
			private readonly IExamParticipationRepository _repo;
			private readonly AppDbContext _context;
			private readonly ICurrentUserService _currentUser;

			public ExamParticipationServices(IExamParticipationRepository repo, AppDbContext context, ICurrentUserService currentUser)
			{
				_repo = repo;
				_context = context;
				_currentUser = currentUser;
			}

			public async Task<(IEnumerable<ExamParticipationResponseDto> Items, int TotalCount)> GetAllExamparticipationsAsync(string? search, string? sort, int page, int pageSize)
			{
				return await _repo.GetAllAsync(search, sort, page, pageSize);
			}

			public async Task<ExamParticipation?> GetByIdAsync(Guid id)
			{
				return await _repo.GetByIdAsync(id);
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
				return entity;
			}

			public async Task<bool> UpdateAsync(Guid id, UpdateExamParticipationDto dto)
			{
				var entity = await _repo.GetByIdAsync(id);
				if (entity == null) return false;

				// update allowed fields
				entity.ActualStart = dto.ActualStart;
				entity.ActualEnd = dto.ActualEnd;
				entity.Status = dto.Status;
				entity.DisqualifiedReason = dto.DisqualifiedReason;
				entity.RecordingVideoPath = dto.RecordingVideoPath;
				entity.IdentitySnapshotPath = dto.IdentitySnapshotPath;

				await _repo.UpdateAsync(entity);
				return true;
			}
    public async Task<bool> UpdateAsyncOnlyExamPartipationStatus(Guid examSlotId, UpdateExamParticipationStatusDto dto)
    {
        var entity = await _repo.GetByIdAsync(examSlotId);
        if (entity == null) return false;

        entity.Status = dto.Status;

        await _repo.UpdateAsync(entity);
        return true;
    }
    public async Task<bool> DeleteAsync(Guid id)
			{
				var entity = await _repo.GetByIdAsync(id);
				if (entity == null) return false;
				await _repo.DeleteAsync(entity);
				return true;
			}
		}