import { Component, OnInit } from '@angular/core';
import { environment } from '../../environments/environment';
import { EnvironmentService } from '../services/environment.service';

@Component({
  selector: 'app-nav-menu',
  templateUrl: './nav-menu.component.html',
  styleUrls: ['./nav-menu.component.css']
})
export class NavMenuComponent implements OnInit {
  isExpanded = false;
  public environment = environment;
  public environmentName: string | null = null;
  public environmentVersion: string | null = null;

  constructor(private environmentService: EnvironmentService) {
  }

  ngOnInit(): void {
    this.environmentService.environmentName$.subscribe(name => this.environmentName = name || null);
    this.environmentService.environmentVersion$.subscribe(version => this.environmentVersion = version || null);
    this.environmentService.loadEnvironment();
  }

  collapse() {
    this.isExpanded = false;
  }

  toggle() {
    this.isExpanded = !this.isExpanded;
  }
}
