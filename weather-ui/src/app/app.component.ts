import { Component } from '@angular/core';

import { WeatherPageComponent } from './weather-page/weather-page.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [WeatherPageComponent],
  template: '<app-weather-page />',
})
export class AppComponent {}