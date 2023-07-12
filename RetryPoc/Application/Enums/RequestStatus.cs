namespace RetryPoc.Application.Enums;

public enum RequestStatus
{
    New = 0,
    Started = 1,
    Pending = 2,
    MoreInformationNeeded = 3,
    Completed = 4,
    OnHold = 5
}
