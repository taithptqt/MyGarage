// Mục đích: Cung cấp API kiểm tra điều kiện, redeem, reverse và tra cứu lịch sử sử dụng voucher.
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;
using MyGarage.Interfaces;

namespace MyGarage.Controllers
{
    [Route("api/voucher-redemptions")]
    [ApiController]
    public class VoucherRedemptionController : ControllerBase
    {
        private readonly IVoucherRedemptionService _service;
        private readonly IMapper _mapper;

        public VoucherRedemptionController(IVoucherRedemptionService service, IMapper mapper)
        {
            _service = service;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<VoucherRedemptionResponseDto>>> GetAll([FromQuery] string? status = null)
        {
            var items = await _service.GetAllAsync(status);
            var dtos = _mapper.Map<IEnumerable<VoucherRedemptionResponseDto>>(items);
            return Ok(dtos);
        }

        [HttpGet("{id:long}")]
        public async Task<ActionResult<VoucherRedemptionResponseDto>> GetById(long id)
        {
            var item = await _service.GetDetailedByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<VoucherRedemptionResponseDto>(item));
        }

        [HttpPost("check")]
        public async Task<ActionResult<VoucherValidationResultResponseDto>> Check([FromBody] VoucherRedemptionRequestDto request)
        {
            var result = await _service.CheckEligibilityAsync(request);
            return result.IsEligible ? Ok(result) : BadRequest(result);
        }

        [HttpPost("redeem")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> Redeem([FromBody] VoucherRedemptionRequestDto request)
        {
            var result = await _service.RedeemAsync(request);
            if (!result.Success)
            {
                return BadRequest(result);
            }

            if (result.Redemption == null)
            {
                return Ok(result);
            }

            return CreatedAtAction(nameof(GetById), new { id = result.Redemption.Id }, result);
        }

        [HttpPost("{id:long}/reverse")]
        public async Task<ActionResult<VoucherRedeemResultResponseDto>> Reverse(long id, [FromBody] VoucherRedemptionReverseDto request)
        {
            var result = await _service.ReverseAsync(id, request);
            return result.Success ? Ok(result) : BadRequest(result);
        }
    }
}

