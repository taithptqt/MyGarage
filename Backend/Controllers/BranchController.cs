using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MyGarage.Models;
using AutoMapper;
using MyGarage.Interfaces;
using MyGarage.DTOs.Requests;
using MyGarage.DTOs.Responses;

namespace MyGarage.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchController : ControllerBase
    {
        private readonly IBranchService _branchService;
        private readonly IMapper _mapper;

        public BranchController(IBranchService branchService, IMapper mapper)
        {
            _branchService = branchService;
            _mapper = mapper;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BranchResponseDto>>> GetAll()
        {
            var items = await _branchService.GetAllAsync();
            var activeItems = items.Where(x => x.IsDeleted != true);

            static string RemoveDiacritics(string? value)
            {
                var text = value ?? string.Empty;
                var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
                var chars = normalized
                    .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    .ToArray();
                return new string(chars)
                    .Normalize(System.Text.NormalizationForm.FormC)
                    .Replace('\u0111', 'd')
                    .Replace('\u0110', 'D');
            }

            static string NormalizeBranchNameKey(string? value)
            {
                var normalized = RemoveDiacritics(value)
                    .ToLowerInvariant()
                    .Replace('.', ' ')
                    .Replace('-', ' ')
                    .Replace('_', ' ')
                    .Trim();

                normalized = string.Join(" ", normalized.Split(' ', System.StringSplitOptions.RemoveEmptyEntries));

                return normalized switch
                {
                    "ha noi" or "hanoi" => "ha noi",
                    "da nang" or "danang" => "da nang",
                    "hcm" or "tp hcm" or "tphcm" or "ho chi minh" or "sai gon" or "saigon" => "hcm",
                    _ => normalized
                };
            }

            static int ScoreBranch(Branch branch)
            {
                var score = 0;
                var name = branch.Name ?? string.Empty;
                if (RemoveDiacritics(name) != name) score += 2;
                if (string.Equals(branch.Status, "active", System.StringComparison.OrdinalIgnoreCase)) score += 1;
                return score;
            }

            var dedupedItems = activeItems
                .GroupBy(x => NormalizeBranchNameKey(x.Name))
                .Select(g => g.OrderByDescending(ScoreBranch).ThenBy(x => x.Id).First())
                .OrderBy(x => x.Name)
                .ToList();

            var dtos = _mapper.Map<IEnumerable<BranchResponseDto>>(dedupedItems);
            return Ok(dtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BranchResponseDto>> GetById(long id)
        {
            var item = await _branchService.GetByIdAsync(id);
            if (item == null)
            {
                return NotFound();
            }
            var dto = _mapper.Map<BranchResponseDto>(item);
            return Ok(dto);
        }

        [HttpPost]
        public async Task<ActionResult<BranchResponseDto>> Create([FromBody] BranchCreateDto itemDto)
        {
            var entity = _mapper.Map<Branch>(itemDto);
            var createdItem = await _branchService.AddAsync(entity);
            var responseDto = _mapper.Map<BranchResponseDto>(createdItem);
            return CreatedAtAction(nameof(GetById), new { id = createdItem.Id }, responseDto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<BranchResponseDto>> Update(long id, [FromBody] BranchUpdateDto itemDto)
        {
            var existingItem = await _branchService.GetByIdAsync(id);
            if (existingItem == null)
            {
                return NotFound();
            }

            _mapper.Map(itemDto, existingItem);
            var updatedItem = await _branchService.UpdateAsync(id, existingItem);
            
            if (updatedItem == null)
            {
                return NotFound();
            }
            var responseDto = _mapper.Map<BranchResponseDto>(updatedItem);
            return Ok(responseDto);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(long id)
        {
            var result = await _branchService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
    }
}




