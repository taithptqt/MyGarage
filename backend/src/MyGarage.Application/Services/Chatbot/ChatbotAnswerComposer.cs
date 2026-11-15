using System.Threading;
using System.Threading.Tasks;
using MyGarage.Application.Interfaces.Stores.Chatbot;
using MyGarage.Enums;
using MyGarage.Interfaces;

namespace MyGarage.Services;

public class ChatbotAnswerComposer : IChatbotAnswerComposer
{
    private readonly IChatbotStore _store;

    public ChatbotAnswerComposer(IChatbotStore store)
    {
        _store = store;
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

        var supportBranch = await _store.ResolveSupportBranchAsync(branchId, cancellationToken);
        var hotlinePart = string.IsNullOrWhiteSpace(supportBranch?.Hotline)
            ? "hotline chi nhánh"
            : $"hotline {supportBranch.Hotline}";
        var branchPart = string.IsNullOrWhiteSpace(supportBranch?.Name)
            ? "chi nhánh gần nhất"
            : $"chi nhánh \"{supportBranch.Name}\"";

        return $"Mình chưa tìm thấy thông tin phù hợp trong dữ liệu hiện có. Vui lòng liên hệ {hotlinePart} hoặc ghé {branchPart} để được hỗ trợ trực tiếp.";
    }
}
