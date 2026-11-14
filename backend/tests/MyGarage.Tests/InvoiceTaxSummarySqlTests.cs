using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;
using MyGarage.Services;
using MyGarage.Application.Payments;
using Xunit;

namespace MyGarage.Tests;

public sealed class InvoiceTaxSummarySqlTests
{
    [LocalSqlFact]
    public async Task Summary_TranslatesNullNegativeAndTopThirtyOnSqlServer()
    {
        var name = "MyGarageTaxTest_" + Guid.NewGuid().ToString("N");
        var master = new SqlConnectionStringBuilder { DataSource = @"(localdb)\MSSQLLocalDB", InitialCatalog = "master", IntegratedSecurity = true, TrustServerCertificate = true };
        var test = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var connection = new SqlConnection(master.ConnectionString);
        await connection.OpenAsync();
        await using (var create = connection.CreateCommand()) { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
        try
        {
            var options = new DbContextOptionsBuilder<GarageContext>().UseSqlServer(test.ConnectionString).Options;
            await using var db = new GarageContext(options, new HttpContextAccessor());
            var script = db.Database.GenerateCreateScript();
            script = Regex.Replace(script, @"CREATE UNIQUE INDEX \[UQ_ServiceOrder_DiscountCategory\][\s\S]*?;", "", RegexOptions.IgnoreCase);
            script = script.Replace("ON DELETE CASCADE", "ON DELETE NO ACTION", StringComparison.OrdinalIgnoreCase);
            script = Regex.Replace(script, @"ALTER TABLE\s+\[[^\]]+\]\s+ADD CONSTRAINT\s+\[[^\]]+\]\s+FOREIGN KEY[\s\S]*?;", "", RegexOptions.IgnoreCase);
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (!string.IsNullOrWhiteSpace(batch)) await db.Database.ExecuteSqlRawAsync(batch);
            var empty = await new InvoiceTaxSummaryReader(db).ReadAsync(default);
            Assert.Equal(0m, empty.TotalTax);
            Assert.Equal(0, empty.TaxedCount);
            Assert.Empty(empty.Items);
            var emptyList = await new InvoiceListPageReader(db).ReadAsync(new InvoiceListCriteria("", "ALL", "ALL", 1, 10), default);
            Assert.Equal(0, emptyList.Total);
            db.Invoices.AddRange(Enumerable.Range(1, 32).Select(i => new Invoice { PayerType = "CUSTOMER", TaxAmount = i == 1 ? -2m : 1m }));
            db.Invoices.AddRange(new Invoice { PayerType = "CUSTOMER", TaxAmount = null }, new Invoice { PayerType = "CUSTOMER", TaxAmount = 0m });
            await db.SaveChangesAsync();
            var result = await new InvoiceTaxSummaryReader(db).ReadAsync(default);
            Assert.Equal(29m, result.TotalTax);
            Assert.Equal(32, result.TaxedCount);
            Assert.Equal(30, result.Items.Count);
            Assert.True(result.Items.Zip(result.Items.Skip(1), (a, b) => a.Id > b.Id).All(x => x));
            db.Invoices.Add(new Invoice { PayerType = "CUSTOMER", CompanyName = "Công ty A", PaymentStatus = "paid", IssuedAt = new DateTime(2026, 1, 1) });
            await db.SaveChangesAsync();
            var reader = new InvoiceListPageReader(db);
            var match = await reader.ReadAsync(new InvoiceListCriteria("công ty", "PAID", "ALL", 1, 1), default);
            Assert.Equal(1, match.Total);
            Assert.Equal(1, match.Summary.IssuedCount);
            Assert.Equal(1, match.Summary.PaidCount);
            var withoutAccent = await reader.ReadAsync(new InvoiceListCriteria("cong ty", "ALL", "ALL", 1, 10), default);
            Assert.Equal(0, withoutAccent.Total);
            db.Invoices.AddRange(
                new Invoice { PayerType = "CUSTOMER", CompanyName = "STATUS-CHECK", PaymentStatus = "PAID" },
                new Invoice { PayerType = "CUSTOMER", CompanyName = "STATUS-CHECK", PaymentStatus = "PAID " },
                new Invoice { PayerType = "CUSTOMER", CompanyName = "STATUS-CHECK", PaymentStatus = " " },
                new Invoice { PayerType = "CUSTOMER", CompanyName = "STATUS-CHECK", PaymentStatus = "UNKNOWN" });
            await db.SaveChangesAsync();
            var statuses = await reader.ReadAsync(new InvoiceListCriteria("status-check", "ALL", "ALL", 1, 10), default);
            Assert.Equal(4, statuses.Total);
            Assert.Equal(1, statuses.Summary.PaidCount);
            Assert.Equal(1, statuses.Summary.UnknownStatusCount);
            var paidOnly = await reader.ReadAsync(new InvoiceListCriteria("status-check", "PAID", "ALL", 1, 10), default);
            Assert.Equal("PAID", Assert.Single(paidOnly.Items).PaymentStatus);
            var unknownOnly = await reader.ReadAsync(new InvoiceListCriteria("status-check", "UNKNOWN", "ALL", 1, 10), default);
            Assert.Equal("UNKNOWN", Assert.Single(unknownOnly.Items).PaymentStatus);
            var selectedId = result.Items[0].Id;
            var method = new PaymentMethod { MethodCode = "CASH", MethodName = "Cash" };
            db.InvoiceDetails.Add(new InvoiceDetail { InvoiceId = selectedId, ItemType = "PART", ItemName = "Part", Unit = "unit", Quantity = 1, FinalAmount = 10 });
            db.Payments.Add(new Payment { InvoiceId = selectedId, Method = method, Type = "PAYMENT", Direction = "INFLOW", Amount = 10 });
            await db.SaveChangesAsync();
            var detail = await new InvoiceViewDetailReader(db).ReadAsync(selectedId, 1, 1, 10, default);
            Assert.NotNull(detail);
            Assert.Equal(selectedId, Assert.Single(detail.Details).InvoiceId);
            Assert.Equal(selectedId, Assert.Single(detail.Payments).InvoiceId);
        }
        finally
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
            await drop.ExecuteNonQueryAsync();
        }
    }
}
