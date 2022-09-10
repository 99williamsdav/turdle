import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'showPlus'
})
export class ShowPlusPipe implements PipeTransform {
  transform(value: number | undefined, dp: number | null = null): string {
    if (value == null) {
      return '';
    }

    let strVal = dp != null ? value.toFixed(dp) : value.toString();
    return value < 0 ? strVal : `+${strVal}`;
  }
}
