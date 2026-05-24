using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly AppDbContext _context;

        public TransactionService(AppDbContext context)
        {
            _context = context;
        }

        // ================= 1. HÀM LẤY LỊCH SỬ CÓ PHÂN TRANG =================
        public async Task<(IEnumerable<TransactionResponseDto> Data, int TotalItems)> GetTransactionsByWalletAsync(Guid walletId, int page, int pageSize)
        {
            var query = _context.Transactions.Where(t => t.WalletId == walletId);

            int totalItems = await query.CountAsync();

            var data = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionResponseDto
                {
                    Id = t.Id,
                    WalletId = t.WalletId,
                    Amount = t.Amount,
                    Type = t.Type.ToString(), // Chuyển Enum thành string
                    Status = t.Status.ToString(),
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    ProcessedAt = t.ProcessedAt
                }).ToListAsync();

            return (data, totalItems);
        }

        // ================= 2. HÀM TRỪ TIỀN ĐIỂM DANH (INTERNAL) =================
        public async Task<TransactionResponseDto> DeductAttendanceFeeAsync(Guid walletId, Guid attendanceSessionId, int studentCount)
        {
            // 1. Kiểm tra Ca điểm danh
            var session = await _context.AttendanceSessions.FindAsync(attendanceSessionId);
            if (session == null) throw new Exception("Không tìm thấy ca điểm danh.");

            // 🛡️ CHỐNG DOUBLE-BILLING:
            if (session.BillingTransId != null)
                throw new Exception("Ca điểm danh này ĐÃ ĐƯỢC THANH TOÁN, không thể trừ tiền lại.");

            if (session.Status != SessionStatus.Completed)
                throw new Exception("Ca điểm danh chưa hoàn tất (COMPLETED), chưa thể tính phí.");

            var wallet = await _context.Wallets.FindAsync(walletId);
            if (wallet == null) throw new Exception("Không tìm thấy ví của trường học.");

            // Lấy giá & Trừ tiền...
            var activePricing = await _context.PricingConfigs
                .Where(p => p.ServiceType == PricingServiceType.ATTENDANCE_UNIT && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (activePricing == null) throw new Exception("Chưa cấu hình đơn giá điểm danh.");

            decimal totalFee = studentCount * activePricing.UnitPrice;
            if (wallet.Balance < totalFee) throw new Exception("Số dư ví không đủ để thanh toán.");

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
                CreatedAt = DateTime.UtcNow
            };

            // 🛡️ GÁN ID GIAO DỊCH VÀO CA ĐIỂM DANH ĐỂ KHÓA LẠI
            session.BillingTransId = transaction.Id;

            _context.Wallets.Update(wallet);
            _context.Transactions.Add(transaction);
            _context.AttendanceSessions.Update(session); // Cập nhật lại session
            await _context.SaveChangesAsync();

            return new TransactionResponseDto { /* (Map properties như cũ) */ };
        }

        // ================= 3. HÀM TRỪ TIỀN GIÁM THỊ (INTERNAL) =================
        public async Task<TransactionResponseDto> DeductProctoringFeeAsync(Guid walletId, Guid examParticipationId, int hours)
        {
            // 1. Kiểm tra Ca thi của học sinh
            var participation = await _context.ExamParticipations.FindAsync(examParticipationId);
            if (participation == null) throw new Exception("Không tìm thấy dữ liệu giám thị.");

            // 🛡️ CHỐNG DOUBLE-BILLING:
            if (participation.BillingTransId != null)
                throw new Exception("Ca giám thị này ĐÃ ĐƯỢC THANH TOÁN, không thể trừ tiền lại.");

            if (participation.Status == ParticipationStatus.Joined)
                throw new Exception("Học sinh đang thi, chưa thể chốt phí.");

            var wallet = await _context.Wallets.FindAsync(walletId);
            if (wallet == null) throw new Exception("Không tìm thấy ví của trường học.");

            // Lấy giá & Trừ tiền...
            var activePricing = await _context.PricingConfigs
                .Where(p => p.ServiceType == PricingServiceType.PROCTORING_PER_HOUR && p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (activePricing == null) throw new Exception("Chưa cấu hình đơn giá giám thị.");

            decimal totalFee = hours * activePricing.UnitPrice;
            if (wallet.Balance < totalFee) throw new Exception("Số dư ví không đủ để thanh toán.");

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
                CreatedAt = DateTime.UtcNow
            };

            // 🛡️ GÁN ID GIAO DỊCH VÀO CA THI ĐỂ KHÓA LẠI
            participation.BillingTransId = transaction.Id;

            _context.Wallets.Update(wallet);
            _context.Transactions.Add(transaction);
            _context.ExamParticipations.Update(participation); // Cập nhật lại participation
            await _context.SaveChangesAsync();

            return new TransactionResponseDto { /* (Map properties như cũ) */ };
        }
    }
}
    