using Application.Common.Interfaces; // برای ICryptoPayApiClient, IUserRepository, ISubscriptionRepository, IAppDbContext
using Application.DTOs.CryptoPay;
using Application.Interfaces;
using AutoMapper;
using Domain.Entities; // برای Subscription, Transaction
using Microsoft.Extensions.Logging;
using Shared.Results;
using System.Globalization;
using System.Text.Json;

namespace Application.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly ICryptoPayApiClient _cryptoPayApiClient;
        private readonly IUserRepository _userRepository; // برای دریافت اطلاعات کاربر
        private readonly ISubscriptionRepository _subscriptionRepository; // برای ایجاد اشتراک پس از پرداخت
        private readonly ITransactionRepository _transactionRepository; // برای ثبت تراکنش پرداخت
        private readonly IAppDbContext _context; // Unit of Work
        private readonly IMapper _mapper;
        private readonly ILogger<PaymentService> _logger;

        // شما به یک راه برای دریافت اطلاعات پلن‌ها (قیمت، مدت اعتبار و ...) نیاز دارید
        // private readonly IPlanRepository _planRepository; یا یک سرویس مشابه

        public PaymentService(
            ICryptoPayApiClient cryptoPayApiClient,
            IUserRepository userRepository,
            ISubscriptionRepository subscriptionRepository,
            ITransactionRepository transactionRepository,
            IAppDbContext context,
            IMapper mapper,
            ILogger<PaymentService> logger)
        {
            _cryptoPayApiClient = cryptoPayApiClient;
            _userRepository = userRepository;
            _subscriptionRepository = subscriptionRepository;
            _transactionRepository = transactionRepository;
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }
        // Guids are still useful for identifying plans for logging and descriptions.
        private static readonly Guid PremiumPlanId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        private static readonly Guid BestPlanId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        /// <summary>
        /// Asynchronously creates a new cryptocurrency payment invoice via the CryptoPay API
        /// for a specific user and plan. Handles user existence check and integrates with
        /// the transaction repository to log pending payments. Handles potential API and data access errors.
        /// </summary>
        /// <param name="userId">The ID of the user initiating the payment.</param>
        /// <param name="planId">The ID of the subscription plan.</param>
        /// <param name="selectedCryptoAsset">The selected cryptocurrency asset (e.g., "USDT").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result object containing the created invoice DTO on success, or errors on failure.</returns>
        public async Task<Result<CryptoPayInvoiceDto>> CreateCryptoPaymentInvoiceAsync(
             Guid userId, Guid planId, string selectedCryptoAsset, decimal amount, CancellationToken cancellationToken = default)
        {
            if (userId == Guid.Empty || planId == Guid.Empty || string.IsNullOrWhiteSpace(selectedCryptoAsset) || amount <= 0)
            {
                _logger.LogWarning("Invalid input. UserID: {UserId}, PlanID: {PlanId}, Asset: {Asset}, Amount: {Amount}", userId, planId, selectedCryptoAsset, amount);
                return Result<CryptoPayInvoiceDto>.Failure("Invalid parameters for invoice creation.");
            }

            _logger.LogInformation("Creating invoice for UserID: {UserId}, PlanID: {PlanId}, Asset: {Asset}, Amount: {Amount}", userId, planId, selectedCryptoAsset, amount);

            try
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (user == null)
                {
                    return Result<CryptoPayInvoiceDto>.Failure("User not found.");
                }

                // ✅ REMOVED: No more internal price calculation.
                // We only get the plan name for the description.
                string planName = planId switch
                {
                    var id when id == PremiumPlanId => "Premium Plan",
                    var id when id == BestPlanId => "Best Plan",
                    _ => "Unknown Plan"
                };

                if (planName == "Unknown Plan")
                {
                    _logger.LogWarning("Unknown PlanID: {PlanId} for UserID: {UserId}.", planId, userId);
                    return Result<CryptoPayInvoiceDto>.Failure("Selected plan is not valid.");
                }

                string planDescription = $"Subscription to {planName} for user {user.Username}";
                string internalOrderId = $"SUB-{planId}-{userId}-{DateTime.UtcNow.Ticks}";

                CreateCryptoPayInvoiceRequestDto invoiceRequest = new()
                {
                    Asset = selectedCryptoAsset,
                    // ✅ CHANGED: Use the 'amount' parameter directly.
                    // Use InvariantCulture to ensure '.' is the decimal separator.
                    // Using "F8" to support cryptocurrencies with high precision like BTC.
                    Amount = amount.ToString("F8", CultureInfo.InvariantCulture),
                    Description = planDescription,
                    Payload = JsonSerializer.Serialize(new { UserId = userId, PlanId = planId, OrderId = internalOrderId }),
                    PaidBtnName = "callback",
                    PaidBtnUrl = $"https://t.me/your_bot_username?start={internalOrderId}", // REMEMBER TO CHANGE BOT USERNAME
                    ExpiresInSeconds = 3600
                };

                Result<CryptoPayInvoiceDto> invoiceResult = await _cryptoPayApiClient.CreateInvoiceAsync(invoiceRequest, cancellationToken);

                if (invoiceResult.Succeeded && invoiceResult.Data != null)
                {
                    // The rest of the logic for creating a pending transaction remains the same.
                    Transaction pendingTransaction = new()
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        Amount = amount, // This is the crypto amount
                        Currency = selectedCryptoAsset, // A good idea to add a 'Currency' field to your Transaction entity
                        Type = Domain.Enums.TransactionType.SubscriptionPayment,
                        Description = $"Pending {selectedCryptoAsset} payment for {planName}. Invoice ID: {invoiceResult.Data.InvoiceId}",
                        PaymentGatewayInvoiceId = invoiceResult.Data.InvoiceId.ToString(),
                        Status = "Pending",
                        Timestamp = DateTime.UtcNow
                    };

                    await _transactionRepository.AddAsync(pendingTransaction, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken);

                    return invoiceResult;
                }

                return Result<CryptoPayInvoiceDto>.Failure(invoiceResult.Errors ?? ["Failed to create payment invoice."]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during invoice creation for UserID {UserId}.", userId);
                return Result<CryptoPayInvoiceDto>.Failure("An unexpected internal error occurred.");
            }
        }

        /// <summary>
        /// Asynchronously checks the status of a specific CryptoPay invoice by its ID.
        /// Handles potential API errors during status retrieval.
        /// </summary>
        /// <param name="invoiceId">The ID of the invoice to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A Result object containing the invoice DTO if found and status retrieved, or errors on failure.</returns>
        public async Task<Result<CryptoPayInvoiceDto>> CheckInvoiceStatusAsync(long invoiceId, CancellationToken cancellationToken = default)
        {
            // Basic validation (optional - invoiceId > 0 is usually implied by long type, but good practice)
            if (invoiceId <= 0)
            {
                _logger.LogWarning("Attempted to check status for invalid InvoiceID: {InvoiceId}", invoiceId);
                return Result<CryptoPayInvoiceDto>.Failure("Invalid invoice ID.");
            }

            _logger.LogInformation("Checking status for CryptoPay InvoiceID: {InvoiceId}", invoiceId);

            try
            {
                // Prepare the request to get invoice(s) by ID.
                GetCryptoPayInvoicesRequestDto request = new() { InvoiceIds = invoiceId.ToString() };

                // Call the CryptoPay API to get invoice information. **CRITICAL point of failure (Network/API/Parsing).**
                Result<IEnumerable<CryptoPayInvoiceDto>> result = await _cryptoPayApiClient.GetInvoicesAsync(request, cancellationToken);

                // Handle the functional result from the CryptoPay API client.
                if (result.Succeeded && result.Data != null && result.Data.Any())
                {
                    // If successful and data found, return the first invoice (should be the one requested).
                    CryptoPayInvoiceDto invoice = result.Data.First();
                    _logger.LogInformation("Status for CryptoPay InvoiceID {InvoiceId} is {Status}", invoiceId, invoice.Status);
                    return Result<CryptoPayInvoiceDto>.Success(invoice);
                }
                else
                {
                    // CryptoPay API client reported a functional error or invoice not found/data is empty.
                    _logger.LogWarning("Could not retrieve status or invoice not found for CryptoPay InvoiceID {InvoiceId}. Errors: {Errors}",
                        invoiceId, string.Join(", ", result.Errors ?? ["No specific errors reported by API client."]));

                    // FIX (CS8604): Add a null check before calling .Any() to prevent a NullReferenceException.
                    // This ensures that if Errors is null, we fall back to the default message.
                    return Result<CryptoPayInvoiceDto>.Failure(result.Errors != null && result.Errors.Any() ? result.Errors : ["Invoice not found or failed to retrieve status."]);
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                // Handle cancellation specifically.
                _logger.LogInformation(ex, "CryptoPay invoice status check for InvoiceID {InvoiceId} was cancelled.", invoiceId);
                throw; // Re-throw cancellation.
            }
            // Catch specific exceptions if needed (e.g., JsonException for parsing errors, HttpRequestException for network errors).
            // catch (JsonException jsonEx)
            // {
            //     _logger.LogError(jsonEx, "JSON parsing error during CryptoPay invoice status check for InvoiceID {InvoiceId}.", invoiceId);
            //     return Result<CryptoPayInvoiceDto>.Failure($"Internal error: Failed to parse invoice data. ({jsonEx.Message})");
            // }
            // catch (HttpRequestException httpEx) // Catch network errors before API responds
            // {
            //     _logger.LogError(httpEx, "Network error calling CryptoPay API for InvoiceID {InvoiceId}.", invoiceId);
            //     return Result<CryptoPayInvoiceDto>.Failure($"Could not communicate with payment gateway to check status. Please try again. ({httpEx.Message})");
            // }
            catch (Exception ex)
            {
                // Catch any other unexpected technical exceptions (e.g., errors within _cryptoPayApiClient not converted to Result, other unexpected issues).
                // Log the critical technical error.
                _logger.LogError(ex, "An unexpected error occurred during CryptoPay invoice status check process for InvoiceID {InvoiceId}.", invoiceId);

                // Return a generic failure result for unhandled technical errors.
                return Result<CryptoPayInvoiceDto>.Failure($"An unexpected error occurred while checking invoice status. Please try again later.");
                // Optionally include ex.Message for logging but not usually in the user-facing message.
                // return Result<CryptoPayInvoiceDto>.Failure($"An unexpected error occurred: {ex.Message}");
            }
        }
    }
}