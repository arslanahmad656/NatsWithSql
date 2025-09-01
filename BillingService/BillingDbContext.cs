using Microsoft.EntityFrameworkCore;
using Shared;

namespace BillingService;

public class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Bill> Bills => Set<Bill>();
}
