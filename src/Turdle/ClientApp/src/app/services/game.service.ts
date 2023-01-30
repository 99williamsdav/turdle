import {Inject, Injectable, NgZone} from '@angular/core';
import * as signalR from "@aspnet/signalr";
import {BehaviorSubject, interval, Observable, of, Subject, Subscription, timer} from 'rxjs';
import {HttpClient, HttpParams} from "@angular/common/http";
import {CookieService} from "ngx-cookie";

@Injectable({
  providedIn: 'root'
})
export class GameService {
  public roomCode: string = '';

  public playerAlias: string = '';
  public pointSchedule: PointSchedule | null = null;
  public roundState: RoundState = { status: 'Waiting', players: [], startTime: null, roundNumber: 1, guessDeadlines: null, currentExpectedGuessCount: 0, nextGuessDeadline: null, guessTimeLimitMs: null, correctAnswer: null, wordLength: 5, maxGuesses: 6};
  public currentPlayer: Player | null = null;
  public currentBoard: Board | null = null;
  public currentWord: string = '';
  public gameParams: GameParameters | null = null;
  public secondsUntilStart: number | null = null;
  private _previousAliasInfo: Subject<AliasInfo> = new Subject<AliasInfo>();
  public previousAliasInfoObservable: Observable<AliasInfo> = this._previousAliasInfo.asObservable();
  //public previousAliasInfo: AliasInfo | null = null;
  private countdownTimerSubscription: Subscription | null = null;
  public pings: Date[] = [];
  private hubConnection: signalR.HubConnection | null = null;
  private _onHubConnected: BehaviorSubject<boolean> = new BehaviorSubject<boolean>(false);
  public onHubConnected: Observable<boolean> = this._onHubConnected.asObservable();
  private alphabet: string = 'QWERTYUIOPASDFGHJKLZXCVBNM';
  private guessing: boolean = false;
  private suggestingGuess: boolean = false;

  public fakeReadyGameBoard: Board | null = null;
  public fakeReadyGameWord: string = 'STAR';

  constructor(
    private ngZone: NgZone,
    private http: HttpClient,
    private cookieService: CookieService,
    @Inject('BASE_URL') private baseUrl: string) {
    console.log('GameService ctor');
  }

  public async setupConnection(roomCode: string): Promise<void> {
    this.roomCode = roomCode;

    if (this.isConnected) {
      this.roundState = { status: '', players: [], startTime: null, roundNumber: 0, guessDeadlines: null, currentExpectedGuessCount: 0, nextGuessDeadline: null, guessTimeLimitMs: null, correctAnswer: null, wordLength: 0, maxGuesses: 0 };
      return;
    }

    console.log('Initialising game hub connection.');
    let hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.baseUrl + 'gameHub')
      .build();
    try {
      await hubConnection.start();
      this.hubConnection = hubConnection;
      this._onHubConnected.next(true);
    } catch (e) {
      console.log('Error while starting connection: ' + e);
      return;
    }

    console.log('Game hub connection started');
    this.hubConnection?.on('Ping', (data) => {
      this.ngZone.run(() => this.pings.push(new Date()));
    });

    this.hubConnection?.on('GameStateUpdated', (gameState: RoundState) => {
      this.ngZone.run(() => {
        this.roundState = gameState;
        this.currentPlayer = gameState.players.filter(p => p.alias == this.playerAlias)[0];
        this.hydrateKnownWords();
      });
    });

    this.hubConnection?.on('GameParametersUpdated', (gameParams: GameParameters) => {
      this.ngZone.run(() => {
        this.gameParams = gameParams;
      });
    });

    this.hubConnection?.on('StartNewGame', (freshBoard: Board) => {
      this.ngZone.run(() => {
        this.currentBoard = freshBoard; // { status: 'Playing', rows: [], presentLetters: [], absentLetters: [], correctLetters: [], points: 0, currentRowPoints: 0, solvedOrder: null, rank: 1, isJointRank: true };
        this.currentWord = '';
        this.secondsUntilStart = null;
        this.startCountdown();
        this.updatePointSchedule();
      });
    });

    this.hubConnection?.on('BoardUpdated', (board: Board) => {
      this.ngZone.run(() => {
        this.currentBoard = board;
        this.hydrateKnownWords();
      });
    });
  }

  public async initGameData(): Promise<void> {
    this.http.get<AliasInfo>(this.baseUrl + 'getpreviousalias').subscribe(async result => {
      //this.previousAliasInfo = result;
      this._previousAliasInfo.next(result);
    } );

    this.http.get<RoundState>(this.baseUrl + 'getgamestate', { params: new HttpParams().set('roomCode', this.roomCode) })
      .subscribe(async result => {
        this.roundState = result;
      }, error => console.error(error));

    this.http.get<GameParameters>(this.baseUrl + 'getgameparameters', { params: new HttpParams().set('roomCode', this.roomCode) })
      .subscribe(async result => {
        this.gameParams = result;
      }, error => console.error(error));

    this.http.get<Board>(this.baseUrl + 'getfakereadyboard').subscribe(async result => {
      this.fakeReadyGameBoard = result;
    }, error => console.error(error));

    this.updatePointSchedule();
  }

  private hydrateKnownWords(): void {
    if (this.currentBoard == null || this.currentBoard.status != 'Playing' || this.roundState == null)
      return;

    if (Object.keys(this.currentBoard.knownLetterStatusHashes).length > 0) {

      for (let player of this.roundState.players) {
        if (player.board == null)
          continue;
        for (let row of player.board.rows) {
          for (let tile of row.tiles) {
            if (tile.statusHash in this.currentBoard.knownLetterStatusHashes)
              tile.letterPosition = {
                letter: this.currentBoard.knownLetterStatusHashes[tile.statusHash],
                position: tile.position
              };
          }
        }
      }

    } else if (Object.keys(this.currentBoard.knownWordHashes).length > 0) {

      for (let player of this.roundState.players) {
        if (player.board == null)
          continue;
        for (let row of player.board.rows) {
          if (row.wordHash in this.currentBoard.knownWordHashes)
           row.tiles = this.currentBoard.knownWordHashes[row.wordHash];
        }
      }

    }
  }

  private updatePointSchedule(): void {
    this.http.get<PointSchedule>(this.baseUrl + 'getpointschedule', { params: new HttpParams().set('roomCode', this.roomCode) })
      .subscribe(async result => {
      this.pointSchedule = result;
    }, error => console.error(error));
  }

  private startCountdown(): void {
    this.countdownTimerSubscription = interval(200).subscribe(x => {
      console.log('timer interval. x=' + x + ' startTime=' + this.roundState.startTime);
      if (this.roundState.startTime != null) {
        let timeRemaining = Date.parse(this.roundState.startTime) - new Date().getTime();
        console.log('timeRemaining=' + timeRemaining);
        if (timeRemaining < 0) {
          this.secondsUntilStart = 0;
          this.countdownTimerSubscription?.unsubscribe();
        } else {
          this.secondsUntilStart = Math.ceil(timeRemaining / 1000.0);
        }
      }
    });
  }

  public async registerAlias(alias: string): Promise<boolean> {
    if (this.hubConnection == null)
      return false;

    try {
      let result = await this.hubConnection.invoke<Result<Player>>('RegisterAlias', this.roomCode, alias);

      if (!result.isSuccess) {
        alert(result.errorMessage);
        return false;
      }

      console.log(alias);
      this.playerAlias = alias;
      this.currentPlayer = result.response;
      this.cookieService.put('LastAlias', alias);

      if (this.roundState.status == 'Playing' || this.roundState.status == 'Finished')
      {
        this.currentBoard = await this.hubConnection.invoke<Board>('GetPlayerBoard', this.roomCode);
        this.hydrateKnownWords();
      }

      return true;
    } catch (e) {
      console.log('Error while registering alias: ' + e);
      return false;
    }
  }

  // TODO restrict to admin somehow
  public async registerAdminConnection(): Promise<void> {
    if (this.hubConnection == null)
      return;

    await this.hubConnection.invoke('RegisterAdminConnection', this.roomCode)
  }
  public async registerTvConnection(): Promise<void> {
    if (this.hubConnection == null)
      return;

    if (!this.roomCode)
      return;

    await this.hubConnection.invoke('RegisterTvConnection', this.roomCode)
  }

  public async startGame(): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('VoteToStart', this.roomCode);
    } catch (e) {
      console.log('Error voting to start: ' + e);
    }
  }

  public async toggleReady(ready: boolean): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      await this.hubConnection.invoke('ToggleReady', this.roomCode, ready);
    } catch (e) {
      console.log('Error toggling ready: ' + e);
    }
  }

  public async giveUp(): Promise<void> {
    if (this.hubConnection == null)
      return;

    try {
      let result = await this.hubConnection.invoke<Result<Board>>('GiveUp', this.roomCode);
      if (result.isSuccess) {
        this.currentBoard = result.response;
        this.currentWord = '';
      } else {
        // TODO nice alert
        alert(result.errorMessage);
      }
    } catch (e) {
      console.log('Error giving up: ' + e);
    }
  }

  public async logOut(): Promise<void> {
    if (this.hubConnection == null)
      return;
    if (this.roomCode == null)
      return;

    try {
      await this.hubConnection.invoke('LogOut', this.roomCode);
      this.cookieService.remove('LastAlias');
      this.currentPlayer = null;
      this.playerAlias = '';
      this.currentBoard = null;
    } catch (e) {
      console.log('Error logging out: ' + e);
    }
  }

  public async enterFakeReadyKey(letter: string): Promise<void> {
    if (letter.length == 1 &&
      this.fakeReadyGameWord.length < 6 &&
      this.alphabet.indexOf(letter.toUpperCase()) > -1) {
      this.fakeReadyGameWord += letter.toUpperCase();
    } else if (letter == 'Backspace') {
      this.fakeReadyGameWord = this.fakeReadyGameWord.substring(0, this.fakeReadyGameWord.length - 1);
    } else if (letter == 'Enter' && this.fakeReadyGameWord.length == 5) {
      if (this.fakeReadyGameWord == 'START' || this.fakeReadyGameWord == 'BEGIN') {
        await this.startGame();
      } else {
        alert('Try again');
      }
    }
  }

  public async enterKey(letter: string): Promise<void> {
    console.log(letter);
    if (this.currentBoard?.status != 'Playing') {
      console.log('Ignoring key press because board status is ' + this.currentBoard?.status);
      return;
    }
    if (this.roundState.status != 'Playing') {
      console.log('Ignoring key press because game status is ' + this.roundState.status);
      return;
    }

    if (letter.length == 1 &&
      this.currentWord.length < this.roundState.wordLength &&
      this.alphabet.indexOf(letter.toUpperCase()) > -1) {
      this.currentWord += letter.toUpperCase();
    } else if (letter == 'Backspace') {
      this.currentWord = this.currentWord.substring(0, this.currentWord.length - 1);
    } else if (letter == 'Enter' && this.currentWord.length == this.roundState.wordLength) {
      await this.playGuess();
    }
  }

  public async playGuess(): Promise<void> {
    if (this.hubConnection == null) {
      console.log('No server connection.');
      return;
    }
    if (this.currentBoard?.status != 'Playing') {
      console.log('Board is complete.');
      return;
    }

    if (this.guessing) {
      console.log('debounce guess');
      return;
    }
    this.guessing = true;

    try {
      let result = await this.hubConnection.invoke<Result<Board>>('PlayGuess', this.roomCode, this.currentWord, this.currentBoard.rows.length + 1);
      if (result.isSuccess) {
        this.currentBoard = result.response;
        console.log(this.currentWord);
        this.currentWord = '';
        this.hydrateKnownWords();
      } else {
        // TODO nice alert
        alert(result.errorMessage);
      }
    } catch (e) {
      console.log('Error while playing guess: ' + e);
    } finally {
      this.guessing = false;
    }
  }

  public async suggestGuess(): Promise<void> {
    if (this.hubConnection == null) {
      console.log('No server connection.');
      return;
    }
    if (this.currentBoard?.status != 'Playing') {
      console.log('Board is complete.');
      return;
    }

    if (this.suggestingGuess) {
      console.log('debounce suggest guess');
      return;
    }
    this.suggestingGuess = true;

    try {
      let suggestedGuess = await this.hubConnection.invoke<string>('SuggestGuess', this.roomCode);
      if (suggestedGuess) {
        this.currentWord = suggestedGuess;
      } else {
        // TODO nice alert
        alert('No valid word found.');
      }
    } catch (e) {
      console.log('Error while suggesting guess: ' + e);
    } finally {
      this.suggestingGuess = false;
    }
  }

  public async revealAbsentLetter(): Promise<void> {
    if (this.hubConnection == null) {
      console.log('No server connection.');
      return;
    }
    if (this.currentBoard?.status != 'Playing') {
      console.log('Board is complete.');
      return;
    }

    try {
      let result = await this.hubConnection.invoke<Result<Board>>('RevealAbsentLetter', this.roomCode);
      if (result.isSuccess) {
        this.currentBoard = result.response;
      } else {
        // TODO nice alert
        alert(result.errorMessage);
      }
    } catch (e) {
      console.log('Error while revealing absent letter: ' + e);
    }
  }

  public async revealPresentLetter(): Promise<void> {
    if (this.hubConnection == null) {
      console.log('No server connection.');
      return;
    }
    if (this.currentBoard?.status != 'Playing') {
      console.log('Board is complete.');
      return;
    }

    try {
      let result = await this.hubConnection.invoke<Result<Board>>('RevealPresentLetter', this.roomCode);
      if (result.isSuccess) {
        this.currentBoard = result.response;
      } else {
        // TODO nice alert
        alert(result.errorMessage);
      }
    } catch (e) {
      console.log('Error while revealing present letter: ' + e);
    }
  }


  // ADMIN METHODS

  public async updateGuessTimeLimit(seconds: number): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateGuessTimeLimit', this.roomCode, seconds);
    } catch (e) {
      console.log('Error updating guess time limit: ' + e);
    }
  }

  public async updateAnswerList(answerListType: string): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateAnswerList', this.roomCode, answerListType);
    } catch (e) {
      console.log('Error updating word length: ' + e);
    }
  }

  public async updateMaxGuesses(maxGuesses: number): Promise<void> {
    if (this.hubConnection == null)
      return;
    try {
      await this.hubConnection.invoke('UpdateMaxGuesses', this.roomCode, maxGuesses);
    } catch (e) {
      console.log('Error updating max guesses: ' + e);
    }
  }


  // PROPERTY GETTERS

  public get isConnected() : boolean {
    return this.hubConnection != null;
  }
}

export interface Result<T> {
  response: T;
  isSuccess: boolean;
  errorMessage: string | null;
}

export interface RoundState {
  status: string;
  players: Player[];
  startTime: string | null;
  roundNumber: number;
  guessDeadlines: Date[] | null;
  nextGuessDeadline: Date | null;
  guessTimeLimitMs: number | null;
  currentExpectedGuessCount: number;
  correctAnswer: string | null | undefined;
  wordLength: number;
  maxGuesses: number;
}

export interface AliasInfo {
  alias: string | null;
  status: string;
}

export interface Player {
  alias: string;
  points: number;
  ready: boolean;
  connectionId: string;
  isConnected: boolean;
  rank: number;
  isJointRank: boolean;
  board: Board | null;
  joinedOn: string;
  registeredAt: string;
}

export interface Board {
  status: string;
  rows: Row[];
  correctLetters: LetterPosition[];
  presentLetters: LetterPosition[];
  absentLetters: string[];
  presentLetterCounts: {[key: string]: number};
  letterStatuses: {[key: string]: string};
  knownLetterStatusHashes: {[key: string]: string};
  knownWordHashes: {[key: string]: Tile[]};
  solvedOrder: number | null;
  completionTimeMs: number | null;
  points: number;
  currentRowPoints: number;
  rank: number;
  isJointRank: boolean;
}

export interface Row {
  tiles: Tile[];
  isCorrect: boolean;
  playedOrder: number | null;
  playedAt: Date | null;
  guessNumber: number;
  pointsAwarded: number;
  errors: TileError[] | null;
  pointsAdjustments: PointAdjustment[];
  wasForced: boolean;
  wordHash: string;
  emoji: string | null;
}

export interface TileError {
  letterPosition: LetterPosition;
  error: string;
}

export interface Tile {
  letterPosition: LetterPosition;
  status: string;
  statusHash: string;
  position: number;
}

export interface LetterPosition {
  position: number;
  letter: string;
}

export interface PointAdjustment {
  reason: string;
  points: number;
  description: string;
}

export interface PointSchedule {
   pointScaleType: string;
  // points awarded by order of submitting a valid answer for each row
  validAnswerOrderPoints: number[][];
  firstValidAnswerPoints: number[];
  // points awarded by order of getting the correct answer
  correctWordOrderPoints: number[];
  firstCorrectAnswerPoints: number;
  // points awarded for which guess is the correct answer
  solutionGuessNumberPoints: number[];
  // points lost for breaking hard-mode
  hardModeErrorPoints: {[key: string]: number};
  suggestedGuessCostPoints: number;
  revealedAbsentCostPoints: number;
  revealedPresentCostPoints: number;
}

export interface Room {
  createdOn : Date;
  roomCode: string;
  players: Player[];
  adminAlias: string;
  roundNumber: number;
  currentRoundStatus: string;
}

export interface GameParameters {
  maxGuesses: number;
  guessTimeLimitSeconds: number;
  answerList: string;
  useNaughtyWordList: boolean;
  adminAlias: string | null;
}
