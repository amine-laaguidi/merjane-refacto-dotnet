using Microsoft.EntityFrameworkCore;
using Refacto.DotNet.Controllers.Database.Context;
using Refacto.DotNet.Controllers.Entities;

namespace Refacto.DotNet.Controllers.Services.Impl
{
    public class ProductService : IProductService
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _dbContext;

        public ProductService(INotificationService notificationService, AppDbContext dbContext)
        {
            _notificationService = notificationService;
            _dbContext = dbContext;
        }

        public void HandleSeasonalProduct(Product p)
        {
            if (DateTime.Now.AddDays(p.LeadTime) > p.SeasonEndDate || p.SeasonStartDate > DateTime.Now)
            {
                _notificationService.SendOutOfStockNotification(p.Name);
                if (p.Available > 0)
                {
                    p.Available = 0;
                    _ = _dbContext.SaveChanges();
                }
            }
            else if (p.Available > 0)
            {
                p.Available -= 1;
                _ = _dbContext.SaveChanges();
            }
            else
            {
                _notificationService.SendDelayNotification(p.LeadTime, p.Name);
            }
        }

        public void HandleNormalProduct(Product p)
        {
            if (p.Available > 0)
            {
                p.Available -= 1;
                _dbContext.SaveChanges();
            }
            else if (p.LeadTime > 0)
            {
                _notificationService.SendDelayNotification(p.LeadTime, p.Name);
            }
        }

        public void HandleExpiredProduct(Product p)
        {
            if (p.ExpiryDate <= DateTime.Now.Date)
            {
                _notificationService.SendExpirationNotification(p.Name, (DateTime)p.ExpiryDate);
                if (p.Available > 0)
                {
                    p.Available = 0;
                    _ = _dbContext.SaveChanges();
                }
            }
            else
            {
                HandleNormalProduct(p);
            }
            

        }
    }
}
