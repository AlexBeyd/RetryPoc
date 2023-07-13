using DotNetCore.CAP;
using RetryPoc.Application.Contracts;
using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;
using RetryPoc.Infrastructure;
using System.Text.Json;

namespace RetryPoc.Application.Services
{
    public class FailSafeService : IFailSafeService
    {
        private readonly IEventsRepository<PendingEventObject> _pendingEventsRepository;
        private readonly ICapPublisher _capPublisher;
        private readonly IConfiguration _configuration;

        //TODO: find a way to work with CAP database to operate the requests
        private static List<RequestMessageObject> _requestMessageObjects = new();

        public FailSafeService(ICapPublisher capPublisher, IConfiguration configuration, IEventsRepository<PendingEventObject> pendingEventsRepository)
        {
            _pendingEventsRepository = pendingEventsRepository;
            _capPublisher = capPublisher;
            _configuration = configuration;
        }

        public async Task<PendingEventObject> AddPendingEvent(RequestMessageChangeStatusObject messageObject)
        {
            var currentMax = await _pendingEventsRepository.IsFound(messageObject.Id) ? (await _pendingEventsRepository.FindAsync(o => o.RelatedRequestId == messageObject.Id))?.Max(o => o.OrderInQueue) : 0;

            var pendingEvent = new PendingEventObject
            {
                RelatedRequestId = messageObject.Id,
                PendingEventValue = JsonSerializer.Serialize(messageObject),
                PendingEventTypeName = messageObject.GetType().AssemblyQualifiedName,
                OrderInQueue = (int)(currentMax != null ? currentMax + 1 : 0),
                TopicNameForPublish = _configuration.GetValue<string>("Cap:RequestsChangeStatusTopicName")
            };

            return await _pendingEventsRepository.AddAsync(pendingEvent);
        }

        public async Task PublishChangeStatus(RequestMessageChangeStatusObject changeStatusObject)
        {
            await _capPublisher.PublishAsync(_configuration.GetValue<string>("Cap:RequestsChangeStatusTopicName"), changeStatusObject);
        }

        public async Task<RequestMessageObject> PublishNewRequest()
        {
            var messageObject = new RequestMessageObject
            {
                Id = _requestMessageObjects.Any() ? _requestMessageObjects.Max(x => x.Id) + 1 : 1,
                Status = RequestStatus.New
            };

            await _capPublisher.PublishAsync(_configuration.GetValue<string>("Cap:NewServiceRequestsTopicName"), messageObject);

            return messageObject;
        }

        public async Task ReplayPendingEventsForRequest(int relatedRequestId)
        {
            //check if there are messages following the creation of the new request 
            var failedEventsOrdered = (await _pendingEventsRepository.FindAsync(o => o.RelatedRequestId == relatedRequestId)).OrderBy(e => e.OrderInQueue).ToArray();

            foreach (var failedEvent in failedEventsOrdered)
            {
                await _capPublisher.PublishDelayAsync(
                   TimeSpan.FromSeconds(failedEvent.OrderInQueue * _configuration.GetValue<int>("Cap:DelayedMessagesIntervalSec")),
                    failedEvent.TopicNameForPublish,
                    JsonSerializer.Deserialize(failedEvent.PendingEventValue, Type.GetType(failedEvent.PendingEventTypeName)));

                await _pendingEventsRepository.DeleteAsync(failedEvent);
            }
        }

        public async Task<int> RequestChangeStatus(int id, RequestStatus status)
        {
            if (!_requestMessageObjects.Any(o => o.Id == id))
            {
                return -1;
            }

            await PublishChangeStatus(new RequestMessageChangeStatusObject { Id = id, NewStatus = status });

            return 0;
        }

        public IEnumerable<RequestMessageObject> ListAllRequests()
        {
            return _requestMessageObjects;
        }

        public async Task<IEnumerable<PendingEventObject>> ListAllPendingEvents()
        {
            return await _pendingEventsRepository.ListAllAsync();
        }

        public async Task ProcessRequest(RequestMessageObject messageObject, bool isProcessingNewRequests)
        {
            var methodName = "AddNewRequest";

            if (isProcessingNewRequests)
            {

                messageObject.MtoaId = Guid.NewGuid();

                if (_requestMessageObjects.All(o => o.Id != messageObject.Id))
                {
                    //this is a new request

                    Console.WriteLine($"{methodName}: Created --> Request Id: {messageObject.Id}, Mtoa Id: {messageObject.MtoaId}");

                    _requestMessageObjects.Add(messageObject);
                }
                else
                {
                    //if the request already exists inside database - then this is a successfull retrying message

                    _requestMessageObjects.ForEach(o =>
                    {
                        if (o.Id == messageObject.Id)
                        {
                            o.MtoaId = messageObject.MtoaId;
                        }
                    });

                    await ReplayPendingEventsForRequest(messageObject.Id);
                }
            }
            else
            {
                //add new request to database anyways, without MTOA ID
                if (_requestMessageObjects.All(o => o.Id != messageObject.Id))
                {
                    _requestMessageObjects.Add(messageObject);
                }

                Console.WriteLine($"{methodName}: The processing is not functional! Timestamp: {DateTime.Now}", "Error");

                throw new InvalidOperationException($"Invalid operation exception was thrown at {DateTime.Now}");
            }
        }

        public async Task ProcessRequestStatusChange(RequestMessageChangeStatusObject messageObject)
        {
            var methodName = "RequestStatusChangeReceiver";

            if (_requestMessageObjects.Any(o => o.Id == messageObject.Id && o.MtoaId == Guid.Empty) || (await _pendingEventsRepository.FindAsync(o => o.RelatedRequestId == messageObject.Id)).Any())
            {
                //Case when the related request don't have MTOA ID or
                //failed events list with related request Id exists
                Console.WriteLine($"{methodName}: Request Id: {messageObject.Id} have no MTOA ID. The status change is impossible. Adding the change request to failed events.");

                await AddPendingEvent(messageObject);
            }

            else if (_requestMessageObjects.Any(o => o.Id == messageObject.Id))
            {
                //case with regular status change event

                Console.WriteLine($"{methodName}: Status Changed --> Request Id:{messageObject.Id}, New Status: {messageObject.NewStatus}");

                _requestMessageObjects.ForEach(o =>
                {
                    if (o.Id == messageObject.Id)
                    {
                        o.Status = messageObject.NewStatus;
                    }
                });
            }
        }
    }
}
