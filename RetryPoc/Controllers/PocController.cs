﻿using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;
using System.Text.Json;

namespace RetryPoc.Controllers
{
    [ApiController]
    [Route("api")]
    public class PocController : Controller
    {
        private const int DelayIntervalSec = 10;
        private readonly ICapPublisher _capPublisher;
        private readonly IConfiguration _configuration;
        private static bool _isProcessingNewRequests = true;

        private static List<RequestMessageObject> _requestMessageObjects = new();
        private static List<PendingEventObject> _pendingdEvents = new();

        public PocController(ICapPublisher capPublisher, IConfiguration configuration)
        {
            _capPublisher = capPublisher;
            _configuration = configuration;
        }

        [HttpPost("create-new-request")]
        public async Task<IActionResult> NewMessageRequestPublisher()
        {
            var nextId = _requestMessageObjects.Any() ? _requestMessageObjects.Max(x => x.Id) + 1 : 0;

            var messageObject = new RequestMessageObject
            {
                Id = nextId,
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

            return Accepted(value: "New Request processor is now down...");
        }

        [HttpGet("list-requests")]
        public IActionResult ListAllRequests()
        {
            return Ok(_requestMessageObjects);
        }

        [HttpGet("list-pending-events")]
        public IActionResult ListAllPendingEvents()
        {
            return Ok(_pendingdEvents);
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

                    //check if there are messages following the creation of the new request 
                    var failedEventsOrdered = _pendingdEvents.Where(e => e.RelatedRequestId == messageObject.Id).OrderBy(e => e.OrderInQueue).ToArray();

                    foreach (var failedEvent in failedEventsOrdered)
                    {
                        await _capPublisher.PublishDelayAsync(
                           TimeSpan.FromSeconds(failedEvent.OrderInQueue * DelayIntervalSec),
                            failedEvent.TopicNameForPublish,
                            JsonSerializer.Deserialize(failedEvent.PendingEventValue, Type.GetType(failedEvent.PendingEventTypeName)));

                        _pendingdEvents.Remove(failedEvent);
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

                Console.WriteLine($"{methodName}: The processing is not functional! Timestamp: {DateTime.Now}", "Error");

                throw new InvalidOperationException($"Invalid operation exception was thrown at {DateTime.Now}");
            }
        }

        [NonAction]
        [CapSubscribe("workload-requests-change-status-poc")]
        public async Task RequestStatusChangeReceiver(RequestMessageChangeStatusObject messageObject)
        {
            var methodName = "RequestStatusChangeReceiver";

            if (_requestMessageObjects.Any(o => o.Id == messageObject.Id && o.MtoaId == Guid.Empty) || _pendingdEvents.Any(e => e.RelatedRequestId == messageObject.Id))
            {
                //Case when the related request don't have MTOA ID or
                //failed events list with related request Id exists
                Console.WriteLine($"{methodName}: Request Id: {messageObject.Id} have no MTOA ID. The status change is impossible. Adding the change request to failed events.");

                var currentMax = _pendingdEvents != null && _pendingdEvents.Any(o => o.RelatedRequestId == messageObject.Id) ? _pendingdEvents.Where(o => o.RelatedRequestId == messageObject.Id)?.Max(o => o.OrderInQueue) : 0;

                var failedEvent = new PendingEventObject
                {
                    RelatedRequestId = messageObject.Id,
                    PendingEventValue = JsonSerializer.Serialize(messageObject),
                    PendingEventTypeName = messageObject.GetType().AssemblyQualifiedName,
                    TimeStamp = DateTimeOffset.Now,
                    OrderInQueue = (int)(currentMax != null ? currentMax + 1 : 0),
                    TopicNameForPublish = _configuration.GetValue<string>("Cap:RequestsChangeStatisTopicName")
                };

                _pendingdEvents ??= new();
                _pendingdEvents.Add(failedEvent);
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

        #endregion Receivers
    }
}
