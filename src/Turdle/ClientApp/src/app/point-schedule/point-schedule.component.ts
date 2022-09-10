import {Component, EventEmitter, Input, Output} from '@angular/core';
import {Board, Player, PointSchedule, Tile} from "../services/game.service";

@Component({
  selector: 'point-schedule',
  templateUrl: './point-schedule.component.html',
  styleUrls: ['./point-schedule.component.css']
})
export class PointScheduleComponent {
  @Input() pointSchedule!: PointSchedule;
  @Input() playerCount!: number;

  public enumerate(count: number): number[] | null {
    if (count <= 0)
      return [];
    return Array.from(Array(count).keys())
  }
}
