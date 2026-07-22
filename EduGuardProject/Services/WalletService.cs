using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;

namespace EduGuardProject.Services
{
    public class WalletService : IWalletService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public WalletService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<WalletResponseDto?> GetWalletByInstitutionIdAsync(Guid institutionId)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == institutionId);

            if (wallet == null) return null;

            return new WalletResponseDto
            {
                Id = wallet.Id,
                InstitutionId = wallet.InstitutionId,
                Balance = wallet.Balance,
                Currency = wallet.Currency,
                LowBalanceThreshold = wallet.LowBalanceThreshold
            };
        }

        public async Task<(IEnumerable<TransactionResponseDto> Data, int TotalItems)> GetTransactionHistoryAsync(Guid walletId, int page, int pageSize)
        {
            var query = _context.Transactions.Where(t => t.WalletId == walletId);

            // Đếm tổng số lượng để làm Metadata
            var totalItems = await query.CountAsync();

            // Lấy dữ liệu theo trang
            var data = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionResponseDto
                {
                    Id = t.Id,
                    Amount = t.Amount,
                    Type = t.Type.ToString(),
                    Status = t.Status.ToString(),
                    Description = t.Description,
                    CreatedAt = t.CreatedAt,
                    ProcessedAt = t.ProcessedAt
                })
                .ToListAsync();

            return (data, totalItems);
        }

        public async Task<TransactionResponseDto> ProcessTopUpAsync(TopUpRequestDto dto)
        {
            // 1. Tìm ví của Institution
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == dto.InstitutionId);

            if (wallet == null)
                throw new Exception("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new Exception("Số tiền nạp phải lớn hơn 0.");

            // 2. Tạo Transaction lịch sử
            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Type = TransactionType.TOP_UP,
                Status = TransactionStatus.SUCCESS, // Nạp trực tiếp nên thành công luôn
                Description = dto.Description ?? "Nạp tiền vào ví hệ thống",
                ProcessedAt = DateTime.UtcNow
            };

            // 3. Cộng tiền vào ví
            wallet.Balance += dto.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            // 4. Lưu tất cả vào DB (EF Core sẽ tự động quản lý transaction nếu 1 trong 2 lỗi)
            _context.Transactions.Add(transaction);
            _context.Wallets.Update(wallet);

            await _context.SaveChangesAsync();

            return new TransactionResponseDto
            {
                Id = transaction.Id,
                Amount = transaction.Amount,
                Type = transaction.Type.ToString(),
                Status = transaction.Status.ToString(),
                Description = transaction.Description,
                CreatedAt = transaction.CreatedAt,
                ProcessedAt = transaction.ProcessedAt
            };
        }

        //Phần VN Pay
        public async Task<string> ProcessTopUpAsync(TopUpRequestDto dto, HttpContext httpContext)
        {
            var vnpay = new VnPayLibrary();

            // 0. Tìm ví TRƯỚC — fail sớm nếu không có ví, tránh tạo URL vô nghĩa
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == dto.InstitutionId);

            if (wallet == null)
                throw new Exception("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new Exception("Số tiền nạp phải lớn hơn 0.");

            string tmnCode = _configuration["VnPay:TmnCode"];
            string hashSecret = _configuration["VnPay:HashSecret"];
            string baseUrl = _configuration["VnPay:BaseUrl"];

            long amountInCents = (long)(dto.Amount * 100);

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", tmnCode);
            vnpay.AddRequestData("vnp_Amount", amountInCents.ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");

            string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                ipAddress = "14.169.25.10";
            vnpay.AddRequestData("vnp_IpAddr", ipAddress);

            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "NapTienVi_EduGuard");
            vnpay.AddRequestData("vnp_OrderType", "other");

            string returnUrl = _configuration["VnPay:ReturnUrl"];
            vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);

            // ⚠️ Guid.Substring(0,8) dễ đụng hàng khi traffic lớn — nhưng OK cho scope capstone.
            // Quan trọng hơn: PHẢI unique trong DB (VnpayRef đã có unique index sẵn ở model, tốt).
            string txnRef = Guid.NewGuid().ToString().Substring(0, 8);
            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            // 🔥 CHỖ THIẾU: lưu transaction PENDING xuống DB, để lúc return có cái mà tra
            var transaction = new Transaction
            {
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Type = TransactionType.TOP_UP,
                Status = TransactionStatus.PENDING,   // đổi tên enum member nếu khác
                Description = dto.Description ?? "Nạp tiền qua VNPay",
                VnpayRef = txnRef,
                CreatedAt = DateTime.UtcNow
            };
            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            string paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);
            return paymentUrl;
        }

        public async Task<bool> ProcessVnPayReturnAsync(IQueryCollection query)
        {
            var vnpay = new VnPayLibrary();

            foreach (var key in query.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                    vnpay.AddResponseData(key, query[key]);
            }

            string vnp_SecureHash = query["vnp_SecureHash"];
            string secretKey = _configuration["VnPay:HashSecret"];

            bool isValidSignature = vnpay.ValidateSignature(vnp_SecureHash, secretKey);
            if (!isValidSignature)
                return false;

            string responseCode = query["vnp_ResponseCode"];
            string txnRef = query["vnp_TxnRef"];
            string vnpAmountRaw = query["vnp_Amount"];

            // 1. Tìm lại transaction đã lưu lúc tạo URL
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.VnpayRef == txnRef);

            if (transaction == null)
                return false; // không khớp txnRef nào trong DB -> nghi ngờ giả mạo, không cộng

            // 2. Idempotency guard — VNPay có thể gọi return/IPN nhiều lần cho cùng 1 giao dịch
            if (transaction.Status == TransactionStatus.SUCCESS)
                return true; // đã xử lý rồi, khỏi cộng lại lần 2

            // 3. Verify số tiền VNPay trả về khớp với số tiền đã lưu (chống sửa amount ở client)
            if (long.TryParse(vnpAmountRaw, out long vnpAmountCents))
            {
                var expectedCents = (long)(transaction.Amount * 100);
                if (vnpAmountCents != expectedCents)
                {
                    transaction.Status = TransactionStatus.FAILED;
                    transaction.ProcessedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return false;
                }
            }

            if (responseCode != "00")
            {
                transaction.Status = TransactionStatus.FAILED;
                transaction.ProcessedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                return false;
            }

            // 4. Thành công thật sự -> cộng tiền vào ví
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.Id == transaction.WalletId);
            if (wallet == null)
                return false;

            wallet.Balance += transaction.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

            transaction.Status = TransactionStatus.SUCCESS;
            transaction.ProcessedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return true;
        }
    }
}
