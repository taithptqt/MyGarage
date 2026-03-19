using Microsoft.EntityFrameworkCore;
using MyGarage.Data;
using MyGarage.Models;

namespace MyGarage.Scripts
{
    public class DataSeeder
    {
        public static async Task SeedCatalogsAsync(GarageContext context)
        {
            if (await context.ServiceCategories.AnyAsync()) return;

            // Seed Service Categories
            var catMaintenance = new ServiceCategory { Code = "MAINT", Name = "Bảo dưỡng", Description = "Các dịch vụ bảo trì định kỳ" };
            var catRepair = new ServiceCategory { Code = "REPAIR", Name = "Sửa chữa", Description = "Sửa chữa hỏng hóc hệ thống" };
            var catTires = new ServiceCategory { Code = "TIRES", Name = "Lốp & Phanh", Description = "Thay thế lốp và hệ thống phanh" };

            context.ServiceCategories.AddRange(catMaintenance, catRepair, catTires);
            await context.SaveChangesAsync();

            // Seed Standard Services
            context.StandardServices.AddRange(
                new StandardService { 
                    CategoryId = catMaintenance.Id, 
                    Code = "MAINT-01", 
                    Name = "Thay nhớt & Lọc nhớt", 
                    BasePrice = 150000, 
                    StandardMinutes = 45 
                },
                new StandardService { 
                    CategoryId = catRepair.Id, 
                    Code = "REP-01", 
                    Name = "Vệ sinh kim phun", 
                    BasePrice = 350000, 
                    StandardMinutes = 60 
                },
                new StandardService { 
                    CategoryId = catTires.Id, 
                    Code = "TIRE-01", 
                    Name = "Cân mâm bấm chì", 
                    BasePrice = 200000, 
                    StandardMinutes = 30 
                }
            );

            // Seed Parts Categories
            var pCatOil = new Category { CategoryName = "Dầu nhớt", Description = "Các loại dầu bôi trơn" };
            var pCatTire = new Category { CategoryName = "Lốp xe", Description = "Lốp các loại xe thông dụng" };
            
            context.Categories.AddRange(pCatOil, pCatTire);
            await context.SaveChangesAsync();

            // Seed Products (Parts)
            context.Products.AddRange(
                new Product { 
                    CategoryId = pCatOil.Id, 
                    Sku = "OIL-CASTROL-4L", 
                    Name = "Dầu Castrol Magnatec 4L", 
                    RetailPrice = 850000, 
                    Unit = "Bình"
                },
                new Product { 
                    CategoryId = pCatTire.Id, 
                    Sku = "TIRE-MICHELIN-185", 
                    Name = "Lốp Michelin 185/65R15", 
                    RetailPrice = 1650000, 
                    Unit = "Cái"
                }
            );

            await context.SaveChangesAsync();
        }
    }
}
