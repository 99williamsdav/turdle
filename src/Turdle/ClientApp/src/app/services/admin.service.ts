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
    try {
      await this.hubConnection.start();
    } catch (e) {
      console.log('Error while starting connection: ' + e);
      return;
    }

    console.log('Admin hub connection started');
    this.hubConnection?.on('Ping', (data) => {
      this.ngZone.run(() => this.pings.push(new Date()));
    });
  }

  public async kickPlayer(alias: string): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('KickPlayer', alias);
    } catch (e) {
      console.log('Error kicking player: ' + e);
    }
  }

  public async disconnectPlayer(alias: string): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('DisconnectPlayer', alias);
    } catch (e) {
      console.log('Error disconnecting player: ' + e);
    }
  }

  public async hardReset(): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('HardReset');
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

  public get isConnected() : boolean {
    return this.hubConnection != null;
  }
}
