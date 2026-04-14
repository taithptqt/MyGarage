using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotAnswerComposer : IChatbotAnswerComposer
{
    private readonly IGarageContext _context;

    public ChatbotAnswerComposer(IGarageContext context)
    {
        _context = context;
    }

    public async Task<string> ComposeAsync(
        ChatbotRetrievalResult retrieval,
        long? branchId,
        CancellationToken cancellationToken = default)
    {
        if (retrieval.QueryType == ChatbotKnowledgeQueryType.SERVICE_BASE_PRICE &&
            !retrieval.IsGrounded &&
            !retrieval.IsAmbiguous)
        {
            return "Mình chưa xác định được dịch vụ cần báo giá. Bạn cho mình tên dịch vụ cụ thể (ví dụ: thay dầu, thay lọc gió, kiểm tra phanh) nhé.";
        }

        if (retrieval.IsGrounded && !string.IsNullOrWhiteSpace(retrieval.Answer))
        {
            return retrieval.Answer;
        }

        if (retrieval.IsAmbiguous && !string.IsNullOrWhiteSpace(retrieval.ClarificationPrompt))
        {
            return retrieval.ClarificationPrompt;
        }

        var supportBranch = await ResolveSupportBranchAsync(branchId, cancellationToken);
        var hotlinePart = string.IsNullOrWhiteSpace(supportBranch?.Hotline)
            ? "hotline chi nhánh"
            : $"hotline {supportBranch.Hotline}";
        var branchPart = string.IsNullOrWhiteSpace(supportBranch?.Name)
            ? "chi nhánh gần nhất"
            : $"chi nhánh \"{supportBranch.Name}\"";

        return $"Mình chưa tìm thấy thông tin phù hợp trong dữ liệu hiện có. Vui lòng liên hệ {hotlinePart} hoặc ghé {branchPart} để được hỗ trợ trực tiếp.";
    }

    private async Task<SupportBranch?> ResolveSupportBranchAsync(long? branchId, CancellationToken cancellationToken)
    {
        if (branchId.HasValue)
        {
            var branch = await _context.Branches
                .AsNoTracking()
                .Where(x => x.Id == branchId.Value && x.IsDeleted != true)
                .Select(x => new SupportBranch
                {
                    Name = x.Name,
                    Hotline = x.Hotline
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (branch is not null)
            {
                return branch;
            }
        }

        return await _context.Branches
            .AsNoTracking()
            .Where(x => x.IsDeleted != true && !string.IsNullOrWhiteSpace(x.Hotline))
            .OrderBy(x => x.Id)
            .Select(x => new SupportBranch
            {
                Name = x.Name,
                Hotline = x.Hotline
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    private sealed class SupportBranch
    {
        public string? Name { get; set; }

        public string? Hotline { get; set; }
    }
}
