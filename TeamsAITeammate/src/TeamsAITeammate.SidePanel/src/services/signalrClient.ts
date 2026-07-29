import * as signalR from '@microsoft/signalr';
import { authentication } from '@microsoft/teams-js';

let connection: signalR.HubConnection | null = null;

export function getConnection(meetingId: string): signalR.HubConnection {
  if (connection) return connection;

  connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/meeting-analysis', {
      accessTokenFactory: () => authentication.getAuthToken(),
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Information)
    .build();

  connection.onreconnected(() => {
    console.log('SignalR reconnected — rejoining meeting group');
    connection?.invoke('JoinMeeting', meetingId).catch(console.error);
  });

  return connection;
}

export async function startConnection(meetingId: string): Promise<signalR.HubConnection> {
  const conn = getConnection(meetingId);

  if (conn.state === signalR.HubConnectionState.Disconnected) {
    await conn.start();
    await conn.invoke('JoinMeeting', meetingId);
  }

  return conn;
}

export async function stopConnection(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}
