using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using OrderService;
using Shared;

internal class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<OrderDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb")));

        builder.Services.AddSingleton<NatsConnection>(sp => new(NatsOpts.Default with
        {
            Url = "nats://localhost:4222",
            SerializerRegistry = new NatsJsonContextSerializerRegistry(OrderJsonContext.Default)
        }));

        builder.Services.AddSingleton<Strings>();

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

        app.Run();
    }
}