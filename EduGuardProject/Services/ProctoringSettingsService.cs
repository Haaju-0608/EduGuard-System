using System.Text.Json;
using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.Extensions.Caching.Distributed;

namespace EduGuardProject.Services
{
    public class ProctoringSettingsService : IProctoringSettingsService
    {
        private static readonly TimeSpan EffectiveConfigCacheTtl = TimeSpan.FromMinutes(10);

        // AI-detected violation types only — browser violations (TabSwitch/WindowBlur/ExitFullscreen)
        // are not subject to per-type detection thresholds.
        private static readonly ViolationType[] AiViolationTypes =
        {
            ViolationType.Impersonation,
            ViolationType.GazeDiversion,
            ViolationType.MultipleFaces,
            ViolationType.Absence,
            ViolationType.HeadTurn,
            ViolationType.FaceObstructed
        };

        private readonly IProctoringSettingsRepository _repo;
        private readonly IDistributedCache _cache;

        public ProctoringSettingsService(IProctoringSettingsRepository repo, IDistributedCache cache)
        {
            _repo = repo;
            _cache = cache;
        }

        public async Task<IEnumerable<ProctoringSettingsResponseDto>> GetAllAsync()
        {
            var configs = await _repo.GetAllAsync();
            return configs.Select(MapToDto);
        }

        public async Task<ProctoringSettingsResponseDto?> GetByIdAsync(Guid id)
        {
            var config = await _repo.GetByIdAsync(id);
            return config == null ? null : MapToDto(config);
        }

        public async Task<ProctoringSettingsResponseDto> GetEffectiveAsync(Guid? institutionId)
        {
            var cacheKey = EffectiveCacheKey(institutionId);
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
                return JsonSerializer.Deserialize<ProctoringSettingsResponseDto>(cached)!;

            var config = institutionId.HasValue
                ? await _repo.GetActiveByInstitutionAsync(institutionId.Value) ?? await _repo.GetActiveByInstitutionAsync(null)
                : await _repo.GetActiveByInstitutionAsync(null);

            var dto = config == null ? DefaultDto() : MapToDto(config);

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(dto),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = EffectiveConfigCacheTtl });

            return dto;
        }

        public async Task<ProctoringSettingsResponseDto> CreateAsync(
            CreateProctoringSettingsDto dto, Guid adminId, AppRole role, Guid? actorInstitutionId)
        {
            ValidateThresholds(dto.ViolationTypeThresholds);
            EnsureCanConfigure(dto.InstitutionId, role, actorInstitutionId);

            // Only one active config per institution (or per system-wide default) at a time —
            // deactivate whatever was active before, same convention as PricingConfig.
            var currentActive = await _repo.GetActiveByInstitutionAsync(dto.InstitutionId);
            if (currentActive != null)
            {
                currentActive.IsActive = false;
                currentActive.UpdatedAt = DateTime.UtcNow;
                currentActive.UpdatedBy = adminId;
                await _repo.UpdateAsync(currentActive);
            }

            var entity = new ProctoringSettings
            {
                Id = Guid.NewGuid(),
                InstitutionId = dto.InstitutionId,
                MaxAiViolationCount = dto.MaxAiViolationCount,
                CooldownSeconds = dto.CooldownSeconds,
                AllowConsecutiveSameType = dto.AllowConsecutiveSameType,
                AiNotifyThreshold = dto.AiNotifyThreshold,
                BrowserNotifyThreshold = dto.BrowserNotifyThreshold,
                IsActive = true,
                CreatedBy = adminId,
                UpdatedBy = adminId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ViolationTypeSettings = dto.ViolationTypeThresholds.Select(t => new ProctoringViolationTypeSetting
                {
                    Id = Guid.NewGuid(),
                    ViolationType = t.ViolationType,
                    DetectionThresholdSeconds = t.DetectionThresholdSeconds
                }).ToList()
            };

            await _repo.AddAsync(entity);
            await _cache.RemoveAsync(EffectiveCacheKey(dto.InstitutionId));
            return MapToDto(entity);
        }

        public async Task<bool> UpdateAsync(
            Guid id, UpdateProctoringSettingsDto dto, Guid adminId, AppRole role, Guid? actorInstitutionId)
        {
            ValidateThresholds(dto.ViolationTypeThresholds);
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            EnsureCanConfigure(entity.InstitutionId, role, actorInstitutionId);

            entity.MaxAiViolationCount = dto.MaxAiViolationCount;
            entity.CooldownSeconds = dto.CooldownSeconds;
            entity.AllowConsecutiveSameType = dto.AllowConsecutiveSameType;
            entity.AiNotifyThreshold = dto.AiNotifyThreshold;
            entity.BrowserNotifyThreshold = dto.BrowserNotifyThreshold;
            entity.IsActive = dto.IsActive;
            entity.UpdatedBy = adminId;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await _repo.ReplaceViolationTypeThresholdsAsync(entity.Id, dto.ViolationTypeThresholds.Select(t => new ProctoringViolationTypeSetting
            {
                Id = Guid.NewGuid(),
                ViolationType = t.ViolationType,
                DetectionThresholdSeconds = t.DetectionThresholdSeconds
            }));

            await _cache.RemoveAsync(EffectiveCacheKey(entity.InstitutionId));
            return true;
        }

        private static void EnsureCanConfigure(Guid? targetInstitutionId, AppRole role, Guid? actorInstitutionId)
        {
            if (role == AppRole.SuperAdmin) return;
            if (role != AppRole.SchoolAdmin)
                throw new UnauthorizedAccessException("Only SchoolAdmin or SuperAdmin can configure proctoring settings.");
            if (targetInstitutionId is null)
                throw new UnauthorizedAccessException("Only SuperAdmin can configure the system-wide default.");
            if (targetInstitutionId != actorInstitutionId)
                throw new UnauthorizedAccessException("School admins can only configure their own institution.");
        }

        private static void ValidateThresholds(List<ViolationTypeThresholdDto> thresholds)
        {
            if (thresholds.GroupBy(t => t.ViolationType).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Each violation type can only have one detection threshold.");
            if (thresholds.Any(t => !AiViolationTypes.Contains(t.ViolationType)))
                throw new InvalidOperationException("Detection thresholds only apply to AI-detected violation types (not browser violations).");
        }

        private static string EffectiveCacheKey(Guid? institutionId) =>
            $"proctoring-settings:effective:{(institutionId.HasValue ? institutionId.Value.ToString() : "global")}";

        private static ProctoringSettingsResponseDto DefaultDto() => new()
        {
            MaxAiViolationCount = 10,
            CooldownSeconds = 5,
            AllowConsecutiveSameType = false,
            AiNotifyThreshold = 3,
            BrowserNotifyThreshold = 3,
            IsActive = true
        };

        private static ProctoringSettingsResponseDto MapToDto(ProctoringSettings s) => new()
        {
            Id = s.Id,
            InstitutionId = s.InstitutionId,
            MaxAiViolationCount = s.MaxAiViolationCount,
            CooldownSeconds = s.CooldownSeconds,
            AllowConsecutiveSameType = s.AllowConsecutiveSameType,
            AiNotifyThreshold = s.AiNotifyThreshold,
            BrowserNotifyThreshold = s.BrowserNotifyThreshold,
            IsActive = s.IsActive,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            ViolationTypeThresholds = s.ViolationTypeSettings.Select(t => new ViolationTypeThresholdResponseDto
            {
                ViolationType = t.ViolationType,
                DetectionThresholdSeconds = t.DetectionThresholdSeconds
            }).ToList()
        };
    }
}
