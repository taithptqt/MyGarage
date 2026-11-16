# Customer chatbot RAG access

The existing chatbot API is customer-only: JWT role must be CUSTOMER and sessions must belong to that customer. This change does not grant staff/admin access to the customer chatbot or change their existing admin APIs.

Knowledge-base documents are denied by default. Admins may explicitly publish reviewed customer-facing material by prefixing Topic with `PUBLIC:` or `CUSTOMER:`. Example: `CUSTOMER: warranty policy`. Other topics, including legacy unclassified records, are excluded in the database query before their content can enter retrieval, responses or LLM prompts. Prefix matching may follow the database collation; use uppercase prefixes consistently.

Do not mark internal/private records as PUBLIC or CUSTOMER. No existing knowledge rows are automatically reclassified. Unclassified warranty/membership fallback articles will no longer be returned until reviewed and published. Structured public service prices, branch details and operating hours keep their existing deterministic lookups; personal records remain scoped to the server-resolved customer through existing tools.

Rejected input skips retrieval. Prompts reinforce the customer role and treat retrieved text, tool results and history as data, not executable instructions. These prompts supplement the API authorization, record ownership checks and tool allowlist; they do not replace those checks.

Verification: `dotnet test backend/tests/MyGarage.Tests/MyGarage.Tests.csproj --filter ChatbotRagAccessTests`
