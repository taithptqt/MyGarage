using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Requests.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public sealed partial class ChatbotToolDefinitionRegistry
{
    private async Task<object> ExecuteToolPayloadAsync(
        ChatbotToolExecutionRequest request,
        string normalizedToolName,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        return normalizedToolName switch
        {
            PricingInventoryLookupTool => await ExecutePricingLookupPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerBenefitsTool => await ExecuteGetCustomerBenefitsPayloadAsync(request, cancellationToken),
            GetCustomerVouchersTool => await ExecuteGetCustomerVouchersPayloadAsync(request, argsRoot, cancellationToken),
            ListCustomerVehiclesTool => await ExecuteListCustomerVehiclesPayloadAsync(request, argsRoot, cancellationToken),
            GetBranchListTool => await ExecuteGetBranchListPayloadAsync(argsRoot, cancellationToken),
            SearchStandardServicesTool => await ExecuteSearchStandardServicesPayloadAsync(argsRoot, cancellationToken),
            GetAvailableSlotsTool => await ExecuteGetAvailableSlotsPayloadAsync(argsRoot, cancellationToken),
            ReadBookingStateTool => await ExecuteReadBookingStatePayloadAsync(request, cancellationToken),
            UpdateBookingStateTool => await ExecuteUpdateBookingStatePayloadAsync(request, argsRoot, cancellationToken),
            CreateAppointmentDraftTool => await ExecuteCreateAppointmentDraftPayloadAsync(argsRoot, cancellationToken),
            ConfirmAppointmentBookingTool => await ExecuteConfirmAppointmentBookingPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerServiceHistoryTool => await ExecuteGetCustomerServiceHistoryPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerServiceOrderStatusTool => await ExecuteGetCustomerServiceOrderStatusPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerInvoicePaymentStatusTool => await ExecuteGetCustomerInvoicePaymentStatusPayloadAsync(request, argsRoot, cancellationToken),
            GetCustomerWarrantyHistoryTool => await ExecuteGetCustomerWarrantyHistoryPayloadAsync(request, argsRoot, cancellationToken),
            CreateServiceOrderDraftTool => await ExecuteCreateServiceOrderDraftPayloadAsync(request, argsRoot, cancellationToken),
            CreateRetailOrderDraftTool => await ExecuteCreateRetailOrderDraftPayloadAsync(request, argsRoot, cancellationToken),
            _ => throw new ToolExecutionException("TOOL_NOT_IMPLEMENTED", "Tool is allowlisted but no executor is registered.")
        };
    }

    private async Task<object> ExecutePricingLookupPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var productHint = ReadString(argsRoot, "productHint");
        if (string.IsNullOrWhiteSpace(productHint) || productHint.Length > 200)
        {
            throw new ToolExecutionException("INVALID_TOOL_ARGS", "productHint is required.");
        }

        var lookup = await _pricingInventoryGuard.ResolveAsync(
            request.CustomerId,
            request.BranchId,
            productHint,
            cancellationToken);

        return new
        {
            lookup.ProductFound,
            lookup.ProductHint,
            lookup.ProductId,
            lookup.ProductName,
            lookup.ProductSku,
            lookup.PriceAvailable,
            lookup.Price,
            lookup.PriceSource,
            lookup.StockAvailable,
            lookup.StockQuantity,
            lookup.StockSource
        };
    }

    private async Task<object> ExecuteGetCustomerBenefitsPayloadAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var loyalty = await _loyaltyStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "uu dai thanh vien",
            cancellationToken);
        var vouchers = await _voucherStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "voucher",
            cancellationToken);

        return new
        {
            loyaltySummary = loyalty?.Summary,
            loyalty = loyalty?.LoyaltyStatus,
            voucherSummary = vouchers?.Summary,
            vouchers = vouchers?.VoucherStatus?.AvailableVouchers
                ?? Array.Empty<MyGarage.DTOs.Responses.Chatbot.ChatbotVoucherStatusItemDto>()
        };
    }

    private async Task<object> ExecuteGetCustomerVouchersPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var includeUpcoming = ReadBool(argsRoot, "includeUpcoming") ?? true;
        var lookup = await _voucherStatusLookupService.TryLookupAsync(
            request.CustomerId,
            "voucher",
            cancellationToken);
        var items = lookup?.VoucherStatus?.AvailableVouchers ?? Array.Empty<DTOs.Responses.Chatbot.ChatbotVoucherStatusItemDto>();
        if (!includeUpcoming)
        {
            items = items.Where(x => x.IsCurrentlyActive).ToArray();
        }

        return new
        {
            summary = lookup?.Summary,
            count = items.Count,
            vouchers = items
        };
    }

    private async Task<object> ExecuteListCustomerVehiclesPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var includeInactiveOwnership = ReadBool(argsRoot, "includeInactiveOwnership") ?? false;
        var query = _context.CustomerVehicles
            .AsNoTracking()
            .Where(x =>
                x.CustomerId == request.CustomerId &&
                x.IsDeleted != true &&
                x.Vehicle.IsDeleted != true);
        if (!includeInactiveOwnership)
        {
            query = query.Where(x => x.IsCurrentOwner != false);
        }

        var vehicles = await query
            .OrderBy(x => x.Vehicle.LicensePlate)
            .Take(30)
            .Select(x => new
            {
                vehicleId = x.VehicleId,
                licensePlate = x.Vehicle.LicensePlate,
                modelName = x.Vehicle.Model.ModelName,
                brandName = x.Vehicle.Model.Brand.Name
            })
            .ToListAsync(cancellationToken);

        return new { count = vehicles.Count, vehicles };
    }

    private async Task<object> ExecuteGetBranchListPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var onlyActive = ReadBool(argsRoot, "onlyActive") ?? true;
        var query = _context.Branches.AsNoTracking().Where(x => x.IsDeleted != true);
        if (onlyActive)
        {
            query = query.Where(x => x.Status != null && x.Status.ToUpper() == "ACTIVE");
        }

        var branches = await query
            .OrderBy(x => x.Name)
            .Select(x => new { branchId = x.Id, branchName = x.Name, status = x.Status, address = x.Address, hotline = x.Hotline })
            .ToListAsync(cancellationToken);
        return new { count = branches.Count, branches };
    }

    private async Task<object> ExecuteSearchStandardServicesPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var keyword = ReadString(argsRoot, "keyword");
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 30);
        var query = _context.StandardServices.AsNoTracking().Where(x => x.IsDeleted != true && x.IsActive == true);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var token = keyword.Trim().ToUpperInvariant();
            query = query.Where(x =>
                (x.Code != null && x.Code.ToUpper().Contains(token)) ||
                (x.Name != null && x.Name.ToUpper().Contains(token)));
        }

        var services = await query
            .OrderBy(x => x.Name)
            .Take(limit)
            .Select(x => new { serviceId = x.Id, serviceCode = x.Code, serviceName = x.Name, basePrice = x.BasePrice, standardMinutes = x.StandardMinutes })
            .ToListAsync(cancellationToken);
        return new { count = services.Count, services };
    }

    private async Task<object> ExecuteGetAvailableSlotsPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var branchId = ReadLong(argsRoot, "branchId");
        var dateText = ReadString(argsRoot, "date");
        if (!branchId.HasValue || branchId.Value <= 0)
        {
            throw new ToolExecutionException("INVALID_TOOL_ARGS", "branchId is required.");
        }

        if (!DateOnly.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ToolExecutionException("INVALID_TOOL_ARGS", "date must be in yyyy-MM-dd format.");
        }

        var serviceIds = ReadLongArray(argsRoot, "serviceIds");
        var requestedServiceIds = serviceIds.Count == 0 ? null : string.Join(",", serviceIds);
        var vehicleCount = Math.Clamp(ReadInt(argsRoot, "vehicleCount") ?? 1, 1, 10);
        var isParallel = ReadBool(argsRoot, "isParallel") ?? false;
        var bookingFlow = ReadString(argsRoot, "bookingFlow") ?? "FIXED";
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var slots = await _appointmentService.GetAvailableSlotsAsync(
            branchId.Value,
            date,
            requestedServiceIds,
            requestedPackageIds: null,
            bookingFlow: bookingFlow,
            vehicleCount: vehicleCount,
            isParallel: isParallel);

        var result = slots
            .Where(x => x.IsAvailable && x.RemainingCapacity > 0)
            .OrderBy(x => x.StartTime)
            .Take(limit)
            .Select(x => new { startTime = x.StartTime, endTime = x.EndTime, remainingCapacity = x.RemainingCapacity, maxCapacity = x.MaxCapacity })
            .ToList();

        return new { date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), count = result.Count, slots = result };
    }

    private async Task<object> ExecuteReadBookingStatePayloadAsync(
        ChatbotToolExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _chatbotService.GetSessionByIdAsync(request.CustomerId, request.SessionId, cancellationToken)
            ?? throw new ToolExecutionException("SESSION_NOT_FOUND", "Session not found.");
        var stateRoot = ParseIntentStateRoot(session.IntentState);
        var booking = stateRoot["booking"] as JsonObject ?? new JsonObject();
        return new
        {
            step = ReadString(booking, "Step") ?? ReadString(booking, "step"),
            vehicleId = ReadLong(booking, "VehicleId") ?? ReadLong(booking, "vehicleId"),
            branchId = ReadLong(booking, "BranchId") ?? ReadLong(booking, "branchId"),
            serviceIds = ReadLongArray(booking, "ServiceIds").Concat(ReadLongArray(booking, "serviceIds")).Distinct().ToArray(),
            bookingDate = ReadString(booking, "BookingDate") ?? ReadString(booking, "bookingDate"),
            selectedSlotStartUtc = ReadString(booking, "SelectedSlotStartUtc") ?? ReadString(booking, "selectedSlotStartUtc"),
            selectedSlotEndUtc = ReadString(booking, "SelectedSlotEndUtc") ?? ReadString(booking, "selectedSlotEndUtc")
        };
    }

    private async Task<object> ExecuteUpdateBookingStatePayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var session = await _chatbotService.GetSessionByIdAsync(request.CustomerId, request.SessionId, cancellationToken)
            ?? throw new ToolExecutionException("SESSION_NOT_FOUND", "Session not found.");
        var root = ParseIntentStateRoot(session.IntentState);
        if (ReadBool(argsRoot, "reset") == true)
        {
            root["booking"] = new JsonObject();
        }

        var booking = root["booking"] as JsonObject ?? new JsonObject();
        if (argsRoot.ContainsKey("vehicleId")) booking["VehicleId"] = ReadLong(argsRoot, "vehicleId");
        if (argsRoot.ContainsKey("branchId")) booking["BranchId"] = ReadLong(argsRoot, "branchId");
        if (argsRoot.ContainsKey("serviceIds")) booking["ServiceIds"] = new JsonArray(ReadLongArray(argsRoot, "serviceIds").Select(x => JsonValue.Create(x)).ToArray());
        if (argsRoot.ContainsKey("preferredDate")) booking["BookingDate"] = ReadString(argsRoot, "preferredDate");
        if (argsRoot.ContainsKey("preferredStartTimeUtc")) booking["SelectedSlotStartUtc"] = ReadString(argsRoot, "preferredStartTimeUtc");
        if (argsRoot.ContainsKey("preferredEndTimeUtc")) booking["SelectedSlotEndUtc"] = ReadString(argsRoot, "preferredEndTimeUtc");

        root["booking"] = booking;
        root["flow"] = new JsonObject
        {
            ["intent"] = nameof(ChatbotIntentType.BOOKING),
            ["updated_at"] = DateTime.UtcNow
        };

        await _chatbotService.UpdateSessionStateAsync(
            request.CustomerId,
            request.SessionId,
            new ChatbotSessionStateUpdateRequestDto
            {
                CurrentIntent = nameof(ChatbotIntentType.BOOKING),
                IntentState = root.ToJsonString()
            },
            cancellationToken);

        return await ExecuteReadBookingStatePayloadAsync(request, cancellationToken);
    }

    private async Task<object> ExecuteCreateAppointmentDraftPayloadAsync(
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var branchId = ReadLong(argsRoot, "branchId");
        var vehicleId = ReadLong(argsRoot, "vehicleId");
        var serviceIds = ReadLongArray(argsRoot, "serviceIds");
        var startTime = ReadDateTime(argsRoot, "startTimeUtc");
        var endTime = ReadDateTime(argsRoot, "endTimeUtc") ?? startTime?.AddMinutes(60);

        var missing = new List<string>();
        if (!branchId.HasValue || branchId.Value <= 0) missing.Add("branchId");
        if (!vehicleId.HasValue || vehicleId.Value <= 0) missing.Add("vehicleId");
        if (serviceIds.Count == 0) missing.Add("serviceIds");
        if (!startTime.HasValue) missing.Add("startTimeUtc");

        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var start = startTime!.Value;
        var resolvedBranchId = branchId!.Value;
        var date = DateOnly.FromDateTime(VietnamTime.ToVietnamTime(start));
        var slots = await _appointmentService.GetAvailableSlotsAsync(
            resolvedBranchId,
            date,
            string.Join(",", serviceIds),
            requestedPackageIds: null,
            bookingFlow: "FIXED",
            vehicleCount: 1,
            isParallel: false);
        var target = slots.FirstOrDefault(x =>
            x.IsAvailable &&
            x.RemainingCapacity > 0 &&
            x.StartTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) == start.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

        return new
        {
            canProceed = target is not null,
            reasonCode = target is null ? "SLOT_UNAVAILABLE" : null,
            preview = target is null ? null : new { branchId, vehicleId, serviceIds, startTimeUtc = startTime, endTimeUtc = endTime }
        };
    }

    private async Task<object> ExecuteConfirmAppointmentBookingPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var draft = await ExecuteCreateAppointmentDraftPayloadAsync(argsRoot, cancellationToken);
        var draftNode = JsonSerializer.SerializeToNode(draft) as JsonObject;
        if (draftNode?["canProceed"]?.GetValue<bool>() != true)
        {
            return new { confirmed = false, draft };
        }

        var branchId = ReadLong(argsRoot, "branchId")!.Value;
        var vehicleId = ReadLong(argsRoot, "vehicleId")!.Value;
        var serviceIds = ReadLongArray(argsRoot, "serviceIds");
        var startTime = ReadDateTime(argsRoot, "startTimeUtc")!.Value;
        var endTime = ReadDateTime(argsRoot, "endTimeUtc") ?? startTime.AddMinutes(60);
        var customerNote = ReadString(argsRoot, "customerNote");

        var customer = await _context.Customers
            .AsNoTracking()
            .Where(x => x.Id == request.CustomerId && x.IsDeleted != true)
            .Select(x => new { x.FullName, x.Phone })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ToolExecutionException("CUSTOMER_NOT_FOUND", "Authenticated customer not found.");

        var created = await _appointmentService.BookAsync(
            request.CustomerId,
            new BookAppointmentRequestDto
            {
                BranchId = branchId,
                VehicleId = vehicleId,
                StartTime = startTime,
                EndTime = endTime,
                BookingFlow = "FIXED",
                RequestedServiceIds = string.Join(",", serviceIds),
                CustomerFullName = customer.FullName,
                CustomerPhone = customer.Phone,
                CustomerNotes = customerNote
            });

        return new
        {
            confirmed = true,
            appointmentId = created.Id,
            message = $"Appointment booked successfully. AppointmentId={created.Id}."
        };
    }

    private async Task<object> ExecuteGetCustomerServiceHistoryPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 20);
        var orders = await _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId)
            .OrderByDescending(x => x.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .Select(x => new
            {
                orderId = x.Id,
                orderCode = x.Code,
                status = x.Status,
                paymentStatus = x.PaymentStatus,
                finalAmount = x.FinalAmount,
                createdAt = x.CreatedAt,
                vehicleId = x.VehicleId,
                licensePlate = x.Vehicle.LicensePlate,
                vehicleModel = x.Vehicle.Model.ModelName
            })
            .ToListAsync(cancellationToken);

        return new
        {
            count = orders.Count,
            serviceOrders = orders
        };
    }

    private async Task<object> ExecuteGetCustomerServiceOrderStatusPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var orderId = ReadLong(argsRoot, "orderId");
        var orderCode = ReadString(argsRoot, "orderCode");
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var query = _context.ServiceOrders
            .AsNoTracking()
            .Where(x => x.CustomerId == request.CustomerId);

        if (orderId.HasValue && orderId.Value > 0)
        {
            query = query.Where(x => x.Id == orderId.Value);
        }

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            var normalizedCode = orderCode.Trim().ToUpperInvariant();
            query = query.Where(x => x.Code.ToUpper().Contains(normalizedCode));
        }

        var orders = await query
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .Select(x => new
            {
                orderId = x.Id,
                orderCode = x.Code,
                status = x.Status,
                qcStatus = x.Qcstatus,
                paymentStatus = x.PaymentStatus,
                promisedHandover = x.PromisedHandoverTime,
                actualHandover = x.ActualHandover,
                updatedAt = x.UpdatedAt ?? x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new
        {
            count = orders.Count,
            serviceOrders = orders,
            hasExactFilter = orderId.HasValue || !string.IsNullOrWhiteSpace(orderCode)
        };
    }

    private async Task<object> ExecuteGetCustomerInvoicePaymentStatusPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var invoiceId = ReadLong(argsRoot, "invoiceId");
        var orderCode = ReadString(argsRoot, "orderCode");
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var query = _context.Invoices
            .AsNoTracking()
            .Where(x =>
                (x.ServiceOrderId != null && x.ServiceOrder != null && x.ServiceOrder.CustomerId == request.CustomerId) ||
                (x.RetailOrderId != null && x.RetailOrder != null && x.RetailOrder.CustomerId == request.CustomerId));

        if (invoiceId.HasValue && invoiceId.Value > 0)
        {
            query = query.Where(x => x.Id == invoiceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(orderCode))
        {
            var normalizedCode = orderCode.Trim().ToUpperInvariant();
            query = query.Where(x =>
                (x.ServiceOrder != null && x.ServiceOrder.Code.ToUpper().Contains(normalizedCode)) ||
                (x.RetailOrder != null && x.RetailOrder.OrderCode.ToUpper().Contains(normalizedCode)));
        }

        var invoices = await query
            .OrderByDescending(x => x.IssuedAt ?? x.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .Select(x => new
            {
                invoiceId = x.Id,
                serviceOrderCode = x.ServiceOrder != null ? x.ServiceOrder.Code : null,
                retailOrderCode = x.RetailOrder != null ? x.RetailOrder.OrderCode : null,
                totalAmount = x.TotalAmount,
                paidAmount = x.PaidAmount,
                paymentStatus = x.PaymentStatus,
                issuedAt = x.IssuedAt,
                remainingAmount = (x.TotalAmount ?? 0m) - (x.PaidAmount ?? 0m)
            })
            .ToListAsync(cancellationToken);

        return new
        {
            count = invoices.Count,
            invoices
        };
    }

    private async Task<object> ExecuteGetCustomerWarrantyHistoryPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var vehicleId = ReadLong(argsRoot, "vehicleId");
        var limit = Math.Clamp(ReadInt(argsRoot, "limit") ?? 10, 1, 20);

        var query = _context.ServiceOrderDetails
            .AsNoTracking()
            .Where(x =>
                x.ServiceOrder.CustomerId == request.CustomerId &&
                (
                    x.IsWarrantyItem == true ||
                    (x.StandardService != null &&
                     ((x.StandardService.WarrantyMonths ?? 0) > 0 || (x.StandardService.WarrantyKm ?? 0) > 0))
                ));

        if (vehicleId.HasValue && vehicleId.Value > 0)
        {
            query = query.Where(x => x.ServiceOrder.VehicleId == vehicleId.Value);
        }

        var warrantyItems = await query
            .OrderByDescending(x => x.ServiceOrder.CreatedAt ?? DateTime.MinValue)
            .Take(limit)
            .Select(x => new
            {
                serviceOrderId = x.ServiceOrderId,
                serviceOrderCode = x.ServiceOrder.Code,
                vehicleId = x.ServiceOrder.VehicleId,
                licensePlate = x.ServiceOrder.Vehicle.LicensePlate,
                itemName = x.ItemName,
                isWarrantyItem = x.IsWarrantyItem,
                warrantyMonths = x.StandardService != null ? x.StandardService.WarrantyMonths : null,
                warrantyKm = x.StandardService != null ? x.StandardService.WarrantyKm : null,
                serviceOrderStatus = x.ServiceOrder.Status,
                createdAt = x.ServiceOrder.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new
        {
            count = warrantyItems.Count,
            items = warrantyItems
        };
    }

    private async Task<object> ExecuteCreateServiceOrderDraftPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var vehicleId = ReadLong(argsRoot, "vehicleId");
        var originBranchId = ReadLong(argsRoot, "originBranchId") ?? request.BranchId;
        var executeBranchId = ReadLong(argsRoot, "executeBranchId") ?? originBranchId;
        var serviceIds = ReadLongArray(argsRoot, "serviceIds");
        var customerNote = ReadString(argsRoot, "customerNote");

        var missing = new List<string>();
        if (!vehicleId.HasValue || vehicleId.Value <= 0) missing.Add("vehicleId");
        if (!originBranchId.HasValue || originBranchId.Value <= 0) missing.Add("originBranchId");
        if (!executeBranchId.HasValue || executeBranchId.Value <= 0) missing.Add("executeBranchId");
        if (serviceIds.Count == 0) missing.Add("serviceIds");

        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var resolvedVehicleId = vehicleId!.Value;
        var resolvedOriginBranchId = originBranchId!.Value;
        var resolvedExecuteBranchId = executeBranchId!.Value;

        var ownsVehicle = await _context.CustomerVehicles
            .AsNoTracking()
            .AnyAsync(x =>
                x.CustomerId == request.CustomerId &&
                x.VehicleId == resolvedVehicleId &&
                x.IsDeleted != true &&
                x.IsCurrentOwner != false,
                cancellationToken);

        if (!ownsVehicle)
        {
            return new { canProceed = false, reasonCode = "VEHICLE_NOT_OWNED" };
        }

        var requiredBranchIds = new[] { resolvedOriginBranchId, resolvedExecuteBranchId }
            .Distinct()
            .ToArray();
        var activeBranchIds = await _context.Branches
            .AsNoTracking()
            .Where(x =>
                requiredBranchIds.Contains(x.Id) &&
                x.IsDeleted != true &&
                x.Status != null &&
                x.Status.ToUpper() == "ACTIVE")
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        if (activeBranchIds.Count != requiredBranchIds.Length)
        {
            return new { canProceed = false, reasonCode = "BRANCH_NOT_FOUND_OR_INACTIVE" };
        }

        var distinctServiceIds = serviceIds.Distinct().ToList();
        var services = await _context.StandardServices
            .AsNoTracking()
            .Where(x => distinctServiceIds.Contains(x.Id) && x.IsDeleted != true && x.IsActive == true)
            .Select(x => new
            {
                serviceId = x.Id,
                serviceCode = x.Code,
                serviceName = x.Name,
                basePrice = x.BasePrice
            })
            .ToListAsync(cancellationToken);
        if (services.Count != distinctServiceIds.Count)
        {
            return new { canProceed = false, reasonCode = "SERVICE_NOT_FOUND_OR_INACTIVE" };
        }

        var servicePreviews = services.Cast<object>().ToArray();
        var estimatedTotal = services.Sum(x => x.basePrice);

        return new
        {
            canProceed = true,
            preview = new
            {
                vehicleId,
                originBranchId,
                executeBranchId,
                serviceIds,
                services = servicePreviews,
                estimatedTotal,
                customerNote
            }
        };
    }

    private async Task<object> ExecuteCreateRetailOrderDraftPayloadAsync(
        ChatbotToolExecutionRequest request,
        JsonObject argsRoot,
        CancellationToken cancellationToken)
    {
        var branchId = ReadLong(argsRoot, "branchId") ?? request.BranchId;
        var customerNote = ReadString(argsRoot, "customerNote");

        var draftItems = new List<(long ProductId, decimal Quantity)>();
        if (argsRoot.TryGetPropertyValue("items", out var itemsNode) && itemsNode is JsonArray itemsArray)
        {
            foreach (var itemNode in itemsArray)
            {
                if (itemNode is not JsonObject item)
                {
                    continue;
                }

                var productId = ReadLong(item, "productId");
                var quantity = ReadDecimal(item, "quantity");
                if (productId.HasValue && productId.Value > 0 && quantity.HasValue && quantity.Value > 0)
                {
                    draftItems.Add((productId.Value, quantity.Value));
                }
            }
        }

        var missing = new List<string>();
        if (!branchId.HasValue || branchId.Value <= 0) missing.Add("branchId");
        if (draftItems.Count == 0) missing.Add("items");
        if (missing.Count > 0)
        {
            return new { canProceed = false, reasonCode = "MISSING_REQUIRED_FIELDS", missingFields = missing };
        }

        var resolvedBranchId = branchId!.Value;

        var branchExists = await _context.Branches
            .AsNoTracking()
            .AnyAsync(x =>
                x.Id == resolvedBranchId &&
                x.IsDeleted != true &&
                x.Status != null &&
                x.Status.ToUpper() == "ACTIVE",
                cancellationToken);
        if (!branchExists)
        {
            return new { canProceed = false, reasonCode = "BRANCH_NOT_FOUND_OR_INACTIVE" };
        }

        var productIds = draftItems.Select(x => x.ProductId).Distinct().ToList();
        var products = await _context.Products
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id) && x.IsDeleted != true && x.IsActive == true)
            .Select(x => new
            {
                x.Id,
                x.Sku,
                x.Name,
                unitPrice = x.RetailPrice ?? 0m
            })
            .ToListAsync(cancellationToken);

        var stockByProduct = await _context.Inventories
            .AsNoTracking()
            .Where(x => productIds.Contains(x.ProductId) && x.Warehouse.BranchId == resolvedBranchId)
            .GroupBy(x => x.ProductId)
            .Select(g => new
            {
                productId = g.Key,
                availableStock = g.Sum(x => x.Quantity - (x.ReservedQuantity ?? 0m))
            })
            .ToListAsync(cancellationToken);

        var stockMap = stockByProduct.ToDictionary(x => x.productId, x => x.availableStock);
        var productMap = products.ToDictionary(x => x.Id, x => x);

        var lineItems = draftItems
            .Select(item =>
            {
                var hasProduct = productMap.TryGetValue(item.ProductId, out var product);
                var availableStock = stockMap.TryGetValue(item.ProductId, out var stock) ? stock : 0m;
                var unitPrice = hasProduct ? product!.unitPrice : 0m;
                var lineTotal = unitPrice * item.Quantity;
                return new
                {
                    productId = item.ProductId,
                    sku = hasProduct ? product!.Sku : null,
                    productName = hasProduct ? product!.Name : null,
                    requestedQuantity = item.Quantity,
                    unitPrice,
                    lineTotal,
                    availableStock,
                    isProductFound = hasProduct,
                    isStockSufficient = availableStock >= item.Quantity
                };
            })
            .ToList();

        var canProceed = lineItems.Count > 0 &&
            lineItems.All(x => x.isProductFound && x.isStockSufficient);
        var subTotal = lineItems.Sum(x => x.lineTotal);

        return new
        {
            canProceed,
            reasonCode = canProceed ? null : "DRAFT_VALIDATION_FAILED",
            preview = new
            {
                branchId,
                customerId = request.CustomerId,
                customerNote,
                subTotal,
                items = lineItems
            }
        };
    }

    private static JsonObject ParseIntentStateRoot(string? intentState)
    {
        if (string.IsNullOrWhiteSpace(intentState))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(intentState) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private static string? ReadString(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value && value.TryGetValue<string>(out var stringValue))
        {
            return stringValue?.Trim();
        }

        return node.ToJsonString().Trim('"').Trim();
    }

    private static long? ReadLong(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<long>(out var longValue))
            {
                return longValue;
            }
            if (value.TryGetValue<int>(out var intValue))
            {
                return intValue;
            }
            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? ReadInt(JsonObject? root, string propertyName)
    {
        var value = ReadLong(root, propertyName);
        if (!value.HasValue || value.Value < int.MinValue || value.Value > int.MaxValue)
        {
            return null;
        }

        return (int)value.Value;
    }

    private static bool? ReadBool(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<bool>(out var boolValue))
            {
                return boolValue;
            }
            if (value.TryGetValue<string>(out var stringValue)
                && bool.TryParse(stringValue, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static List<long> ReadLongArray(JsonObject? root, string propertyName)
    {
        var values = new List<long>();
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null || node is not JsonArray array)
        {
            return values;
        }

        foreach (var item in array)
        {
            if (item is not JsonValue value)
            {
                continue;
            }

            if (value.TryGetValue<long>(out var longValue) && longValue > 0)
            {
                values.Add(longValue);
                continue;
            }

            if (value.TryGetValue<int>(out var intValue) && intValue > 0)
            {
                values.Add(intValue);
                continue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && long.TryParse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                && parsed > 0)
            {
                values.Add(parsed);
            }
        }

        return values.Distinct().ToList();
    }

    private static DateTime? ReadDateTime(JsonObject? root, string propertyName)
    {
        var value = ReadString(root, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? ReadDecimal(JsonObject? root, string propertyName)
    {
        if (root is null || !root.TryGetPropertyValue(propertyName, out var node) || node is null)
        {
            return null;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<decimal>(out var decimalValue))
            {
                return decimalValue;
            }

            if (value.TryGetValue<double>(out var doubleValue))
            {
                return (decimal)doubleValue;
            }

            if (value.TryGetValue<string>(out var stringValue)
                && decimal.TryParse(stringValue, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
