import {Inject, Injectable, NgZone} from '@angular/core';
import * as signalR from "@aspnet/signalr";
import {HttpClient} from "@angular/common/http";
import {PointSchedule} from "./game.service";

@Injectable({
  providedIn: 'root'
})
export class AdminService {
  public pings: Date[] = [];
  private hubConnection: signalR.HubConnection | null = null;

  constructor(
    private ngZone: NgZone,
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string) {
    console.log('AdminService ctor');
  }

  public async setupConnection(): Promise<void> {
    console.log('Initialising admin hub connection.');
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.baseUrl + 'adminHub')
      .build();
    this.hubConnection.onclose(() => {
      setTimeout(() => this.startConnection(), 5000);
    });

    await this.startConnection();

    console.log('Admin hub connection started');
    this.hubConnection?.on('Ping', (data) => {
      this.ngZone.run(() => this.pings.push(new Date()));
    });
  }

  public async kickPlayer(roomCode: string, alias: string): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('KickPlayer', roomCode, alias);
    } catch (e) {
      console.log('Error kicking player: ' + e);
    }
  }

  public async disconnectPlayer(roomCode: string, alias: string): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('DisconnectPlayer', roomCode, alias);
    } catch (e) {
      console.log('Error disconnecting player: ' + e);
    }
  }

  public async hardReset(roomCode: string): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('HardReset', roomCode);
    } catch (e) {
      console.log('Error doing hard reset: ' + e);
    }
  }

  public async ping(): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('PingAll');
    } catch (e) {
      console.log('Error pinging: ' + e);
    }
  }

  public async updatePointSchedule(pointSchedule: PointSchedule): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdatePointSchedule', pointSchedule);
    } catch (e) {
      console.log('Error updating point schedule: ' + e);
    }
  }

  public async updateGuessTimeLimit(seconds: number): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateGuessTimeLimit', seconds);
    } catch (e) {
      console.log('Error updating guess time limit: ' + e);
    }
  }

  public async updateWordLength(length: number): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateWordLength', length);
    } catch (e) {
      console.log('Error updating word length: ' + e);
    }
  }

  public async updateMaxGuesses(maxGuesses: number): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateMaxGuesses', maxGuesses);
    } catch (e) {
      console.log('Error updating max guesses: ' + e);
    }
  }

  private async startConnection(): Promise<void> {
    if (!this.hubConnection) return;
    try {
      await this.hubConnection.start();
    } catch (e) {
      console.log('Error while starting connection: ' + e);
      setTimeout(() => this.startConnection(), 5000);
    }
  }

  public get isConnected() : boolean {
    return this.hubConnection != null;
  }
}
