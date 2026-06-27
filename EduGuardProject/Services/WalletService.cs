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
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = transactions.Select(MapTransaction).ToList();
            return (data, totalItems);
        }

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

            long amountInCents = (long)(dto.Amount * 100);

            vnpay.AddRequestData("vnp_Version", "2.1.0");
            vnpay.AddRequestData("vnp_Command", "pay");
            vnpay.AddRequestData("vnp_TmnCode", tmnCode);
            vnpay.AddRequestData("vnp_Amount", amountInCents.ToString());
            vnpay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", "VND");

            string ipAddress = ResolveVnPayIpAddress(httpContext);
            vnpay.AddRequestData("vnp_IpAddr", ipAddress);

            vnpay.AddRequestData("vnp_Locale", "vn");
            vnpay.AddRequestData("vnp_OrderInfo", "NapTienVi_EduGuard");
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
        }

        public async Task<bool> ProcessVnPayReturnAsync(IQueryCollection query)
        {
            var vnpay = new VnPayLibrary();

            foreach (var key in query.Keys)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, query[key].ToString());
                }
            }

            string vnpSecureHash = query["vnp_SecureHash"].ToString();
            if (string.IsNullOrWhiteSpace(vnpSecureHash))
                return false;

            string secretKey = _configuration["VnPay:HashSecret"]
                ?? throw new InvalidOperationException("VnPay:HashSecret is not configured.");

            bool isValidSignature = vnpay.ValidateSignature(vnpSecureHash, secretKey);

            if (isValidSignature)
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
