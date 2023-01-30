import { BrowserModule } from '@angular/platform-browser';
import { NgModule } from '@angular/core';
import {FormsModule, ReactiveFormsModule} from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';

import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { HomeComponent } from './home/home.component';
import {GameComponent, NotAliasPipe} from "./game/game.component";
import { GameService } from './services/game.service';
import { GameBoardComponent } from "./game-board/game-board.component";
import { OrdinalPipe } from "./ordinal.pipe";
import { KeyboardComponent } from "./keyboard/keyboard.component";
import { ShowPlusPipe } from "./show-plus.pipe";
import {AdminComponent} from "./admin/admin.component";
import {OrderByPipe} from "./order-by.pipe";
import { NgbModule } from '@ng-bootstrap/ng-bootstrap';
import {AdminService} from "./services/admin.service";
import {TvComponent} from "./tv/tv.component";
import {HumanizePipe} from "./humanize.pipe";
import {CookieModule} from "ngx-cookie";
import {FormatMillisecondsPipe} from "./format-milliseconds";
import TrackByUtils from "./track-by.utils";
import {PointScheduleComponent} from "./point-schedule/point-schedule.component";
import {HomeService} from "./services/home.service";
import {ToastsContainer} from "./toast/toasts-container.component";
import { GameParamsComponent } from './game-params/game-params.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    HomeComponent,
    GameComponent,
    AdminComponent,
    GameBoardComponent,
    OrdinalPipe,
    KeyboardComponent,
    ShowPlusPipe,
    OrderByPipe,
    HumanizePipe,
    FormatMillisecondsPipe,
    TvComponent,
    PointScheduleComponent,
    GameParamsComponent,
    NotAliasPipe,
    ToastsContainer
  ],
    imports: [
      BrowserModule.withServerTransition({appId: 'ng-cli-universal'}),
      HttpClientModule,
      FormsModule,
      RouterModule.forRoot([
        {path: '', component: HomeComponent, pathMatch: 'full'},
        {path: 'play/:code', component: GameComponent},
        {path: 'onlydavidallowed', component: AdminComponent},
        {path: 'tv', component: TvComponent},
        {path: '**', redirectTo: '', pathMatch: 'full'}
      ]),
      ReactiveFormsModule,
      NgbModule,
      CookieModule.forRoot()
    ],
  providers: [
    GameService,
    AdminService,
    HomeService,
    OrderByPipe,
    TrackByUtils
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
