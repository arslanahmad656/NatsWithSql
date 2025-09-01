using System.Text.Json.Serialization;

namespace Shared;

[JsonSerializable(typeof(Order))]
public partial class OrderJsonContext : JsonSerializerContext
{
}