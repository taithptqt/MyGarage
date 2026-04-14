import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";
import { readAuthToken } from "@features/auth/utils/authStorage";

function getAccessToken() {
  try {
    return readAuthToken() || localStorage.getItem("token") || "";
  } catch {
    return "";
  }
}

export function createChatHubConnection() {
  return new HubConnectionBuilder()
    .withUrl("/hubs/chat", {
      accessTokenFactory: () => getAccessToken(),
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build();
}
