import {Component, HostListener, Inject} from '@angular/core';
import {Board, GameService, RoundState, Player, Row, PointSchedule} from "../services/game.service";
import {ActivatedRoute, Router} from '@angular/router'
import {OrderByPipe} from "../order-by.pipe";
import TrackByUtils from "../track-by.utils";

@Component({
  selector: 'app-tv',
  templateUrl: './tv.component.html',
  styleUrls: ['./tv.component.css', '../letter-colors.shared.css']
})
export class TvComponent {

  constructor(
    private route: ActivatedRoute,
    private gameService: GameService,
    private router: Router,
    private orderByPipe: OrderByPipe,
    public trackByUtils: TrackByUtils) {
    console.log('TvComponent ctor');
  }

  async ngOnInit() {
    const roomCode = this.route.snapshot.paramMap.get('code');
    if (!roomCode)
      return await this.router.navigate(['']);

    await this.gameService.setupConnection(roomCode);
    const success = await this.gameService.initGameData();
    if (!success)
      return await this.router.navigate(['']);
    await this.gameService.registerTvConnection();

    return null;
  }

  get roundState(): RoundState {
    return this.gameService.roundState;
  }
  get pointSchedule(): PointSchedule | null {
    return this.gameService.pointSchedule;
  }
  get secondsUntilStart(): number | null {
    return this.gameService.secondsUntilStart;
  }
}
