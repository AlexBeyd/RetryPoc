using RetryPoc.Application.Enums;

namespace RetryPoc.Application.Models;

public class RequestMessageChangeStatusObject
{
    public int Id { get; set; }
    public RequestStatus NewStatus { get; set; }
}
