import { Component } from '@angular/core';
import { FormBuilder, Validators } from '@angular/forms';
import { FormGroup } from '@angular/forms';
import { Router } from '@angular/router';
import { HomeService } from '../services/home.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css', '../letter-colors.shared.css']
})
export class HomeComponent {

  joinForm: FormGroup;

  constructor(
    private homeService: HomeService,
    private fb: FormBuilder,
    private router: Router) {
    console.log('HomeComponent ctor');
    this.joinForm = this.fb.group({
      roomCode: ['', Validators.required]
    });
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

  async joinRoom() {
    const code = this.joinForm.value.roomCode;
    if (!code)
      return;
    await this.router.navigate(['/play', code]);
  }
}
