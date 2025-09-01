using Microsoft.EntityFrameworkCore;
using Shared;

namespace OrderService;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
}
