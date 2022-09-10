import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'formatMilliseconds'
})
export class FormatMillisecondsPipe implements PipeTransform {
  transform<T>(value: number): string | null {
    let totalSeconds = Math.floor(value / 1000);
    let minutes = Math.floor(totalSeconds / 60);

    let seconds = totalSeconds % 60;
    let milliseconds = value % 1000;

    if (minutes > 0) {
      return `${minutes}m ${seconds}s`;
    } else {
      return `${seconds}s`;
    }
  }
}
