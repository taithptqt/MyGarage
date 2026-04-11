# Chatbot AI Provider Setup

## Scope

This guide describes how to configure provider-agnostic LLM routing for chatbot orchestration using:

- database table: `AI_ModelConfigs`
- app configuration: `LlmGateway` section
- provider abstraction: `IChatbotLlmProviderClient`

## Configuration sources

`ChatbotLlmGateway` resolves runtime model/provider settings from two layers:

1. `AI_ModelConfigs` (active model rows, includes model name and optional API key).
2. `LlmGateway:Providers` (provider endpoint and optional fallback API key).

Selection behavior:

- `PreferVisionModel=true` tries active config where `Provider` contains `VISION`.
- otherwise tries active config where `Provider` does not contain `VISION`.
- falls back to first active config when specialized match is unavailable.

## `AI_ModelConfigs` contract

Current model columns used by gateway:

- `Id`
- `Provider`
- `ApiKey` (optional if fallback key exists in app config)
- `ModelName`
- `MaxTokens`
- `Temperature`
- `IsActive`

Expected provider naming examples:

- `OPENAI`
- `AZURE_OPENAI`
- `AZURE_OPENAI_VISION`
- `GEMINI_OPENAI`

Note:

- `OpenAiCompatibleChatbotLlmProviderClient` currently handles providers containing `OPENAI` or `GEMINI` (case-insensitive).

## API endpoints for model config administration

Base route: `/api/ai-model-configs`

- `GET /api/ai-model-configs`
- `GET /api/ai-model-configs/{id}`
- `POST /api/ai-model-configs`
- `PUT /api/ai-model-configs/{id}`
- `DELETE /api/ai-model-configs/{id}`

Example create payload:

```json
{
  "provider": "AZURE_OPENAI",
  "apiKey": "",
  "modelName": "gpt-4.1-mini",
  "maxTokens": 1200,
  "temperature": 0.2,
  "isActive": true
}
```

## `appsettings` configuration

Configure `LlmGateway`:

```json
{
  "LlmGateway": {
    "Enabled": true,
    "RequestTimeoutSeconds": 25,
    "MaxRetries": 2,
    "RetryBaseDelayMilliseconds": 400,
    "Providers": {
      "OPENAI": {
        "BaseUrl": "https://api.openai.com"
      },
      "AZURE_OPENAI": {
        "BaseUrl": "https://<resource>.openai.azure.com",
        "ApiVersion": "2024-10-21"
      },
      "AZURE_OPENAI_VISION": {
        "BaseUrl": "https://<resource>.openai.azure.com",
        "ApiVersion": "2024-10-21"
      },
      "GEMINI_OPENAI": {
        "BaseUrl": "https://generativelanguage.googleapis.com/v1beta/openai"
      }
    }
  }
}
```

Gemini OpenAI-compat endpoint reference (official):  
https://ai.google.dev/gemini-api/docs/openai

## Retry, timeout, and resilience behavior

Current gateway behavior:

- per-attempt timeout via `RequestTimeoutSeconds`.
- retries for transient errors up to `MaxRetries` with exponential backoff.
- transient failure detection for timeout/network/HTTP `408`/`429`/`5xx`.
- normalized failure payload returned when final attempt fails.
- telemetry persisted to `ChatbotActionLogs`.

Circuit-breaker compatibility:

- gateway returns normalized transient failure metadata (`IsTransientFailure`, `FailureCode`) suitable for external circuit-breaker policies.
- if a circuit-breaker is introduced later, apply it in provider client or gateway decorator.

## Security guidance

- Never commit real provider secrets in `appsettings.json`.
- Prefer secure secret sources (environment variables, vault, secret manager).
- If storing API key in `AI_ModelConfigs`, enforce DB access control and key rotation.
- Tool calls must remain server-authoritative regardless of model output.

## Troubleshooting

| Failure code | Meaning | Typical fix |
|---|---|---|
| `LLM_DISABLED` | gateway disabled | set `LlmGateway:Enabled=true` |
| `MODEL_CONFIG_NOT_FOUND` | no active model config | add active row in `AI_ModelConfigs` |
| `PROVIDER_ENDPOINT_NOT_CONFIGURED` | provider missing in `LlmGateway:Providers` | add matching provider entry |
| `PROVIDER_API_KEY_MISSING` | no API key in DB or provider fallback | configure key securely |
| `PROVIDER_CLIENT_NOT_FOUND` | no client handles provider | add provider client implementation |
| `GATEWAY_TIMEOUT` / `TIMEOUT` | provider timeout | increase timeout or reduce prompt/tool load |
| `HTTP_429` | provider throttling | backoff, lower request rate, provision capacity |
| `PROVIDER_RESPONSE_PARSE_ERROR` | unexpected provider payload | verify endpoint/api version/model compatibility |

## Extension points

- Add new provider implementation: implement `IChatbotLlmProviderClient`.
- Add provider-specific auth/header conventions inside provider client.
- Add per-provider routing policy by extending `ResolveModelConfigAsync` and provider options.
- Add observability export by streaming `ChatbotActionLogs` to monitoring stack.
