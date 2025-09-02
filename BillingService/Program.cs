using BillingService;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using Shared;

Console.WriteLine($"Initiating billing service.");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<BillingDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("BillingDb")));

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

Console.WriteLine($"Added services to the billing service.");

var app = builder.Build();

Console.WriteLine($"Launching the listener.");

Console.WriteLine($"Applying migrations for the billing service.");
using (var scope = app.Services.CreateScope())
{
    var maxTries = 100;
    var interval = TimeSpan.FromSeconds(3);
    while (true)
    {
        try
        {
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await db.Database.MigrateAsync().ConfigureAwait(false);
            Console.WriteLine($"Migrations completed for the billing service.");
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

app.MapGet("/health", () =>
{
    var health = new
    {
        Healthy = true,
        Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
    };

    return Results.Ok(health);
});

Console.WriteLine($"Launching the NATS subscriber for the billing service.");

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

Console.WriteLine($"Everything set. Launching the app.");

app.Run();

Console.WriteLine($"App has been launched.");