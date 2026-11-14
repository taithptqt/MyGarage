using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyGarage.Application.Authorization;
using MyGarage.Data;
using MyGarage.Domain.Rules;
using MyGarage.DTOs.Requests;
using MyGarage.Models;
using MyGarage.Services;
using Xunit;

namespace MyGarage.Tests;

public class PurchaseOrderAndGoodsReceiptTests
{
    // =========================================================================
    // SECTION 1: Pure Domain Rules Unit Tests (PurchaseOrderLifecycleRules)
    // =========================================================================

    [Fact]
    public void NormalizeStatus_HandlesAllAliasesCorrectly()
    {
        Assert.Equal("PENDING", PurchaseOrderLifecycleRules.NormalizeStatus("PENDING_APPROVAL"));
        Assert.Equal("PENDING", PurchaseOrderLifecycleRules.NormalizeStatus("pending"));
        Assert.Equal("PARTIAL_RECEIVED", PurchaseOrderLifecycleRules.NormalizeStatus("partially_received"));
        Assert.Equal("PARTIAL_RECEIVED", PurchaseOrderLifecycleRules.NormalizeStatus("PARTIAL_RECEIVED"));
        Assert.Equal("COMPLETED", PurchaseOrderLifecycleRules.NormalizeStatus("received"));
        Assert.Equal("COMPLETED", PurchaseOrderLifecycleRules.NormalizeStatus("COMPLETED"));
        Assert.Equal("CANCELED", PurchaseOrderLifecycleRules.NormalizeStatus("cancelled"));
        Assert.Equal("CANCELED", PurchaseOrderLifecycleRules.NormalizeStatus("CANCELED"));
        Assert.Equal("APPROVED", PurchaseOrderLifecycleRules.NormalizeStatus("approved"));
        Assert.Equal("DRAFT", PurchaseOrderLifecycleRules.NormalizeStatus("draft"));
    }

    [Fact]
    public void CanTransition_EnforcesStrictFsmTransitions()
    {
        // Valid transitions
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("DRAFT", "PENDING"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("DRAFT", "CANCELED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("PENDING", "APPROVED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("PENDING", "CANCELED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("PENDING", "DRAFT"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("APPROVED", "PARTIAL_RECEIVED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("APPROVED", "COMPLETED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("APPROVED", "CANCELED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("PARTIAL_RECEIVED", "COMPLETED"));
        Assert.True(PurchaseOrderLifecycleRules.CanTransition("PARTIAL_RECEIVED", "CANCELED"));

        // Invalid transitions
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("DRAFT", "COMPLETED"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("DRAFT", "APPROVED"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("PENDING", "COMPLETED"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("COMPLETED", "DRAFT"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("COMPLETED", "APPROVED"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("CANCELED", "APPROVED"));
        Assert.False(PurchaseOrderLifecycleRules.CanTransition("CANCELED", "PENDING"));
    }

    [Fact]
    public void CanApprove_And_CanReceive_EnforceStateGuards()
    {
        Assert.True(PurchaseOrderLifecycleRules.CanApprove("PENDING"));
        Assert.True(PurchaseOrderLifecycleRules.CanApprove("PENDING_APPROVAL"));
        Assert.True(PurchaseOrderLifecycleRules.CanApprove("DRAFT"));
        Assert.False(PurchaseOrderLifecycleRules.CanApprove("APPROVED"));
        Assert.False(PurchaseOrderLifecycleRules.CanApprove("COMPLETED"));
        Assert.False(PurchaseOrderLifecycleRules.CanApprove("CANCELED"));

        Assert.True(PurchaseOrderLifecycleRules.CanReceive("APPROVED"));
        Assert.True(PurchaseOrderLifecycleRules.CanReceive("PARTIAL_RECEIVED"));
        Assert.False(PurchaseOrderLifecycleRules.CanReceive("PENDING"));
        Assert.False(PurchaseOrderLifecycleRules.CanReceive("DRAFT"));
        Assert.False(PurchaseOrderLifecycleRules.CanReceive("COMPLETED"));
        Assert.False(PurchaseOrderLifecycleRules.CanReceive("CANCELED"));
    }

    [Fact]
    public void EvaluateCancellation_BlocksCancellationIfGoodsReceiptsExist()
    {
        // 0 receipts, PENDING -> can cancel
        var cleanInput = new PurchaseOrderLifecycleRules.PoCancellationInput("PENDING", 0m, 0);
        var cleanResult = PurchaseOrderLifecycleRules.EvaluateCancellation(cleanInput);
        Assert.True(cleanResult.CanCancel);
        Assert.Null(cleanResult.BlockingReason);

        // Receipts exist -> blocked
        var withGrnInput = new PurchaseOrderLifecycleRules.PoCancellationInput("APPROVED", 5m, 1);
        var withGrnResult = PurchaseOrderLifecycleRules.EvaluateCancellation(withGrnInput);
        Assert.False(withGrnResult.CanCancel);
        Assert.NotNull(withGrnResult.BlockingReason);

        // Already completed -> blocked
        var completedInput = new PurchaseOrderLifecycleRules.PoCancellationInput("COMPLETED", 10m, 1);
        var completedResult = PurchaseOrderLifecycleRules.EvaluateCancellation(completedInput);
        Assert.False(completedResult.CanCancel);

        // Already canceled -> blocked
        var canceledInput = new PurchaseOrderLifecycleRules.PoCancellationInput("CANCELED", 0m, 0);
        var canceledResult = PurchaseOrderLifecycleRules.EvaluateCancellation(canceledInput);
        Assert.False(canceledResult.CanCancel);
    }

    [Fact]
    public void EvaluateReceiptLine_CapsAcceptedQuantityAndReconciles()
    {
        // Case 1: Incoming fits within remaining
        var input1 = new PurchaseOrderLifecycleRules.PoReceiptLineInput(
            OrderedQuantity: 10m,
            CurrentReceivedQuantity: 3m,
            IncomingQuantity: 4m,
            IncomingUnitPrice: 100_000m,
            FallbackUnitPrice: 100_000m
        );
        var res1 = PurchaseOrderLifecycleRules.EvaluateReceiptLine(input1);
        Assert.True(res1.HasAcceptedQuantity);
        Assert.Equal(4m, res1.AcceptedQuantity);
        Assert.Equal(7m, res1.NewReceivedQuantity);
        Assert.Equal(3m, res1.RemainingQuantity);
        Assert.False(res1.IsLineComplete);
        Assert.Equal(400_000m, res1.LineTotalAmount);

        // Case 2: Incoming exceeds remaining -> capped!
        var input2 = new PurchaseOrderLifecycleRules.PoReceiptLineInput(
            OrderedQuantity: 10m,
            CurrentReceivedQuantity: 7m,
            IncomingQuantity: 8m, // attempts to over-receive 8 when only 3 needed
            IncomingUnitPrice: 100_000m,
            FallbackUnitPrice: 100_000m
        );
        var res2 = PurchaseOrderLifecycleRules.EvaluateReceiptLine(input2);
        Assert.True(res2.HasAcceptedQuantity);
        Assert.Equal(3m, res2.AcceptedQuantity); // capped at 3!
        Assert.Equal(10m, res2.NewReceivedQuantity);
        Assert.Equal(0m, res2.RemainingQuantity);
        Assert.True(res2.IsLineComplete);
        Assert.Equal(300_000m, res2.LineTotalAmount);

        // Case 3: Line already complete -> 0 accepted
        var input3 = new PurchaseOrderLifecycleRules.PoReceiptLineInput(
            OrderedQuantity: 10m,
            CurrentReceivedQuantity: 10m,
            IncomingQuantity: 5m,
            IncomingUnitPrice: 100_000m,
            FallbackUnitPrice: 100_000m
        );
        var res3 = PurchaseOrderLifecycleRules.EvaluateReceiptLine(input3);
        Assert.False(res3.HasAcceptedQuantity);
        Assert.Equal(0m, res3.AcceptedQuantity);
        Assert.True(res3.IsLineComplete);
    }

    [Fact]
    public void CalculateMovingAverageCost_ComputesGenuineWeightedMac()
    {
        // 10 units @ 100,000 + 10 units @ 200,000 = 20 units @ 150,000
        var mac1 = PurchaseOrderLifecycleRules.CalculateMovingAverageCost(10m, 100_000m, 10m, 200_000m);
        Assert.Equal(150_000m, mac1);

        // Initial receipt into empty warehouse: 0 units @ 0 + 5 units @ 120,000 = 120,000
        var mac2 = PurchaseOrderLifecycleRules.CalculateMovingAverageCost(0m, 0m, 5m, 120_000m);
        Assert.Equal(120_000m, mac2);

        // Incoming is 0 -> preserves existing MAC
        var mac3 = PurchaseOrderLifecycleRules.CalculateMovingAverageCost(20m, 150_000m, 0m, 300_000m);
        Assert.Equal(150_000m, mac3);

        // Weighted calculation: 30 units @ 100,000 (3,000,000) + 10 units @ 140,000 (1,400,000) = 4,400,000 / 40 = 110,000
        var mac4 = PurchaseOrderLifecycleRules.CalculateMovingAverageCost(30m, 100_000m, 10m, 140_000m);
        Assert.Equal(110_000m, mac4);

        // Inventory total value: 40 * 110,000 = 4,400,000
        var val = PurchaseOrderLifecycleRules.CalculateTotalInventoryValue(40m, mac4);
        Assert.Equal(4_400_000m, val);
    }

    // =========================================================================
    // SECTION 2: Authorization Unit Tests
    // =========================================================================

    [Fact]
    public async Task PurchaseOrderAuthorizationService_RejectsCustomerAndEnforcesBranch()
    {
        var options = new DbContextOptionsBuilder<GarageContext>()
            .UseInMemoryDatabase($"PoAuthTests_{Guid.NewGuid():N}")
            .Options;

        await using var db = new GarageContext(options, new HttpContextAccessor());
        db.PurchaseOrders.Add(new PurchaseOrder
        {
            Id = 501,
            BranchId = 1,
            DestinationWarehouseId = 1,
            SupplierId = 1,
            Status = "PENDING"
        });
        db.GoodsReceiptNotes.Add(new GoodsReceiptNote
        {
            Id = 601,
            GrnCode = "GRN-01",
            PurchaseOrderId = 501,
            WarehouseId = 1,
            Status = "POSTED",
            ReceivedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var authService = new PurchaseOrderAuthorizationService(db);

        var customerActor = new UserActorContext(100, "Customer", "CUSTOMER", null, false);
        var staffBranch1 = new UserActorContext(201, "Staff", "STAFF", 1, false);
        var staffBranch2 = new UserActorContext(202, "Staff", "STAFF", 2, false);
        var managerBranch1 = new UserActorContext(203, "Staff", "MANAGER", 1, false);
        var storekeeperBranch1 = new UserActorContext(204, "Staff", "STOREKEEPER", 1, false);
        var unassignedStaff = new UserActorContext(205, "Staff", "MANAGER", null, false);
        var admin = new UserActorContext(1, "Staff", "ADMIN", null, true);

        // Customer rejected everywhere
        Assert.False(await authService.CanViewPurchaseOrdersAsync(1, customerActor));
        Assert.False(await authService.CanViewPurchaseOrderAsync(501, customerActor));
        Assert.False(await authService.CanCreatePurchaseOrderAsync(1, customerActor));
        Assert.False(await authService.CanApprovePurchaseOrderAsync(501, customerActor));
        Assert.False(await authService.CanReceiveGoodsAsync(501, customerActor));
        Assert.False(await authService.CanViewGoodsReceiptsAsync(1, customerActor));

        // Staff branch scoping
        Assert.True(await authService.CanViewPurchaseOrdersAsync(1, staffBranch1));
        Assert.False(await authService.CanViewPurchaseOrdersAsync(2, staffBranch1));
        Assert.True(await authService.CanViewPurchaseOrderAsync(501, staffBranch1));
        Assert.False(await authService.CanViewPurchaseOrderAsync(501, staffBranch2));
        Assert.False(await authService.CanViewPurchaseOrdersAsync(null, unassignedStaff));
        Assert.False(await authService.CanViewPurchaseOrderAsync(501, unassignedStaff));
        Assert.False(await authService.CanCreatePurchaseOrderAsync(1, unassignedStaff));
        Assert.False(await authService.CanUpdatePurchaseOrderAsync(501, unassignedStaff));
        Assert.False(await authService.CanApprovePurchaseOrderAsync(501, unassignedStaff));
        Assert.False(await authService.CanViewGoodsReceiptsAsync(null, unassignedStaff));

        // Manager approval keyword gating
        Assert.False(await authService.CanApprovePurchaseOrderAsync(501, staffBranch1)); // ordinary staff cannot approve
        Assert.True(await authService.CanApprovePurchaseOrderAsync(501, managerBranch1)); // manager can approve
        Assert.True(await authService.CanApprovePurchaseOrderAsync(501, admin)); // admin can approve

        // Warehouse receipt keyword gating
        Assert.False(await authService.CanReceiveGoodsAsync(501, staffBranch1)); // ordinary staff cannot receive
        Assert.True(await authService.CanReceiveGoodsAsync(501, storekeeperBranch1)); // storekeeper can receive
        Assert.True(await authService.CanReceiveGoodsAsync(501, admin)); // admin can receive
    }

    // =========================================================================
    // SECTION 3: API Integration Tests via TestWebApplicationFactory
    // =========================================================================

    [Fact]
    public async Task PurchaseOrders_RejectApprovalBypassAndWarehouseFromAnotherBranch()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 10, SupplierName = "Supplier" });
            db.Warehouses.AddRange(
                new Warehouse { Id = 101, BranchId = 1, Code = "W1", Name = "W1", Type = "GENERAL", IsActive = true },
                new Warehouse { Id = 102, BranchId = 2, Code = "W2", Name = "W2", Type = "GENERAL", IsActive = true });
            db.PurchaseOrders.Add(new PurchaseOrder { Id = 501, BranchId = 1, DestinationWarehouseId = 101, SupplierId = 10, Status = "PENDING" });
            return Task.CompletedTask;
        });
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "STAFF", 1));

        var create = await client.PostAsJsonAsync("/api/purchase-orders", new {
            branchId = 1, destinationWarehouseId = 101, supplierId = 10, status = "APPROVED", approvedByStaffId = 200
        });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        var approve = await client.PutAsJsonAsync("/api/purchase-orders/501", new { branchId = 1, status = "APPROVED" });
        Assert.Equal(HttpStatusCode.BadRequest, approve.StatusCode);
        var cancel = await client.PutAsJsonAsync("/api/purchase-orders/501", new { branchId = 1, status = "CANCELED" });
        Assert.Equal(HttpStatusCode.BadRequest, cancel.StatusCode);
        var warehouse = await client.PutAsJsonAsync("/api/purchase-orders/501", new { branchId = 1, destinationWarehouseId = 102 });
        Assert.Equal(HttpStatusCode.BadRequest, warehouse.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
        var po = await db.PurchaseOrders.SingleAsync(x => x.Id == 501);
        Assert.Equal("PENDING", po.Status);
        Assert.Equal(101, po.DestinationWarehouseId);
    }

    [Fact]
    public async Task PoLifecycle_CreateApproveAndDualRoutes_WorkSeamlessly()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 10, SupplierName = "Nhà cung cấp Bosch" });
            db.Warehouses.Add(new Warehouse { Id = 101, BranchId = 1, Code = "WH-01", Name = "Kho Phụ Tùng 1", Type = "GENERAL", IsActive = true });
            db.Products.Add(new Product { Id = 201, Sku = "BOSCH-OIL-01", Name = "Lọc nhớt Bosch", Unit = "Cái", MinStockLevel = 5m });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Staff / Manager at Branch 1
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "MANAGER", 1));

        // 1. Create PO via Dual Route: /api/v1/purchase-orders
        var createPayload = new
        {
            branchId = 1,
            destinationWarehouseId = 101,
            supplierId = 10,
            orderDate = "2026-11-13",
            items = new[]
            {
                new { productId = 201, orderedQuantity = 20m, costPrice = 150_000m }
            }
        };

        var createResponse = await client.PostAsJsonAsync("/api/v1/purchase-orders", createPayload);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        using var createBody = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var poId = createBody.RootElement.GetProperty("id").GetInt64();
        Assert.True(poId > 0);
        Assert.Equal("PENDING", createBody.RootElement.GetProperty("status").GetString());
        Assert.Equal("Nhà cung cấp Bosch", createBody.RootElement.GetProperty("supplierName").GetString());

        // 2. Read PO via legacy /api/purchase-orders/{id} and dual /api/v1/purchase-orders/{id}
        var getV1 = await client.GetAsync($"/api/v1/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.OK, getV1.StatusCode);
        using var v1Body = JsonDocument.Parse(await getV1.Content.ReadAsStringAsync());
        Assert.Equal(poId, v1Body.RootElement.GetProperty("id").GetInt64());
        Assert.Equal(20m, v1Body.RootElement.GetProperty("items")[0].GetProperty("orderedQuantity").GetDecimal());
        Assert.Equal(20m, v1Body.RootElement.GetProperty("items")[0].GetProperty("remainingQuantity").GetDecimal());
        Assert.Equal(0m, v1Body.RootElement.GetProperty("items")[0].GetProperty("receivedQuantity").GetDecimal());

        var getLegacy = await client.GetAsync($"/api/purchase-orders/{poId}");
        Assert.Equal(HttpStatusCode.OK, getLegacy.StatusCode);

        // 3. Approve PO via /api/v1/purchase-orders/{id}/approve
        var approveResponse = await client.PutAsJsonAsync($"/api/v1/purchase-orders/{poId}/approve", new { note = "Đồng ý nhập hàng" });
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);
        using var approveBody = JsonDocument.Parse(await approveResponse.Content.ReadAsStringAsync());
        Assert.Equal("APPROVED", approveBody.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PartialReceipt_UpdatesInventoryMacAndDualRouteGoodsReceipts()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            db.Suppliers.Add(new Supplier { Id = 20, SupplierName = "Nhà cung cấp Denso" });
            db.Warehouses.Add(new Warehouse { Id = 102, BranchId = 1, Code = "WH-02", Name = "Kho Tổng Denso", Type = "GENERAL", IsActive = true });
            db.Products.Add(new Product { Id = 202, Sku = "DENSO-PLUG-01", Name = "Bugi Denso Iridium", Unit = "Hộp", MinStockLevel = 10m });

            // Initial stock in Warehouse 102: 10 units @ MAC 100,000 (TotalValue = 1,000,000)
            db.Inventories.Add(new Inventory
            {
                WarehouseId = 102,
                ProductId = 202,
                Quantity = 10m,
                CurrentMacPrice = 100_000m,
                TotalInventoryValue = 1_000_000m,
                ReservedQuantity = 0m,
                MinStockLevel = 10m
            });

            // Seed Approved PO: 20 units @ CostPrice 160,000
            db.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = 88,
                BranchId = 1,
                DestinationWarehouseId = 102,
                SupplierId = 20,
                OrderDate = new DateOnly(2026, 11, 13),
                Status = "APPROVED",
                TotalAmount = 3_200_000m,
                PurchaseOrderItems = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        Id = 881,
                        ProductId = 202,
                        OrderedQuantity = 20m,
                        ReceivedQuantity = 0m,
                        CostPrice = 160_000m
                    }
                }
            });
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Storekeeper / Manager at Branch 1
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(200, "Staff", "STOREKEEPER", 1));

        // 1. Partial Receipt of 10 units via /api/v1/goods-receipts
        var grnPayload1 = new
        {
            purchaseOrderId = 88,
            notes = "Đợt 1 nhận 10 cái",
            items = new[]
            {
                new { purchaseOrderItemId = 881, productId = 202, receivedQuantity = 10m, costPrice = 160_000m }
            }
        };

        var grnResponse1 = await client.PostAsJsonAsync("/api/v1/goods-receipts", grnPayload1);
        Assert.Equal(HttpStatusCode.Created, grnResponse1.StatusCode);
        using var grnBody1 = JsonDocument.Parse(await grnResponse1.Content.ReadAsStringAsync());
        Assert.Equal("PARTIAL_RECEIVED", grnBody1.RootElement.GetProperty("status").GetString());
        var grnId1 = grnBody1.RootElement.GetProperty("goodsReceiptNoteId").GetInt64();

        // Verify Inventory MAC calculation after Đợt 1:
        // Old: 10 units @ 100k
        // Incoming: 10 units @ 160k
        // Expected New MAC: (10*100k + 10*160k) / 20 = 130,000!
        // Expected New Total Value: 20 * 130k = 2,600,000!
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var inv = await db.Inventories.FirstAsync(i => i.WarehouseId == 102 && i.ProductId == 202);
            Assert.Equal(20m, inv.Quantity);
            Assert.Equal(130_000m, inv.CurrentMacPrice);
            Assert.Equal(2_600_000m, inv.TotalInventoryValue);

            var po = await db.PurchaseOrders.Include(x => x.PurchaseOrderItems).FirstAsync(x => x.Id == 88);
            Assert.Equal("PARTIAL_RECEIVED", po.Status);
            Assert.Equal(10m, po.PurchaseOrderItems.First().ReceivedQuantity);
        }

        // 2. Query statusGroup=APPROVED via /api/v1/purchase-orders - must include PARTIAL_RECEIVED!
        var approvedGroupResponse = await client.GetAsync("/api/v1/purchase-orders?statusGroup=APPROVED&envelope=true");
        Assert.Equal(HttpStatusCode.OK, approvedGroupResponse.StatusCode);
        using var groupBody = JsonDocument.Parse(await approvedGroupResponse.Content.ReadAsStringAsync());
        var items = groupBody.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, x => x.GetProperty("id").GetInt64() == 88 && x.GetProperty("status").GetString() == "PARTIAL_RECEIVED");

        // 3. Second Receipt of remaining 10 units (with attempted over-receipt of 15 units) via /api/purchase-orders/88/receive
        var grnPayload2 = new PurchaseOrderReceiveRequestDto
        {
            Note = "Đợt 2 nhận nốt",
            Items = new List<PurchaseOrderReceiveItemDto>
            {
                new() { PurchaseOrderItemId = 881, ProductId = 202, ReceivedQuantity = 15m, CostPrice = 160_000m }
            }
        };

        var grnResponse2 = await client.PostAsJsonAsync("/api/purchase-orders/88/receive", grnPayload2);
        Assert.Equal(HttpStatusCode.OK, grnResponse2.StatusCode);
        using var grnBody2 = JsonDocument.Parse(await grnResponse2.Content.ReadAsStringAsync());
        Assert.Equal("COMPLETED", grnBody2.RootElement.GetProperty("status").GetString());
        // Verify capping: accepted only 10 despite requesting 15!
        Assert.Equal(10m, grnBody2.RootElement.GetProperty("lines")[0].GetProperty("acceptedQuantity").GetDecimal());

        // Verify Inventory MAC calculation after Đợt 2:
        // Old: 20 units @ 130k
        // Incoming: 10 units @ 160k
        // Expected New MAC: (20*130k + 10*160k) / 30 = 4,200,000 / 30 = 140,000!
        // Expected New Total Value: 30 * 140k = 4,200,000!
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GarageContext>();
            var inv = await db.Inventories.FirstAsync(i => i.WarehouseId == 102 && i.ProductId == 202);
            Assert.Equal(30m, inv.Quantity);
            Assert.Equal(140_000m, inv.CurrentMacPrice);
            Assert.Equal(4_200_000m, inv.TotalInventoryValue);

            var po = await db.PurchaseOrders.Include(x => x.PurchaseOrderItems).FirstAsync(x => x.Id == 88);
            Assert.Equal("COMPLETED", po.Status);
            Assert.Equal(20m, po.PurchaseOrderItems.First().ReceivedQuantity);
        }

        // 4. Query Goods Receipts via /api/v1/goods-receipts with pagination envelope
        var grnList = await client.GetAsync("/api/v1/goods-receipts?envelope=true&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, grnList.StatusCode);
        using var grnListBody = JsonDocument.Parse(await grnList.Content.ReadAsStringAsync());
        Assert.Equal(2, grnListBody.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(2, grnListBody.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, grnListBody.RootElement.GetProperty("items").GetArrayLength());
        Assert.False(grnList.Headers.Contains("X-Total-Count"));

        // 5. Query GRN detail via /api/v1/goods-receipts/{id}
        var grnDetail = await client.GetAsync($"/api/v1/goods-receipts/{grnId1}");
        Assert.Equal(HttpStatusCode.OK, grnDetail.StatusCode);
        using var grnDetailBody = JsonDocument.Parse(await grnDetail.Content.ReadAsStringAsync());
        Assert.Equal(88, grnDetailBody.RootElement.GetProperty("purchaseOrderId").GetInt64());
        Assert.Equal("PO-00088", grnDetailBody.RootElement.GetProperty("purchaseOrderCode").GetString());
    }

    [Fact]
    public async Task CustomerRole_IsForbiddenFromAllPoAndGrnEndpoints()
    {
        await using var factory = new TestWebApplicationFactory();
        await factory.SeedAsync(db =>
        {
            TestWebApplicationFactory.SeedBasicBranchAndUsers(db);
            return Task.CompletedTask;
        });

        using var client = factory.CreateClient();
        // Customer Token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            TestWebApplicationFactory.CreateJwtToken(100, "Customer"));

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/purchase-orders")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/goods-receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/goods-receipts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/purchase-orders", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/goods-receipts", new { })).StatusCode);
    }
}
