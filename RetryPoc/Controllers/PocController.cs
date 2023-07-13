using DotNetCore.CAP;
using Microsoft.AspNetCore.Mvc;
using RetryPoc.Application.Contracts;
using RetryPoc.Application.Enums;
using RetryPoc.Application.Models;

namespace RetryPoc.Controllers
{
    [ApiController]
    [Route("api")]
    public class PocController : Controller
    {
        private readonly IFailSafeService _failSafeService;
        private static bool _isProcessingNewRequests = true;

        public PocController(IFailSafeService failSafeService)
        {
            _failSafeService = failSafeService;
        }

        [HttpPost("create-new-request")]
        public async Task<IActionResult> NewMessageRequestPublisher()
        {
            return Ok(await _failSafeService.PublishNewRequest());
        }

        [HttpPut("request-change-status/{id}/{status}")]
        public async Task<IActionResult> RequestChangeStatusPublisher(int id, RequestStatus status)
        {
            if (await _failSafeService.RequestChangeStatus(id, status) == -1)
            {
                return NotFound($"Request Id: {id} not found");
            }

            return Accepted(value: $"Request Id:{id} status is changing to {status}");
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
            return Ok(_failSafeService.ListAllRequests());
        }

        [HttpGet("list-pending-events")]
        public async Task<IActionResult> ListAllPendingEvents()
        {
            return Ok(await _failSafeService.ListAllPendingEvents());
        }

        #region Receivers

        [NonAction]
        [CapSubscribe("workload-service-requests-poc")]
        public async Task NewMessageRequestReceiver(RequestMessageObject messageObject)
        {
            await _failSafeService.ProcessRequest(messageObject, _isProcessingNewRequests);
        }

        [NonAction]
        [CapSubscribe("workload-requests-change-status-poc")]
        public async Task RequestStatusChangeReceiver(RequestMessageChangeStatusObject messageObject)
        {
            await _failSafeService.ProcessRequestStatusChange(messageObject);
        }

        #endregion Receivers
    }
}
