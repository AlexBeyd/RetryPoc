using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;
using System.Diagnostics;

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
        private static List<RequestMessageObject> _requestMessageObjects = new List<RequestMessageObject>();

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

        [HttpGet("list")]
        public IActionResult ListAllRequests()
        {
            return Ok(_requestMessageObjects);
        }

        #region Receivers

        [NonAction]
        [CapSubscribe("workload-service-requests-poc")]
        public void NewMessageRequestReceiver(RequestMessageObject messageObject)
        {
            if (_isProcessingNewRequests)
            {
                messageObject.MtoaId = Guid.NewGuid();

                Debug.WriteLine($"NewMessageRequestReceiver: Created --> Request Id: {messageObject.Id}, Mtoa Id: {messageObject.MtoaId}");

                _requestMessageObjects.Add(messageObject);
            }
            else
            {
                //add new request to database anyways, without MTOA ID
                _requestMessageObjects.Add(messageObject);

                Debug.WriteLine($"NewMessageRequestReceiver: The processing is not functional! Timestamp: {DateTime.Now}", "Error");

                throw new InvalidOperationException($"Invalid operation exception was thrown at {DateTime.Now}");
            }
        }

        [NonAction]
        [CapSubscribe("workload-requests-change-status-poc")]
        public void RequestStatusChangeReceiver(RequestMessageChangeStatusObject messageObject)
        {
            Debug.WriteLine($"RequestStatusChangeReceiver: Status Changed --> Request Id:{messageObject.Id}, New Status: {messageObject.NewStatus}");

            _requestMessageObjects.ForEach(o =>
            {
                if (o.Id == messageObject.Id)
                {
                    o.Status = messageObject.NewStatus;
                }
            });
        }

        #endregion Receivers
    }
}
