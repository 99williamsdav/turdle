import {Component, HostListener, Inject} from '@angular/core';
import {Board, GameService, RoundState, Player, Row, PointSchedule, Room, GameParameters} from "../services/game.service";
import {AdminService} from "../services/admin.service";
import {HomeService} from "../services/home.service";
import { ToastService } from '../toast/toast-service';

@Component({
  selector: 'admin',
  templateUrl: './admin.component.html',
  styleUrls: ['./admin.component.css']
})
export class AdminComponent {
  public guessTimeLimitSeconds: number = 30;
  public answerListType: string = 'FiveLetterEasy';
  public maxGuesses: number = 6;

  constructor(
    private gameService: GameService,
    private adminService: AdminService,
    private homeService: HomeService,
    private toastService: ToastService) {
    console.log('AdminComponent ctor');
  }

  async ngOnInit() {
    await this.homeService.setupConnection();
    await this.homeService.initHomeData();
    await this.adminService.setupConnection();
  }


  // METHODS

  async selectRoom(roomCode: string) {
    if (this.gameService.isConnected) {
      await this.gameService.logOut();
    }

    await this.gameService.setupConnection(roomCode);
    await this.gameService.initGameData();
    await this.gameService.registerAdminConnection();
  }

  async kickPlayer(alias: string) {
    await this.adminService.kickPlayer(this.roomCode, alias);
  }
  async disconnectPlayer(alias: string) {
    await this.adminService.disconnectPlayer(this.roomCode, alias);
  }
  async hardReset() {
    await this.adminService.hardReset(this.roomCode);
  }
  public async startGame(): Promise<void> {
    await this.gameService.startGame();
  }
  public async updatePointSchedule(): Promise<void> {
    if (this.gameService.pointSchedule != null)
      await this.adminService.updatePointSchedule(this.gameService.pointSchedule);
  }
  public async updateGuessTimeLimit(): Promise<void> {
    try {
      await this.gameService.updateGuessTimeLimit(this.gameParams!.guessTimeLimitSeconds);
    } catch (e: any) {
      this.toastService.default(e.message);
    }
  }
  public async updateAnswerList(): Promise<void> {
    try {
      await this.gameService.updateAnswerList(this.gameParams!.answerList);
    } catch (e: any) {
      this.toastService.default(e.message);
    }
  }
  public async updateMaxGuesses(): Promise<void> {
    try {
      await this.gameService.updateMaxGuesses(this.gameParams!.maxGuesses);
    } catch (e: any) {
      this.toastService.default(e.message);
    }
  }


  // PROPERTY GETTERS

  get isConnected() : boolean {
    return this.homeService.isConnected;
  }
  get rooms(): Room[] {
    return this.homeService.rooms;
  }
  get roomCode(): string {
    return this.gameService.roomCode;
  }
  get roundState(): RoundState {
    return this.gameService.roundState;
  }
  get gameParams(): GameParameters | null {
    return this.gameService.gameParams;
  }
  get pointSchedule(): PointSchedule | null {
    return this.gameService.pointSchedule;
  }
  get secondsUntilStart(): number | null {
    return this.gameService.secondsUntilStart;
  }

  // UTILS

  public enumerate(count: number): number[] | null {
    if (count <= 0)
      return [];
    return Array.from(Array(count).keys())
  }
  public getPlayerCount(array: Player[], isBot: boolean) : number {
    return array.filter(x => isBot === x.isBot).length;
  }
}
