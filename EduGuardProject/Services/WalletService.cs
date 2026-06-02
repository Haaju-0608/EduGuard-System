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

        private static DateTime VietnamNow => DateTime.UtcNow.AddHours(7);

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

            var totalItems = await query.CountAsync();

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

        /// <summary>
        /// Nạp tiền trực tiếp (không qua VNPay) — dùng cho admin/test nội bộ.
        /// </summary>
        public async Task<TransactionResponseDto> ProcessTopUpAsync(TopUpRequestDto dto)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == dto.InstitutionId);

            if (wallet == null)
                throw new Exception("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new Exception("Số tiền nạp phải lớn hơn 0.");

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Type = TransactionType.TOP_UP,
                Status = TransactionStatus.SUCCESS,
                Description = dto.Description ?? "Nạp tiền vào ví hệ thống",
                ProcessedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            wallet.Balance += dto.Amount;
            wallet.UpdatedAt = DateTime.UtcNow;

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

        /// <summary>
        /// Tạo giao dịch PENDING, lưu DB, rồi trả URL redirect VNPay.
        /// vnp_TxnRef = Transaction.Id để map callback với ví/institution.
        /// </summary>
        public async Task<string> ProcessTopUpAsync(TopUpRequestDto dto, HttpContext httpContext)
        {
            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.InstitutionId == dto.InstitutionId);

            if (wallet == null)
                throw new Exception("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new Exception("Số tiền nạp phải lớn hơn 0.");

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                Amount = dto.Amount,
                Type = TransactionType.TOP_UP,
                Status = TransactionStatus.PENDING,
                Description = dto.Description ?? "Nạp tiền qua VNPay",
                VnpayRef = null,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            var vnpay = new VnPayLibrary();

            string tmnCode = _configuration["VnPay:TmnCode"] ?? throw new InvalidOperationException("Missing VnPay:TmnCode");
            string hashSecret = _configuration["VnPay:HashSecret"] ?? throw new InvalidOperationException("Missing VnPay:HashSecret");
            string baseUrl = _configuration["VnPay:BaseUrl"] ?? throw new InvalidOperationException("Missing VnPay:BaseUrl");
            string returnUrl = _configuration["VnPay:ReturnUrl"] ?? throw new InvalidOperationException("Missing VnPay:ReturnUrl");

            long amountInCents = (long)(dto.Amount * 100);
            string txnRef = transaction.Id.ToString();

            transaction.VnpayRef = txnRef;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", tmnCode);
            vnpay.AddRequestData("vnp_Amount", amountInCents.ToString());
            vnpay.AddRequestData("vnp_CreateDate", VietnamNow.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");
            vnpay.AddRequestData("vnp_IpAddr", ResolveClientIp(httpContext));
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", $"NapTienVi_EduGuard_{txnRef}");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);
            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            return vnpay.CreateRequestUrl(baseUrl, hashSecret);
        }

        public async Task<bool> ProcessVnPayReturnAsync(IQueryCollection query)
        {
            var vnpay = new VnPayLibrary();

            foreach (var key in query.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, query[key]!);
                }
            }

            string vnpSecureHash = query["vnp_SecureHash"].ToString();
            string secretKey = _configuration["VnPay:HashSecret"]
                ?? throw new InvalidOperationException("Missing VnPay:HashSecret");

            if (!vnpay.ValidateSignature(vnpSecureHash, secretKey))
                return false;

            if (!Guid.TryParse(query["vnp_TxnRef"].ToString(), out var transactionId))
                return false;

            var transaction = await _context.Transactions
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
                return false;

            // Idempotent: refresh callback nhiều lần không cộng tiền lại
            if (transaction.Status == TransactionStatus.SUCCESS)
                return true;

            if (transaction.Status != TransactionStatus.PENDING)
                return false;

            string responseCode = query["vnp_ResponseCode"].ToString();
            string transactionStatus = query["vnp_TransactionStatus"].ToString();

            if (!long.TryParse(query["vnp_Amount"].ToString(), out var vnpAmount))
            {
                await MarkTransactionFailedAsync(transaction);
                return false;
            }

            long expectedAmount = (long)(transaction.Amount * 100);
            if (vnpAmount != expectedAmount)
            {
                await MarkTransactionFailedAsync(transaction);
                return false;
            }

            if (responseCode != "00" || transactionStatus != "00")
            {
                await MarkTransactionFailedAsync(transaction);
                return false;
            }

            await using var dbTransaction = await _context.Database.BeginTransactionAsync();

            // Reload trong transaction để tránh race condition khi callback song song
            transaction = await _context.Transactions
                .Include(t => t.Wallet)
                .FirstOrDefaultAsync(t => t.Id == transactionId);

            if (transaction == null)
            {
                await dbTransaction.RollbackAsync();
                return false;
            }

            if (transaction.Status == TransactionStatus.SUCCESS)
            {
                await dbTransaction.CommitAsync();
                return true;
            }

            if (transaction.Status != TransactionStatus.PENDING)
            {
                await dbTransaction.RollbackAsync();
                return false;
            }

            var now = DateTime.UtcNow;
            transaction.Status = TransactionStatus.SUCCESS;
            transaction.ProcessedAt = now;
            transaction.UpdatedAt = now;
            transaction.Wallet.Balance += transaction.Amount;
            transaction.Wallet.UpdatedAt = now;

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            return true;
        }

        private static string ResolveClientIp(HttpContext httpContext)
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();

            if (string.IsNullOrEmpty(ipAddress) || ipAddress == "::1")
                return "127.0.0.1";

            return ipAddress;
        }

        private async Task MarkTransactionFailedAsync(Transaction transaction)
        {
            if (transaction.Status != TransactionStatus.PENDING)
                return;

            transaction.Status = TransactionStatus.FAILED;
            transaction.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}
