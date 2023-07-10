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
        public void NewMessageRequestReceiver(RequestMessageObject messageObject)
        {
            var methodName = "NewMessageRequestReceiver";

            if (_isProcessingNewRequests)
            {
                messageObject.MtoaId = Guid.NewGuid();

                Debug.WriteLine($"{methodName}: Created --> Request Id: {messageObject.Id}, Mtoa Id: {messageObject.MtoaId}");

                _requestMessageObjects.Add(messageObject);
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
        public void RequestStatusChangeReceiver(RequestMessageChangeStatusObject messageObject)
        {
            var methodName = "RequestStatusChangeReceiver";

            //check for Mtoa Id
            if (_requestMessageObjects.Any(o => o.Id == messageObject.Id && o.MtoaId == Guid.Empty))
            {
                Debug.WriteLine($"{methodName}: Request Id: {messageObject.Id} have no MTOA ID. The status change is impossible.");

                var currentMax = _failedEvents != null && _failedEvents.Any(o => o.ParentRequestId == messageObject.Id) ? _failedEvents.Where(o => o.ParentRequestId == messageObject.Id)?.Max(o => o.OrderInQueue) : 0;

                var failedEvent = new FailedEventObject
                {
                    ParentRequestId = messageObject.Id,
                    FailedEventValue = JsonSerializer.Serialize(messageObject),
                    FailedEventTypeName = messageObject.GetType().Name,
                    TimeStamp = DateTimeOffset.Now,
                    OrderInQueue = (int)(currentMax != null ? currentMax + 1 : 0)
                };

                if (_failedEvents == null)
                {
                    _failedEvents = new();
                }

                _failedEvents.Add(failedEvent);
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

        #endregion Receivers
    }
}
