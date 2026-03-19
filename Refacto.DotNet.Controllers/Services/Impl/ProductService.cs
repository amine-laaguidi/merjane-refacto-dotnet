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

        public void NotifyDelay(int leadTime, Product p)
        {
            p.LeadTime = leadTime;
            _ = _dbContext.SaveChanges();
            _notificationService.SendDelayNotification(leadTime, p.Name);
        }

        public void HandleSeasonalProduct(Product p)
        {
            if (DateTime.Now.AddDays(p.LeadTime) > p.SeasonEndDate || p.SeasonStartDate > DateTime.Now)
            {
                _notificationService.SendOutOfStockNotification(p.Name);
                p.Available = 0;
                _ = _dbContext.SaveChanges();
            }
            else
            {
                p.Available -= 1;
                _ = _dbContext.SaveChanges();
                if (p.Available == 0)
                {
                    _notificationService.SendDelayNotification(p.LeadTime, p.Name);
                }
            }
        }

        public void HandleNormalProduct(Product p)
        {
            p.Available -= 1;
            _ = _dbContext.SaveChanges();
            if (p.Available == 0)
            {
                _notificationService.SendDelayNotification(p.LeadTime, p.Name);
            }
        }

        public void HandleExpiredProduct(Product p)
        {
            if (p.Available > 0 && p.ExpiryDate > DateTime.Now)
            {
                p.Available -= 1;
                _ = _dbContext.SaveChanges();
            }
            else
            {
                _notificationService.SendExpirationNotification(p.Name, (DateTime)p.ExpiryDate);
                p.Available = 0;
                _ = _dbContext.SaveChanges();
            }
        }
    }
}
