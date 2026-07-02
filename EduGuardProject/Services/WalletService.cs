using EduGuardProject.DTOs.Request;
using EduGuardProject.DTOs.Response;
using EduGuardProject.Hubs;
using EduGuardProject.Models;
using EduGuardProject.Services.IServices;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace EduGuardProject.Services
{
    public class WalletService : IWalletService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IRealtimeEventDispatcher _realtime;
        private readonly INotificationDispatcher _notifications;

        public WalletService(
            AppDbContext context,
            IConfiguration configuration,
            IRealtimeEventDispatcher realtime,
            INotificationDispatcher notifications)
        {
            _context = context;
            _configuration = configuration;
            _realtime = realtime;
            _notifications = notifications;
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

            var transactions = await query
                .AsNoTracking()
            var data = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = transactions.Select(MapTransaction).ToList();
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
                throw new InvalidOperationException("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new InvalidOperationException("Số tiền nạp phải lớn hơn 0.");

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
            await DispatchWalletUpdatedAsync(wallet, transaction, dto.Amount);

            return MapTransaction(transaction);
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
                throw new InvalidOperationException("Không tìm thấy ví của trường học này.");

            if (dto.Amount <= 0)
                throw new InvalidOperationException("Số tiền nạp phải lớn hơn 0.");

            var vnpay = new VnPayLibrary();

            string tmnCode = _configuration["VnPay:TmnCode"]
                ?? throw new InvalidOperationException("VnPay:TmnCode is not configured.");
            string hashSecret = _configuration["VnPay:HashSecret"]
                ?? throw new InvalidOperationException("VnPay:HashSecret is not configured.");
            string baseUrl = _configuration["VnPay:BaseUrl"]
                ?? throw new InvalidOperationException("VnPay:BaseUrl is not configured.");
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

            string ipAddress = ResolveVnPayIpAddress(httpContext);
            vnpay.AddRequestData("vnp_IpAddr", ipAddress);

            vnpay.AddRequestData("vnp_IpAddr", ResolveClientIp(httpContext));
            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", $"NapTienVi_EduGuard_{txnRef}");
            vnpay.AddRequestData("vnp_OrderType", "other");

            string returnUrl = _configuration["VnPay:ReturnUrl"]
                ?? throw new InvalidOperationException("VnPay:ReturnUrl is not configured.");
            vnpay.AddRequestData("vnp_ReturnUrl", returnUrl);

            string txnRef = Guid.NewGuid().ToString("N")[..16];
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                WalletId = wallet.Id,
                VnpayRef = txnRef,
                Amount = dto.Amount,
                Type = TransactionType.TOP_UP,
                Status = TransactionStatus.PENDING,
                Description = dto.Description ?? "Nạp tiền vào ví qua VNPay",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();
            await _realtime.PublishDataChangedAsync(
                "transactions",
                "created",
                institutionId: wallet.InstitutionId,
                data: new
                {
                    transactionId = transaction.Id,
                    walletId = wallet.Id,
                    transaction.Amount,
                    transaction.Type,
                    transaction.Status,
                    transaction.CreatedAt
                });

            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            string paymentUrl = vnpay.CreateRequestUrl(baseUrl, hashSecret);

            return paymentUrl;
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
                    vnpay.AddResponseData(key, query[key].ToString());
                    vnpay.AddResponseData(key, query[key]!);
                }
            }

            string vnpSecureHash = query["vnp_SecureHash"].ToString();
            if (string.IsNullOrWhiteSpace(vnpSecureHash))
                return false;

            string secretKey = _configuration["VnPay:HashSecret"]
                ?? throw new InvalidOperationException("VnPay:HashSecret is not configured.");

            bool isValidSignature = vnpay.ValidateSignature(vnpSecureHash, secretKey);
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
                string responseCode = query["vnp_ResponseCode"].ToString();
                if (responseCode == "00")
                {
                    string txnRef = query["vnp_TxnRef"].ToString();
                    var transaction = await _context.Transactions
                        .Include(t => t.Wallet)
                        .FirstOrDefaultAsync(t => t.VnpayRef == txnRef);

                    if (transaction == null)
                        return false;

                    if (!transaction.Status.IsSuccess())
                    {
                        transaction.Status = TransactionStatus.SUCCESS;
                        transaction.ProcessedAt = DateTime.UtcNow;
                        transaction.UpdatedAt = DateTime.UtcNow;
                        transaction.Wallet.Balance += transaction.Amount;
                        transaction.Wallet.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        await DispatchWalletUpdatedAsync(transaction.Wallet, transaction, transaction.Amount);
                    }

                    return true;
                }

                string failedTxnRef = query["vnp_TxnRef"].ToString();
                var failedTransaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.VnpayRef == failedTxnRef);
                if (failedTransaction != null && failedTransaction.Status.IsPending())
                {
                    failedTransaction.Status = TransactionStatus.FAILED;
                    failedTransaction.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    var failedWallet = await _context.Wallets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(w => w.Id == failedTransaction.WalletId);

                    await _realtime.PublishDataChangedAsync(
                        "transactions",
                        "failed",
                        institutionId: failedWallet?.InstitutionId,
                        data: new
                        {
                            transactionId = failedTransaction.Id,
                            failedTransaction.WalletId,
                            failedTransaction.Amount,
                            failedTransaction.Type,
                            failedTransaction.Status,
                            failedTransaction.UpdatedAt
                        });
                }
            }

            return false;
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

        private string ResolveVnPayIpAddress(HttpContext httpContext)
        {
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(ipAddress) &&
                ipAddress != "::1" &&
                ipAddress != "127.0.0.1")
            {
                return ipAddress;
            }

            return _configuration["VnPay:DefaultIpAddress"] ?? "127.0.0.1";
        }

        private async Task DispatchWalletUpdatedAsync(Wallet wallet, Transaction transaction, decimal amount)
        {
            var payload = new
            {
                wallet.InstitutionId,
                walletId = wallet.Id,
                wallet.Balance,
                amount,
                transactionId = transaction.Id,
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
                "succeeded",
                institutionId: wallet.InstitutionId,
                data: payload);
            await _notifications.SendToInstitutionAdminsAsync(
                wallet.InstitutionId,
                "Nạp tiền thành công",
                $"Nạp {amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN"))} VNĐ thành công.",
                NotificationType.LowBalanceAlert,
                ReferenceTypeEnum.Transaction,
                transaction.Id);
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
