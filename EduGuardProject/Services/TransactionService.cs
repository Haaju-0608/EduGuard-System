using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _context;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly INotificationDispatcher _notifications;

        public TransactionService(
            AppDbContext context,
            IRealtimeEventDispatcher realtime,
            INotificationDispatcher notifications)
        {
            _context = context;
            _realtime = realtime;
            _notifications = notifications;
        }

        public async Task<(IEnumerable<TransactionResponseDto> Data, int TotalItems)> GetTransactionsByWalletAsync(Guid walletId, int page, int pageSize)
        {
            var query = _context.Transactions.Where(t => t.WalletId == walletId);

            int totalItems = await query.CountAsync();

            var transactions = await query
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = transactions.Select(MapTransaction).ToList();

            return (data, totalItems);
        }

        public async Task<TransactionResponseDto> DeductAttendanceFeeAsync(Guid walletId, Guid attendanceSessionId, int studentCount)
        {
            if (studentCount <= 0)
                throw new InvalidOperationException("Số lượng học sinh phải lớn hơn 0.");

            var session = await _context.AttendanceSessions.FindAsync(attendanceSessionId);
            if (session == null) throw new InvalidOperationException("Không tìm thấy ca điểm danh.");

            // Prevent duplicate billing for the same attendance session.
            if (session.BillingTransId != null)
                throw new InvalidOperationException("Ca điểm danh này ĐÃ ĐƯỢC THANH TOÁN, không thể trừ tiền lại.");

            if (session.Status != SessionStatus.Completed)
                throw new InvalidOperationException("Ca điểm danh chưa hoàn tất (COMPLETED), chưa thể tính phí.");

            var wallet = await _context.Wallets.FindAsync(walletId);
            if (wallet == null) throw new InvalidOperationException("Không tìm thấy ví của trường học.");

            var activePricing = await _context.PricingConfigs
                .Where(p => p.ServiceType == PricingServiceType.ATTENDANCE_UNIT && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (activePricing == null) throw new InvalidOperationException("Chưa cấu hình đơn giá điểm danh.");

            decimal totalFee = studentCount * activePricing.UnitPrice;
            if (wallet.Balance < totalFee) throw new InvalidOperationException("Số dư ví không đủ để thanh toán.");

            wallet.Balance -= totalFee;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                PricingConfigId = activePricing.Id,
                Amount = totalFee,
                Type = TransactionType.ATTENDANCE_FEE,
                Status = TransactionStatus.SUCCESS,
                Description = $"Trừ phí điểm danh cho {studentCount} học sinh (Ca: {attendanceSessionId})",
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Persist the transaction id to lock the billed session.
            session.BillingTransId = transaction.Id;

            _context.Wallets.Update(wallet);
            _context.Transactions.Add(transaction);
            _context.AttendanceSessions.Update(session);
            await _context.SaveChangesAsync();
            await DispatchWalletUpdatedAsync(wallet, transaction);

            return MapTransaction(transaction);
        }

        public async Task<TransactionResponseDto> DeductProctoringFeeAsync(Guid walletId, Guid examParticipationId, int hours)
        {
            if (hours <= 0)
                throw new InvalidOperationException("Số giờ giám thị phải lớn hơn 0.");

            var participation = await _context.ExamParticipations.FindAsync(examParticipationId);
            if (participation == null) throw new InvalidOperationException("Không tìm thấy dữ liệu giám thị.");

            // Prevent duplicate billing for the same exam participation.
            if (participation.BillingTransId != null)
                throw new InvalidOperationException("Ca giám thị này ĐÃ ĐƯỢC THANH TOÁN, không thể trừ tiền lại.");

            if (participation.Status == ParticipationStatus.Joined)
                throw new InvalidOperationException("Học sinh đang thi, chưa thể chốt phí.");

            var wallet = await _context.Wallets.FindAsync(walletId);
            if (wallet == null) throw new InvalidOperationException("Không tìm thấy ví của trường học.");

            var activePricing = await _context.PricingConfigs
                .Where(p => p.ServiceType == PricingServiceType.PROCTORING_PER_HOUR && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (activePricing == null) throw new InvalidOperationException("Chưa cấu hình đơn giá giám thị.");

            decimal totalFee = hours * activePricing.UnitPrice;
            if (wallet.Balance < totalFee) throw new InvalidOperationException("Số dư ví không đủ để thanh toán.");

            wallet.Balance -= totalFee;
            wallet.UpdatedAt = DateTime.UtcNow;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = walletId,
                PricingConfigId = activePricing.Id,
                Amount = totalFee,
                Type = TransactionType.PROCTORING_FEE,
                Status = TransactionStatus.SUCCESS,
                Description = $"Trừ phí giám thị cho {hours} giờ (ParticipationId: {examParticipationId})",
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Persist the transaction id to lock the billed participation.
            participation.BillingTransId = transaction.Id;

            _context.Wallets.Update(wallet);
            _context.Transactions.Add(transaction);
            _context.ExamParticipations.Update(participation);
            await _context.SaveChangesAsync();
            await DispatchWalletUpdatedAsync(wallet, transaction);

            return MapTransaction(transaction);
        }

        private async Task DispatchWalletUpdatedAsync(Wallet wallet, Transaction transaction)
        {
            var payload = new
            {
                wallet.InstitutionId,
                walletId = wallet.Id,
                wallet.Balance,
                amount = -transaction.Amount,
                transactionId = transaction.Id,
                transaction.Type,
                transaction.Status,
                transaction.ProcessedAt
            };

            await _realtime.PushInstitutionAdminsAsync(wallet.InstitutionId, HubEvents.WalletBalanceUpdated, payload);
            await _realtime.PublishDataChangedAsync(
                "wallet",
                "updated",
                institutionId: wallet.InstitutionId,
                data: payload);
            await _realtime.PublishDataChangedAsync(
                "transactions",
                "created",
                institutionId: wallet.InstitutionId,
                data: payload);

            if (wallet.Balance <= wallet.LowBalanceThreshold)
            {
                await _notifications.SendToInstitutionAdminsAsync(
                    wallet.InstitutionId,
                    "Số dư ví thấp",
                    $"Số dư ví hiện còn {wallet.Balance:N0} {wallet.Currency}, dưới hoặc bằng ngưỡng {wallet.LowBalanceThreshold:N0}.",
                    NotificationType.LowBalanceAlert,
                    ReferenceTypeEnum.Transaction,
                    transaction.Id);
            }
        }

        private static TransactionResponseDto MapTransaction(Transaction transaction) => new()
        {
            Id = transaction.Id,
            WalletId = transaction.WalletId,
            Amount = transaction.Amount,
            Type = transaction.Type.ToCanonicalName(),
            Status = transaction.Status.ToCanonicalName(),
            Description = transaction.Description,
            CreatedAt = transaction.CreatedAt,
            ProcessedAt = transaction.ProcessedAt
        };
    }
}
