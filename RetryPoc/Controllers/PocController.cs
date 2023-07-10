using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;
using System.Diagnostics;
using System.Text.Json;

namespace RetryPoc.Controllers
{
    [ApiController]
    [Route("api")]
    public class PocController : Controller
    {
        private readonly ICapPublisher _capPublisher;
        private readonly IConfiguration _configuration;
        private static bool _isProcessingNewRequests = true;
        private static int RequestId = 1;
        private static List<RequestMessageObject> _requestMessageObjects = new();
        private static List<FailedEventObject> _failedEvents = new();
        private const int DelayIntervalSec = 10;

        public PocController(ICapPublisher capPublisher, IConfiguration configuration)
        {
            _capPublisher = capPublisher;
            _configuration = configuration;
        }

        [HttpPost("create-new-request")]
        public async Task<IActionResult> NewMessageRequestPublisher()
        {
            var messageObject = new RequestMessageObject
            {
                Id = RequestId++,
                Status = RequestStatus.New
            };

            await _capPublisher.PublishAsync(_configuration.GetValue<string>("Cap:NewServiceRequestsTopicName"), messageObject);

            return Ok(messageObject);
        }

        [HttpPut("request-change-status/{id}/{status}")]
        public async Task<IActionResult> RequestChangeStatusPublisher(int id, RequestStatus status)
        {
            if (!_requestMessageObjects.Any(o => o.Id == id))
            {
                return NotFound($"Request Id: {id} not found");
            }

            var message = $"Request Id:{id} status is changing to {status}";

            await _capPublisher.PublishAsync(_configuration.GetValue<string>("Cap:RequestsChangeStatisTopicName"), new RequestMessageChangeStatusObject { Id = id, NewStatus = status });

            return Accepted(value: message);
        }

        [HttpPost("new-request-processor-set-state")]
        public IActionResult SetIfRequestProcessingFunctional(bool isFunctional)
        {
            _isProcessingNewRequests = isFunctional;
            if (isFunctional)
            {
                return Accepted(value: "New Request processor is functional again!");
            }

            return Accepted(value: "New Request processor is down...");
        }

        [HttpGet("list-requests")]
        public IActionResult ListAllRequests()
        {
            return Ok(_requestMessageObjects);
        }

        [HttpGet("list-failed-events")]
        public IActionResult ListAllFailedEvents()
        {
            return Ok(_failedEvents);
        }

        #region Receivers

        [NonAction]
        [CapSubscribe("workload-service-requests-poc")]
        public async Task NewMessageRequestReceiver(RequestMessageObject messageObject)
        {
            var methodName = "NewMessageRequestReceiver";

            if (_isProcessingNewRequests)
            {
                messageObject.MtoaId = Guid.NewGuid();

                if (_requestMessageObjects.All(o => o.Id != messageObject.Id))
                {
                    //this is a new request

                    Debug.WriteLine($"{methodName}: Created --> Request Id: {messageObject.Id}, Mtoa Id: {messageObject.MtoaId}");

                    _requestMessageObjects.Add(messageObject);
                }
                else
                {
                    //if the request already exists inside database - then this is a successfull retrying message

                    _requestMessageObjects.ForEach(o =>
                    {
                        if (o.Id == messageObject.Id)
                        {
                            o.MtoaId = Guid.NewGuid();
                        }
                    });

                    //check if there are messages following the creation of the new request 
                    foreach (var failedEvent in _failedEvents.Where(e => e.ParentRequestId == messageObject.Id).OrderBy(e => e.OrderInQueue))
                    {
                        await _capPublisher.PublishDelayAsync(
                           TimeSpan.FromSeconds(failedEvent.OrderInQueue * DelayIntervalSec),
                            failedEvent.TopicNameForPublish,
                            JsonSerializer.Deserialize(failedEvent.FailedEventValue, Type.GetType(failedEvent.FailedEventTypeName)));
                    }
                }
            }
            else
            {
                //add new request to database anyways, without MTOA ID
                if (_requestMessageObjects.All(o => o.Id != messageObject.Id))
                {
                    _requestMessageObjects.Add(messageObject);
                }

                Debug.WriteLine($"{methodName}: The processing is not functional! Timestamp: {DateTime.Now}", "Error");

                throw new InvalidOperationException($"Invalid operation exception was thrown at {DateTime.Now}");
            }
        }

        [NonAction]
        [CapSubscribe("workload-requests-change-status-poc")]
        public async Task RequestStatusChangeReceiver(RequestMessageChangeStatusObject messageObject)
        {
            var methodName = "RequestStatusChangeReceiver";

            //check for Mtoa Id
            if (_requestMessageObjects.Any(o => o.Id == messageObject.Id && o.MtoaId == Guid.Empty))
            {
                Debug.WriteLine($"{methodName}: Request Id: {messageObject.Id} have no MTOA ID. The status change is impossible.");

                //////////TODO: duplicated code with next else
                var currentMax = _failedEvents != null && _failedEvents.Any(o => o.ParentRequestId == messageObject.Id) ? _failedEvents.Where(o => o.ParentRequestId == messageObject.Id)?.Max(o => o.OrderInQueue) : 0;

                var failedEvent = new FailedEventObject
                {
                    ParentRequestId = messageObject.Id,
                    FailedEventValue = JsonSerializer.Serialize(messageObject),
                    FailedEventTypeName = messageObject.GetType().Name,
                    TimeStamp = DateTimeOffset.Now,
                    OrderInQueue = (int)(currentMax != null ? currentMax + 1 : 0),
                    TopicNameForPublish = _configuration.GetValue<string>("Cap:RequestsChangeStatisTopicName")
                };
                ////////////////////////////

                if (_failedEvents == null)
                {
                    _failedEvents = new();
                }

                _failedEvents.Add(failedEvent);
            }
            else
            {
                if (_failedEvents.Any(e => e.ParentRequestId == messageObject.Id))
                {
                    var currentMax = _failedEvents != null && _failedEvents.Any(o => o.ParentRequestId == messageObject.Id) ? _failedEvents.Where(o => o.ParentRequestId == messageObject.Id)?.Max(o => o.OrderInQueue) : 0;

                    var newFailedEvent = new FailedEventObject
                    {
                        ParentRequestId = messageObject.Id,
                        FailedEventValue = JsonSerializer.Serialize(messageObject),
                        FailedEventTypeName = messageObject.GetType().Name,
                        TimeStamp = DateTimeOffset.Now,
                        OrderInQueue = (int)(currentMax != null ? currentMax + 1 : 0),
                        TopicNameForPublish = _configuration.GetValue<string>("Cap:RequestsChangeStatisTopicName")
                    };
                    _failedEvents.Add(newFailedEvent);

                    //check if there are messages following the creation of the new request 
                    foreach (var failedEvent in _failedEvents.Where(e => e.ParentRequestId == messageObject.Id).OrderBy(e => e.OrderInQueue))
                    {
                        await _capPublisher.PublishDelayAsync(
                           TimeSpan.FromSeconds(failedEvent.OrderInQueue * DelayIntervalSec),
                            failedEvent.TopicNameForPublish,
                            JsonSerializer.Deserialize(failedEvent.FailedEventValue, Type.GetType(failedEvent.FailedEventTypeName)));
                    }
                }
                else
                {
                    Debug.WriteLine($"{methodName}: Status Changed --> Request Id:{messageObject.Id}, New Status: {messageObject.NewStatus}");

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

        #endregion Receivers
    }
}
