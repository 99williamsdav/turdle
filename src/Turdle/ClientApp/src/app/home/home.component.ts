import { Component } from '@angular/core';
import { FormBuilder } from '@angular/forms';
import { FormControl, FormGroup } from '@angular/forms';
import { Validators } from '@angular/forms';
import { GameService } from '../services/game.service';
import { Router } from '@angular/router'
import {HomeService} from "../services/home.service";

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
})
export class HomeComponent {

  constructor(
    private homeService: HomeService,
    private fb: FormBuilder,
    private router: Router) {
    console.log('HomeComponent ctor');
  }

  get isConnected() : boolean {
    return this.homeService.isConnected;
  }

  async ngOnInit() {
    await this.homeService.setupConnection();
  }

  async createRoom() {
    console.log('Creating new room');
    let roomCode = await this.homeService.createRoom();

    if (!roomCode) {
      console.log('Room creation unsuccessful');
      return;
    }

    console.log('Room created with code: ' + roomCode);
    await this.router.navigate(['/play', roomCode]);
  }
}
