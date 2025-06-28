import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class EnvironmentService {
  private environmentNameSubject = new BehaviorSubject<string | null>(null);
  public environmentName$ = this.environmentNameSubject.asObservable();
  private environmentVersionSubject = new BehaviorSubject<string | null>(null);
  public environmentVersion$ = this.environmentVersionSubject.asObservable();

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }

  public loadEnvironment(): void {
    this.http.get<EnvironmentInfo>(this.baseUrl + 'GetEnvironmentInfo')
      .subscribe(info => {
        this.environmentNameSubject.next(info.name);
        this.environmentVersionSubject.next(info.version);
      });
  }
}

export interface EnvironmentInfo {
  name: string;
  version: string;
}
