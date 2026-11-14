import { HubConnectionBuilder, LogLevel } from "@microsoft/signalr";

export function createChatHubConnection() {
  return new HubConnectionBuilder()
    .withUrl("/hubs/chat", {
      withCredentials: true,
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000])
    .configureLogging(LogLevel.Warning)
    .build();
}
