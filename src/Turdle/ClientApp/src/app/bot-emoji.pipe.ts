import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'botEmoji'
})
export class BotEmojiPipe implements PipeTransform {
  transform(alias: string | null | undefined, isBot: boolean | null | undefined): string {
    if (!alias) {
      return '';
    }

    return isBot ? `${alias} 🤖` : alias;
  }
}
