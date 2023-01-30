import {Component, EventEmitter, Input, Output} from '@angular/core';
import { NgbActiveModal } from '@ng-bootstrap/ng-bootstrap';
import {Board, GameParameters, Player, PointSchedule, Tile} from "../services/game.service";

@Component({
  selector: 'game-params',
  templateUrl: './game-params.component.html',
  styleUrls: ['./game-params.component.css']
})
export class GameParamsComponent {
  @Input() gameParams!: GameParameters;

  constructor(public activeModal: NgbActiveModal) { }

  public saveChanges(): void {
    this.activeModal.close(true);
  }
  public close(): void {
    this.activeModal.dismiss('closed');
  }
}
