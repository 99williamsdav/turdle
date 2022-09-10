import {Inject, Injectable, NgZone} from '@angular/core';
import * as signalR from "@aspnet/signalr";
import {HttpClient, HttpParams} from "@angular/common/http";
import {AliasInfo, Board, Player, Room, RoundState} from "./game.service";

@Injectable({
  providedIn: 'root'
})
export class HomeService {
  public pings: Date[] = [];
  private _hubConnection: signalR.HubConnection | null = null;
  public isConnected: boolean = false;
  public rooms: Room[] = [];

  constructor(
    private ngZone: NgZone,
    private http: HttpClient,
    @Inject('BASE_URL') private baseUrl: string) {
    console.log('HomeService ctor');
  }

  public async setupConnection(): Promise<void> {
    console.log('Initialising home hub connection.');
    this._hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.baseUrl + 'homeHub')
      .build();
    try {
      await this._hubConnection.start();
      this.isConnected = true;
    } catch (e) {
      console.log('Error while starting connection: ' + e);
      return;
    }

    console.log('Connection started');
  }

  public async initHomeData(): Promise<void> {
    this.http.get<Room[]>(this.baseUrl + 'getRooms').subscribe(async result => {
      this.rooms = result;
    }, error => console.error(error));
  }

  public async createRoom(): Promise<string | null> {
    if (this._hubConnection == null)
      return null;

    try {
      return await this._hubConnection.invoke('CreateRoom');
    } catch (e) {
      console.log('Error creating room: ' + e);
      return null;
    }
  }
}
