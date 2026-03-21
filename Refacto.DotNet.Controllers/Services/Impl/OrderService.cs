using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Enums.ProductType;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class OrderService : IOrderService
    {
        private readonly IProductService _productService;
        private readonly AppDbContext _dbContext;

        public OrderService(IProductService productService, AppDbContext dbContext)
        {
            _productService = productService;
            _dbContext = dbContext;
        }

        public void ProcessOrder(long orderId)
        {
            try {
                        
                Entities.Order? order = _dbContext.Orders
                    .Include(o => o.Items)
                    .SingleOrDefault(o => o.Id == orderId);
                ICollection<Entities.Product>? products = order.Items;

                foreach (Entities.Product p in products)
                {
                    if (p.Type == ProductType.NORMAL)
                    {
                        _productService.HandleNormalProduct(p);
                    }
                    else if (p.Type == ProductType.SEASONAL)
                    {
                        _productService.HandleSeasonalProduct(p);
                    }
                    else if (p.Type == ProductType.EXPIRABLE)
                    {
                        if (p.Available > 0 && p.ExpiryDate > DateTime.Now.Date)
                        {
                            p.Available -= 1;
                            _ = _dbContext.SaveChanges();
                        }
                        else
                        {
                            _productService.HandleExpiredProduct(p);
                        }
                    }
                }
        
            } catch (Exception ex) {
                 Console.Error.WriteLine($"[OrderService] Exception: {ex}"); throw; 
            }
        }

    }
}
