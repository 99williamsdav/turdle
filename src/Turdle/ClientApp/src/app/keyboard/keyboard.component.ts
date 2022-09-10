import {Component, EventEmitter, Input, Output} from '@angular/core';
import {Board, Player, Tile} from "../services/game.service";

@Component({
  selector: 'keyboard',
  templateUrl: './keyboard.component.html',
  styleUrls: ['./keyboard.component.css', '../letter-colors.shared.css']
})
export class KeyboardComponent {
  @Input() currentBoard!: Board;
  @Output() keyPress: EventEmitter<string> = new EventEmitter();
  public keyboard: string[] = [ 'QWERTYUIOP', 'ASDFGHJKL', ' ZXCVBNM' ];
  public keys: string[][] = this.keyboard.map(row => row.split(''));

  public keyClicked(letter: string): void {
    console.log(letter);
    this.keyPress.emit(letter);
  }
  public getLetterStatus(letter: string): string {
    if (this.currentBoard == null)
      return '';

    console.log('getLetterStatus(' + letter);
    if (this.currentBoard.absentLetters.indexOf(letter) > -1)
      return 'Absent';
    if (this.currentBoard.correctLetters.some(lp => lp.letter == letter))
      return 'Correct';
    if (this.currentBoard.presentLetters.some(lp => lp.letter == letter))
      return 'Present';

    return '';
  }

  public getLetterCount(letter: string): number {
    return this.currentBoard.presentLetterCounts[letter] || 0;
  }
}
