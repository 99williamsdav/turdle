import {Component, ElementRef, HostListener, Inject, Pipe, PipeTransform, ViewChild} from '@angular/core';
import {Board, GameService, RoundState, Player, Row, PointSchedule, GameParameters, ChatMessage} from "../services/game.service";
import {ActivatedRoute, Router} from '@angular/router'
import {OrderByPipe} from "../order-by.pipe";
import TrackByUtils from "../track-by.utils";
import {FormBuilder, Validators} from "@angular/forms";
import {ToastService} from "../toast/toast-service";
import { NgbModal } from '@ng-bootstrap/ng-bootstrap';
import { GameParamsComponent } from '../game-params/game-params.component';
import { AdminService } from '../services/admin.service';

@Component({
  selector: 'app-game',
  templateUrl: './game.component.html',
  styleUrls: ['./game.component.css', '../letter-colors.shared.css']
})
export class GameComponent {
  @ViewChild('chatInputField') chatInputField!: ElementRef<HTMLInputElement>;
  @ViewChild('chatMessagesContainer') chatMessagesContainer!: ElementRef<HTMLDivElement>;
  public innerWidth: number | null = null;
  public isSmallScreen: boolean = false;
  public botPersonality: string = '';
  aliasForm = this.fb.group({
    Alias: ['', Validators.required]
  });
  chatInput: string = '';

  constructor(
    private route: ActivatedRoute,
    private gameService: GameService,
    private toastService: ToastService,
    private fb: FormBuilder,
    private router: Router,
    private orderByPipe: OrderByPipe,
    public trackByUtils: TrackByUtils,
    private modalService: NgbModal) {
    console.log('GameComponent ctor');
  }

  async ngOnInit() {
    const roomCode = this.route.snapshot.paramMap.get('code');
    if (!roomCode)
      return await this.router.navigate(['/']);

    await this.gameService.setupConnection(roomCode);
    const initSuccess = await this.gameService.initGameData();
    if (!initSuccess) {
      this.toastService.error('Room does not exist');
      return await this.router.navigate(['/']);
    }

    this.setInnerWidth();

    if (this.gameService.currentPlayer != null)
      return null;

    this.gameService.previousAliasInfoObservable.subscribe(aliasInfo => {
      console.log(`previousAlias info: ${aliasInfo.alias} (${aliasInfo.status})`)
      if (aliasInfo.status == 'RegisteredConnected') {
        // TODO just don't register
        return this.router.navigate(['/']);
      }

      if (!aliasInfo.alias)
        return;

      this.aliasForm.setValue({Alias: aliasInfo.alias});

      let alias: string = aliasInfo.alias;
      return this.gameService.onHubConnected.subscribe(connected =>
      {
        if (!connected)
          return new Promise<void>(() => null);

        console.log(`Hub connected, registering alias: ${alias}`);
        return this.gameService.registerAlias(alias);
      });
    });

    this.gameService.onNewChatMessage.subscribe(chatMessage => {
      if (!chatMessage) return;
      console.log('new chat message, scrolling container.')
      this.chatMessagesContainer.nativeElement.scroll(0, 0);
    });

    return null;
  }

  @HostListener('window:resize', ['$event'])
  onResize(event: Event) {
    this.setInnerWidth();
  }

  private setInnerWidth(): void {
    this.innerWidth = window.innerWidth;
    this.isSmallScreen = this.innerWidth < 768;
  }

  @HostListener('document:keydown', ['$event'])
  async handleKeyboardEvent(event: KeyboardEvent) {
    if (this.roundState.status == 'Playing')
    {
      await this.gameService.enterKey(event.key);
    } else if (this.roundState.status == 'Ready') {
      await this.gameService.enterFakeReadyKey(event.key);
    }
  }

  async clickKey(letter: string) {
    if (this.roundState.status == 'Playing') {
      await this.gameService.enterKey(letter);
    }
  }

  async registerAlias() {
    let success = await this.gameService.registerAlias(this.aliasForm.value.Alias)
  }

  async clickFakeReadyKey(letter: string) {
    if (this.roundState.status == 'Ready') {
      await this.gameService.enterFakeReadyKey(letter);
    }
  }

  public async giveUp(): Promise<void> {
    await this.gameService.giveUp();
  }
  public async suggestGuess(): Promise<void> {
    await this.gameService.suggestGuess();
  }
  public async revealAbsentLetter(): Promise<void> {
    await this.gameService.revealAbsentLetter();
  }
  public async revealPresentLetter(): Promise<void> {
    await this.gameService.revealPresentLetter();
  }
  public async updateAnswerList(): Promise<void> {
    try {
      await this.gameService.updateAnswerList(this.gameParams!.answerList);
    } catch (e: any) {
      this.toastService.default(e.message);
    }
  }
  public async sendChat(): Promise<void> {
    let message = this.chatInput;
    this.chatInput = '';
    await this.gameService.sendChatMessage(message);
  }

  public chatTyping(): void {
    if (this.chatInput && this.chatInput.length > 0)
      this.gameService.notifyTyping();
    else
      this.gameService.notifyStopTyping();
  }
  public async prepopulateChat(alias: string): Promise<void> {
    this.chatInput = '@' + alias + ' ';
    this.chatInputField.nativeElement.focus();
  }

  public async addBot(personality: string | null): Promise<void> {
    await this.gameService.addBot(personality);
    this.botPersonality = '';
  }
  public openGameParams(): void {
    const modalRef = this.modalService.open(GameParamsComponent);
    modalRef.componentInstance.gameParams = this.gameParams;
    modalRef.result.then((result: boolean) => {
      if (result) {
        this.updateAnswerList();
      }
    }, (reason: string) => {
      console.log('Dismissed action: ' + reason);
    });
  }

  // PROPERTY GETTERS

  get isConnected() : boolean {
    return this.gameService.isConnected;
  }
  get roomCode() : string {
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
  get leftPlayers(): Player[] {
    let players = this.orderByPipe.transform(this.roundState.players.filter(p => p.alias != this.currentPlayer?.alias), 'registeredAt');
    return players.filter((p, i) => i % 3 < 2);
  }
  get rightPlayers(): Player[] {
    let players = this.orderByPipe.transform(this.roundState.players.filter(p => p.alias != this.currentPlayer?.alias), 'registeredAt');
    return players.filter((p, i) => i % 3 == 2);
  }
  get playerAlias(): string {
    return this.gameService.playerAlias;
  }
  get currentPlayer(): Player | null {
    return this.gameService.currentPlayer;
  }
  get currentBoard(): Board | null {
    return this.gameService.currentBoard;
  }
  get currentWord(): string {
    return this.gameService.currentWord;
  }
  get fakeReadyGameBoard(): Board | null {
    return this.gameService.fakeReadyGameBoard;
  }
  get fakeReadyGameWord(): string {
    return this.gameService.fakeReadyGameWord;
  }
  get secondsUntilStart(): number | null {
    return this.gameService.secondsUntilStart;
  }
  get currentUrl(): string {
    return window.location.href;
  }
  get chatMessages(): ChatMessage[] {
    return this.gameService.chatMessages;
  }
  get typingPlayers(): string[] {
    return this.gameService.typingAliases;
  }

  // UTILS

  public enumerate(count: number): number[] | null {
    if (count <= 0)
      return [];
    return Array.from(Array(count).keys())
  }
  public async copyUrl(): Promise<void> {
    await navigator.clipboard.writeText(this.currentUrl);
    this.toastService.default('URL copied to clipboard.');
    //this.toastService.show('URL copied to clipboard.', { classname: 'bg-success text-light', delay: 5000 });
  }

  public async toggleReady(ready: boolean): Promise<void> {
    await this.gameService.toggleReady(ready);
  }
  public async logOut(): Promise<void> {
    await this.gameService.logOut();
  }
  public async startGame(): Promise<void> {
    await this.gameService.startGame();
  }

  get pings(): Date[] {
    return this.gameService.pings;
  }
}

@Pipe({
  name: 'notAlias'
})
export class NotAliasPipe implements PipeTransform {
  transform(value: Player[], alias: string | null | undefined): Player[] {
    return alias != null ? value.filter(x => x.alias != alias) : value;
  }
}
