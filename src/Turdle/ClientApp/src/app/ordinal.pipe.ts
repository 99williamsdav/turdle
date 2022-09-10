import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'ordinal'
})
export class OrdinalPipe implements PipeTransform {
  transform(value: number | undefined | null, includeNumber: boolean = false): string {
    if (value == null || value <= 0) {
      return value?.toString() || '';
    }
    let suffix = this.getSuffix(value);
    return includeNumber ? `${value}${suffix}` : suffix;
  }

  getSuffix(value: number): string {
    if (value == 11 || value == 12 || value == 13)
      return "th";
    switch (value % 10) {
      case 1:  return "st";
      case 2:  return "nd";
      case 3:  return "rd";
      default: return "th";
    }
  }
}
