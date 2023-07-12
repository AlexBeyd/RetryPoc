namespace RetryPoc.Application.Models;

public class PendingEventObject
{
    public int RelatedRequestId { get; set; }
    public string PendingEventValue { get; set; }
    public string PendingEventTypeName { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
    public int OrderInQueue { get; set; }
    public string TopicNameForPublish { get; set; }
}
