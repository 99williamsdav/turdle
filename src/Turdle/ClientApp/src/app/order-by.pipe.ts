import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'orderBy'
})
export class OrderByPipe implements PipeTransform {
  transform<T>(value: T[], property: string): T[] {
    return value.sort((a: any, b: any) => {
        if (a[property] < b[property]) {
          return -1;
        } else if (a[property] > b[property]) {
          return 1;
        } else {
          return 0;
        }
      });
  }
}
