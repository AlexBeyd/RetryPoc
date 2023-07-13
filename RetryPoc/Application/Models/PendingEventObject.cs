namespace RetryPoc.Application.Models;

public class PendingEventObject
{
    public int RelatedRequestId { get; set; }
    public string PendingEventValue { get; set; } = string.Empty;
    public string PendingEventTypeName { get; set; } = string.Empty;
    public DateTimeOffset TimeStamp { get; set; }
    public int OrderInQueue { get; set; }
    public string TopicNameForPublish { get; set; } = string.Empty;
}
