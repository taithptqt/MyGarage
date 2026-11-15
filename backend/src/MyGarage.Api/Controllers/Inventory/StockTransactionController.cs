using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Application.Common.Pagination;
using MyGarage.Extensions;

namespace MyGarage.Controllers
{
    [Route("api/v1/stock-transactions")]
    [ApiController]
    [Authorize(Roles = "Staff")]
    public class StockTransactionController : ControllerBase
    {
        private readonly IStockTransactionService _stockTransactionService;
        private readonly IStockAlertSyncService _stockAlertSyncService;

        public StockTransactionController(
            IStockTransactionService stockTransactionService,
            IStockAlertSyncService stockAlertSyncService)
        {
            _stockTransactionService = stockTransactionService;
            _stockAlertSyncService = stockAlertSyncService;
        }

        [HttpGet]
        public async Task<ActionResult<PagedEnvelope<StockTransactionResponseDto>>> GetAll(
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null)
        {
            var (normalizedPage, normalizedSize) = PaginationExtensions.NormalizePagination(page, pageSize);
            var (paged, total) = await _stockTransactionService.GetPageByIdAsync(normalizedPage, normalizedSize);
            var dtos = _stockTransactionService.MapToDtos(paged);
            return Ok(PagedEnvelope<StockTransactionResponseDto>.Create(dtos, total, normalizedPage, normalizedSize));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<StockTransactionResponseDto>> GetById(long id)
        {
            var dto = await _stockTransactionService.GetDtoByIdAsync(id);
            if (dto == null)
            {
                return NotFound();
            }
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<StockTransactionResponseDto>> Create([FromBody] StockTransactionCreateDto itemDto)
        {
            var responseDto = await _stockTransactionService.CreateDtoAsync(itemDto);
            await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StockTransactionResponseDto>> Update(long id, [FromBody] StockTransactionUpdateDto itemDto)
        {
            var responseDto = await _stockTransactionService.UpdateDtoAsync(id, itemDto);
            if (responseDto == null)
            {
                return NotFound();
            }
            await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _stockTransactionService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            await _stockAlertSyncService.RebuildStockAlertsAsync(this.GetCurrentUserId());
            return NoContent();
        }
    }
}






