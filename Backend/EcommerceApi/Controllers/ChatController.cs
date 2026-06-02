using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using EcommerceApi.Services;

namespace EcommerceApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IRagsService _ragsService;

        public ChatController(IRagsService ragsService)
        {
            _ragsService = ragsService;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<ActionResult<ChatResponse>> PostChat([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Chat message cannot be empty");
            }

            var result = await _ragsService.ProcessChatQueryAsync(request.Message);
            return Ok(result);
        }
    }
}
