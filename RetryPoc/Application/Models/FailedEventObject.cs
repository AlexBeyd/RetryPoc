namespace RetryPoc.Application.Models;

public class FailedEventObject
{
    public int ParentRequestId { get; set; }
    public string FailedEventValue { get; set; }
    public string FailedEventTypeName { get; set; }
    public DateTimeOffset TimeStamp { get; set; }
    public int OrderInQueue { get; set; }
    public string TopicNameForPublish { get; set; }
}
