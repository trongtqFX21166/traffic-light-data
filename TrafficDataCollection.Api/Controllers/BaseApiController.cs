using Microsoft.AspNetCore.Mvc;
using TrafficDataCollection.Api.Models;

namespace TrafficDataCollection.Api.Controllers
{
    public class BaseApiController : ControllerBase
    {
        protected IActionResult SuccessResponse<T>(T data, string message = "success") where T : class
        {
            return Ok(IOTHubResponse<T>.Success(data, message));
        }

        protected IActionResult ErrorResponse(int statusCode, int errorCode, string message)
        {
            var response = IOTHubResponse<object>.Error(errorCode, message);
            return StatusCode(statusCode, response);
        }

        protected IActionResult NotFoundResponse(string message = "Resource not found")
        {
            return NotFound(IOTHubResponse<object>.Error(404, message));
        }

        protected IActionResult BadRequestResponse(string message = "Invalid request")
        {
            return BadRequest(IOTHubResponse<object>.Error(400, message));
        }

        protected IActionResult InternalErrorResponse(string message = "Internal server error")
        {
            return StatusCode(500, IOTHubResponse<object>.InternalError(message));
        }

        protected IActionResult SearchResponse<T>(IEnumerable<T> data, long totalCount) where T : class
        {
            var searchResponse = IOTHubSearchResponse<T>.Create(data.ToList(), totalCount);
            return Ok(IOTHubResponse<IOTHubSearchResponse<T>>.Success(searchResponse));
        }
    }
}