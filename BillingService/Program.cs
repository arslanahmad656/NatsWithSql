using BillingService;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BillingDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb")));

builder.Services.AddSingleton<NatsConnection>(sp => new(NatsOpts.Default with
{
    Url = "nats://localhost:4222",
    SerializerRegistry = new NatsJsonContextSerializerRegistry(OrderJsonContext.Default)
}));

builder.Services.AddSingleton<Strings>();

var app = builder.Build();

Console.WriteLine($"Launching the listener.");

_ = Task.Run(async () =>
{
    Console.WriteLine($"Getting the services.");
    var nats = app.Services.GetRequiredService<NatsConnection>();
    var scopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
    var strings = app.Services.GetRequiredService<Strings>();

    Console.WriteLine($"Starting the loop to listen for the messages on subject {strings.OrdersCreatedSubject}");

    await foreach (var msg in nats.SubscribeAsync<Order>(strings.OrdersCreatedSubject))
    {
        Console.WriteLine($"Received an order: {msg.Data}");
        if (msg.Data is null)
        {
            throw new Exception($"Received a null order.");
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        Console.WriteLine($"Existing bills in the db.");
        var bills = db.Bills.AsNoTracking().ToList();
        Console.WriteLine($"Total: {bills.Count}");
        if (bills.Count > 0)
        {
            Console.WriteLine($"Bills:");
            Console.WriteLine(string.Join(Environment.NewLine, bills.Select(b => b.ToString())));
        }

        Console.WriteLine();

        var billToInsert = new Bill(Guid.NewGuid(), msg.Data.CustomerId, msg.Data.Amount, true);
        Console.WriteLine($"Bill to insert: {billToInsert}");

        db.Bills.Add(billToInsert);
        await db.SaveChangesAsync().ConfigureAwait(false);

        Console.WriteLine($"Bill created.");

        Console.WriteLine($"Message processing complete.");
        Console.WriteLine();
        Console.WriteLine();
    }
});

app.Run();
