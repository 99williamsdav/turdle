import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'botEmoji'
})
export class BotEmojiPipe implements PipeTransform {
  transform(alias: string, isBot: boolean | null | undefined): string {
    return isBot ? `${alias} 🤖` : alias;
  }
}
