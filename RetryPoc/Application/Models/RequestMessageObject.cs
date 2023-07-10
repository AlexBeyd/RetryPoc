using RetryPoc.Application.Enums;
using System.Text.Json.Serialization;

namespace RetryPoc.Application.Models;

public class RequestMessageObject
{
    public int Id { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public Guid MtoaId { get; set; }
    public RequestStatus Status { get; set; }
}
