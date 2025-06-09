import { Inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class EnvironmentService {
  private environmentNameSubject = new BehaviorSubject<string | null>(null);
  public environmentName$ = this.environmentNameSubject.asObservable();

  constructor(private http: HttpClient, @Inject('BASE_URL') private baseUrl: string) { }

  public loadEnvironment(): void {
    this.http.get(this.baseUrl + 'GetEnvironmentName', { responseType: 'text' })
      .subscribe(name => this.environmentNameSubject.next(name));
  }
}
