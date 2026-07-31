using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Repositories.IRepositories;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class InstitutionService : IInstitutionService
    {
        private readonly IInstitutionRepository _repo;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly ICurrentUserService _currentUser;
        private readonly IPricingConfigService _pricingConfigService;   
        private readonly AppDbContext _context;

        public InstitutionService(IInstitutionRepository repo, IRealtimeEventDispatcher realtime, ICurrentUserService currentUser, IPricingConfigService pricingConfigService, AppDbContext context)
        {
            _repo = repo;
            _realtime = realtime;
            _currentUser = currentUser;
            _context = context;
            _pricingConfigService = pricingConfigService;
        }

        public async Task<(IEnumerable<InstitutionResponseDto> Items, int TotalCount)> GetInstitutionsAsync(string? search, string? sort, int page, int pageSize)
        {
            var (entities, totalCount) = await _repo.GetAllAsync(search, sort, page, pageSize);

            // Map Entity sang Response DTO
            var dtos = entities.Select(e => MapToResponseDto(e));
            return (dtos, totalCount);
        }

        public async Task<InstitutionResponseDto?> GetInstitutionByIdAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            return entity == null ? null : MapToResponseDto(entity);
        }

        public async Task<InstitutionResponseDto> CreateInstitutionAsync(CreateInstitutionDto dto)
        {
            var now = DateTime.UtcNow;
            var entity = new Institution
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                SubDomain = dto.SubDomain,
                ContactEmail = dto.ContactEmail,
                BillingModel = dto.BillingModel,
                Status = dto.Status,
                // THÊM MỚI: tự set hạn ban đầu theo đúng chu kỳ đã chọn
                SubscriptionExpiresAt = dto.BillingModel == BillingModel.Monthly
                    ? now.AddMonths(1)
                    : now.AddYears(1),
                CreatedAt = now,
                UpdatedAt = now
            };
            await _repo.AddAsync(entity);
            await PublishInstitutionChangedAsync(entity, "created");
            return MapToResponseDto(entity);
        }

        // THÊM MỚI: gia hạn thêm 1 chu kỳ kể từ hạn cũ (nếu còn hạn) hoặc từ hiện tại (nếu đã hết hạn)
        public async Task<bool> RenewSubscriptionAsync(Guid institutionId)
        {
            var entity = await _repo.GetByIdAsync(institutionId);
            if (entity == null) return false;

            var user = await _currentUser.GetRequiredUserAsync();
            if (user.Role != AppRole.SuperAdmin &&
                (!user.InstitutionId.HasValue || user.InstitutionId.Value != institutionId))
            {
                throw new UnauthorizedAccessException("You do not have permission to do this");
            }

            // 1. Xác định loại giá theo billing model của trường
            var serviceType = entity.BillingModel == BillingModel.Monthly
                ? PricingServiceType.SUBSCRIPTION_MONTHLY
                : PricingServiceType.SUBSCRIPTION_YEARLY;

            var priceConfig = await _pricingConfigService.GetCurrentActiveConfigAsync(serviceType);
            if (priceConfig == null)
                throw new InvalidOperationException(
                    "Chưa cấu hình giá gia hạn subscription. Vui lòng liên hệ quản trị hệ thống.");

            var renewalFee = priceConfig.UnitPrice;

            // 2. Tìm ví, kiểm tra số dư
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == institutionId);
            if (wallet == null)
                throw new InvalidOperationException("Không tìm thấy ví của trường học này.");

            if (wallet.Balance < renewalFee)
                throw new InvalidOperationException(
                    $"Số dư không đủ để gia hạn. Cần {renewalFee:N0}đ, hiện có {wallet.Balance:N0}đ. Vui lòng nạp thêm tiền.");

            // 3. Trừ tiền + ghi transaction
            wallet.Balance -= renewalFee;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                PricingConfigId = priceConfig.Id,
                Amount = renewalFee,
                Type = TransactionType.SUBSCRIPTION_FEE,
                Status = TransactionStatus.SUCCESS,
                Description = $"Gia hạn subscription ({entity.BillingModel})",
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Transactions.Add(transaction);

            // 4. Dời ngày hết hạn (logic cũ giữ nguyên)
            var now = DateTime.UtcNow;
            var baseDate = (entity.SubscriptionExpiresAt.HasValue && entity.SubscriptionExpiresAt.Value > now)
                ? entity.SubscriptionExpiresAt.Value
                : now;

            entity.SubscriptionExpiresAt = entity.BillingModel == BillingModel.Monthly
                ? baseDate.AddMonths(1)
                : baseDate.AddYears(1);

            if (entity.Status == InstitutionStatus.Suspended)
                entity.Status = InstitutionStatus.Active;

            entity.UpdatedAt = now;
            await _repo.UpdateAsync(entity);

            // 5. Lưu tất cả (wallet + transaction) trong 1 lần — EF tự đảm bảo transaction DB
            await _context.SaveChangesAsync();

            await PublishInstitutionChangedAsync(entity, "renewed");
            return true;
        }


public async Task<bool> UpdateInstitutionAsync(Guid id, UpdateInstitutionDto dto)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            entity.Name = dto.Name;
            entity.SubDomain = dto.SubDomain;
            entity.ContactEmail = dto.ContactEmail;
            entity.BillingModel = dto.BillingModel;
            entity.Status = dto.Status;
            entity.UpdatedAt = DateTime.UtcNow;

            await _repo.UpdateAsync(entity);
            await PublishInstitutionChangedAsync(entity, "updated");
            return true;
        }

        public async Task<bool> DeleteInstitutionAsync(Guid id)
        {
            var entity = await _repo.GetByIdAsync(id);
            if (entity == null) return false;

            await _repo.DeleteAsync(entity);
            await PublishInstitutionChangedAsync(entity, "deleted");
            return true;
        }

        private Task PublishInstitutionChangedAsync(Institution entity, string action) =>
            _realtime.PublishDataChangedAsync(
                "institutions",
                action,
                institutionId: entity.Id,
                data: new
                {
                    institutionId = entity.Id,
                    entity.Name,
                    entity.SubDomain,
                    entity.ContactEmail,
                    entity.BillingModel,
                    entity.Status
                });

        // Hàm phụ để Map nhanh dữ liệu đỡ phải viết đi viết lại nhiều lần
        private static InstitutionResponseDto MapToResponseDto(Institution e) => new()
        {
            Id = e.Id,
            Name = e.Name,
            SubDomain = e.SubDomain,
            ContactEmail = e.ContactEmail,
            SubscriptionExpiresAt = e.SubscriptionExpiresAt,
            BillingModel = e.BillingModel,
            Status = e.Status,
            CreatedAt = e.CreatedAt
        };
    }
}
