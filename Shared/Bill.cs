namespace Shared;

public record Bill(Guid Id, string CustomerId, decimal Amount, bool Success);
