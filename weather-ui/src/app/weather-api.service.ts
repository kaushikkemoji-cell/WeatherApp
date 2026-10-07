import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { WeatherReport } from './weather.models';

@Injectable({ providedIn: 'root' })
export class WeatherApiService {
  private readonly http = inject(HttpClient);

  /** Relative URL: the dev proxy forwards /api to the .NET backend. */
  getWeather(): Observable<WeatherReport> {
    return this.http.get<WeatherReport>('/api/weather');
  }
}