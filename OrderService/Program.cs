using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using OrderService;
using Shared;

internal class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine($"Inititating the order service.");
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<OrderDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb")));

        builder.Services.AddSingleton<NatsConnection>(sp =>
        {
            var natshost = builder.Configuration["NatsHost"];
            Console.WriteLine($"NATS host: {natshost}");
            if (natshost is null)
            {
                throw new Exception("NATS host was not found");
            }

            var natsUrl = $"nats://{natshost}:4222";
            Console.WriteLine($"NATS URL: {natsUrl}");

            return new(NatsOpts.Default with
            {
                Url = natsUrl,
                SerializerRegistry = new NatsJsonContextSerializerRegistry(OrderJsonContext.Default)
            });
        });

        builder.Services.AddSingleton<Strings>();

        Console.WriteLine($"Added services for the order service.");
        Console.WriteLine($"Building the app and its pipeline.");
        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred while handling the request: {context.Request.Path}. {ex.Message}");

                context.Response.StatusCode = 500;
                await context.Response.WriteAsJsonAsync(new
                {
                    ex.Message,
                });
            }
        });

        app.MapGet("/orders", async (OrderDbContext db) =>
        {
            var orders = await db.Orders.AsNoTracking().ToListAsync().ConfigureAwait(false);
            return Results.Ok(orders);
        });

        app.MapPost("/orders", async (Order order, OrderDbContext db, NatsConnection nats, Strings strings) =>
        {
            Console.WriteLine($"Inserting orders.");
            Console.WriteLine($"Existing orders: ");
            var orders = db.Orders.AsNoTracking().ToList();
            Console.WriteLine($"Total: {orders.Count}");
            if (orders.Count > 0)
            {
                Console.WriteLine($"Orders:");
                Console.WriteLine(string.Join(Environment.NewLine, orders.Select(o => o.ToString())));
            }

            Console.WriteLine();
            Console.WriteLine($"Order to insert: {order}");

            db.Orders.Add(order);
            await db.SaveChangesAsync().ConfigureAwait(false);

            Console.WriteLine($"Order has been inserted.");
            Console.WriteLine($"Publishing to NATS.");

            await nats.PublishAsync(strings.OrdersCreatedSubject, order);

            Console.WriteLine($"Published.");

            return Results.Created($"/{order.Id}", order);
        });

        app.MapGet("/health", () =>
        {
            var health = new
            {
                Healthy = true,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            };

            return Results.Ok(health);
        });

        Console.WriteLine($"Built the app and its pipeline.");
        Console.WriteLine($"Running the migrations for the order service.");
        using (var scope = app.Services.CreateScope())
        {
            var maxTries = 10;
            var interval = TimeSpan.FromSeconds(3);
            while (true)
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                    await db.Database.MigrateAsync().ConfigureAwait(false);
                    Console.WriteLine($"Migrations completed for the order service.");
                    break;
                }
                catch (Exception ex)
                {
                    Console.Write($"Error occurred while migrating: {ex.Message}");
                    maxTries--;
                    await Task.Delay(interval).ConfigureAwait(false);
                    if (maxTries <= 0)
                    {
                        throw new Exception($"Could not perform migrations after many tries.");
                    }
                }
            }
        }

        app.Run();
    }
}