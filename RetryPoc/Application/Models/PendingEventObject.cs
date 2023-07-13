using System.ComponentModel.DataAnnotations.Schema;

namespace RetryPoc.Application.Models;

public class PendingEventObject
{
    public int Id { get; set; }
    public int RelatedRequestId { get; set; }
    public string PendingEventValue { get; set; } = string.Empty;
    public string PendingEventTypeName { get; set; } = string.Empty;
    public int OrderInQueue { get; set; }
    public string TopicNameForPublish { get; set; } = string.Empty;
}
