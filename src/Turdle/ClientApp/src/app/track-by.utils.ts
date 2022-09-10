import {Player, Row, Tile} from "./services/game.service";

export default class TrackByUtils {
  public trackByPlayer(index: number, player: Player): any { return player.alias; }
  public trackByRow(index: number, row: Row): any { return row.guessNumber; }
  public trackByTile(index: number, tile: Tile): any { return `${tile.letterPosition?.letter}|${tile.letterPosition?.position}`; }
}
