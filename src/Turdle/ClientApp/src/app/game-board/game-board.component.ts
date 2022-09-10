import {Component, Input} from '@angular/core';
import {Board} from "../services/game.service";
import {interval, Subscription} from "rxjs";
import TrackByUtils from "../track-by.utils";

@Component({
  selector: 'game-board',
  templateUrl: './game-board.component.html',
  styleUrls: ['./game-board.component.css', './game-board.4.component.css', './game-board.5.component.css', './game-board.6.component.css', '../letter-colors.shared.css']
})
export class GameBoardComponent {

  constructor(
    public trackByUtils: TrackByUtils) {
  }

  @Input() board!: Board | null;
  @Input() wordLength!: number;
  @Input() maxGuesses!: number;
  @Input() currentWord: string | undefined;
  @Input() guessDeadlines: Date[] | null | undefined;
  @Input() currentExpectedGuessCount: number | undefined;
  @Input() nextGuessDeadline: Date | null | undefined;
  @Input() guessTimeLimitMs: number | null | undefined;
  public secondsUntilGuessDeadline: number | null = null;
  public deadlinePct: number | null = null;
  private timerSubscription: Subscription | null = null;

  public get isPlayer(): boolean {
    return this.currentWord !== undefined;
  }

  public enumerate(count: number): number[] | null {
    if (count <= 0)
      return [];
    return Array.from(Array(count).keys())
  }

  ngOnInit() {
    if (this.nextGuessDeadline !== undefined) {
      this.startTimer();
    }
  }

  private startTimer(): void {
    const pctPerGuess = 100 / 6;
    this.timerSubscription = interval(200).subscribe(x => {
      //console.log('tick');
      if (this.nextGuessDeadline != null) {
        let timeRemaining = new Date(this.nextGuessDeadline).getTime() - new Date().getTime();
        //console.log('timeRemaining=' + timeRemaining);
        if (timeRemaining < 0) {
          this.secondsUntilGuessDeadline = 0;
          if (this.guessTimeLimitMs != null && this.currentExpectedGuessCount) {
            this.deadlinePct = (this.currentExpectedGuessCount + 1) * pctPerGuess;
          }
        } else {
          this.secondsUntilGuessDeadline = Math.ceil(timeRemaining / 1000.0);
          if (this.guessTimeLimitMs != null && this.currentExpectedGuessCount != null) {
            let prevGuessPct = Math.min(5, this.currentExpectedGuessCount) * pctPerGuess;
            let currentGuessPct = (1 - (timeRemaining / this.guessTimeLimitMs)) * pctPerGuess;
            this.deadlinePct = prevGuessPct + currentGuessPct;
          }
        }
      }
    });
  }
}
