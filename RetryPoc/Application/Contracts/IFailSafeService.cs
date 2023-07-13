using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;

namespace RetryPoc.Application.Contracts;

public interface IFailSafeService
{
    Task<RequestMessageObject> PublishNewRequest();
    Task PublishChangeStatus(RequestMessageChangeStatusObject changeStatusObject);
    Task<PendingEventObject> AddPendingEvent(RequestMessageChangeStatusObject messageObject);
    Task ReplayPendingEventsForRequest(int relatedRequestId);
    Task<int> RequestChangeStatus(int id, RequestStatus status);
    IEnumerable<RequestMessageObject> ListAllRequests();
    Task<IEnumerable<PendingEventObject>> ListAllPendingEvents();
    Task ProcessRequest(RequestMessageObject messageObject, bool isProcessingNewRequests);
    Task ProcessRequestStatusChange(RequestMessageChangeStatusObject messageObject);
}
