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

            // 1. Lấy cấu hình từ appsettings.json
            string tmnCode = _configuration["VnPay:TmnCode"];
            string hashSecret = _configuration["VnPay:HashSecret"];
            string baseUrl = _configuration["VnPay:BaseUrl"];

            // 2. Ép kiểu số tiền sang long và nhân 100 (Tuyệt đối không để dính dấu thập phân)
            long amountInCents = (long)(dto.Amount * 100);

            // 3. Đưa dữ liệu vào bộ thư viện VnPayLibrary mới cập nhật
            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", tmnCode);
            vnpay.AddRequestData("vnp_Amount", amountInCents.ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");

            // 🔥 BẪY IP: Không dùng httpContext để lấy IP lúc test Local/Ngrok (tránh bị dính "::1")
            // Ép cứng luôn IPv4 chuẩn này để VNPay Sandbox không bắt bẻ chữ ký
            string ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();

            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
            {
                ipAddress = "14.169.25.10"; // test tạm IPv4 public
            }

            vnpay.AddRequestData("vnp_IpAddr", ipAddress);

            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "NapTienVi_EduGuard");
            vnpay.AddRequestData("vnp_OrderType", "other");

            // 🔥 BẪY URL RETURN: Khớp hoàn toàn với Route [HttpGet("vnpay-return")] ở Controller của bạn
            // Hãy lấy chính xác link Ngrok đang active của bạn điền vào đây
            string returnUrl = _configuration["VnPay:ReturnUrl"];
            vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);

            // Mã giao dịch duy nhất (Ví dụ dùng Guid hoặc Id tự tăng từ DB)
            string txnRef = Guid.NewGuid().ToString().Substring(0, 8);
            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            // 4. Tiến hành sinh URL (Sử dụng hàm băm tự động sửa lỗi %20 và chữ HOA)
            string paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);
            Console.WriteLine("========= FINAL PAYMENT URL =========");
            Console.WriteLine(paymentUrl);
            Console.WriteLine("=====================================");

            return paymentUrl;
        }

        public async Task<bool> ProcessVnPayReturnAsync(IQueryCollection query)
        {
            var vnpay = new VnPayLibrary();

            // 1. Đọc toàn bộ Query string do VNPay gửi về đưa vào Library
            foreach (var key in query.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, query[key]);
                }
            }

            // 2. Lấy chữ ký do VNPay gửi qua và mã Secret Key trong máy của bạn
            string vnp_SecureHash = query["vnp_SecureHash"];
            string secretKey = _configuration["VnPay:HashSecret"];

            // 3. Xác thực chữ ký song phương
            bool isValidSignature = vnpay.ValidateSignature(vnp_SecureHash, secretKey);

            if (isValidSignature)
            {
                string responseCode = query["vnp_ResponseCode"];
                if (responseCode == "00")
                {
                    // Giao dịch thành công quả quyết! 
                    // Thực hiện logic cộng tiền vào DB của bạn ở đây...

                    return true;
                }
            }

            // Chữ ký sai hoặc giao dịch thất bại (Hủy thanh toán)
            return false;
        }
    }
}
