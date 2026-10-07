import { HttpErrorResponse } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';

import { WeatherApiService } from '../weather-api.service';
import { WeatherReport, WeatherStatus } from '../weather.models';

@Component({
  selector: 'app-weather-page',
  standalone: true,
  templateUrl: './weather-page.component.html',
  styleUrl: './weather-page.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class WeatherPageComponent implements OnInit {
  private readonly api = inject(WeatherApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly report = signal<WeatherReport | null>(null);
  readonly entries = computed(() => this.report()?.entries ?? []);

  readonly statusLabels: Record<WeatherStatus, string> = {
    Ok: 'OK',
    InvalidDate: 'Invalid date',
    NoData: 'No data',
    ApiError: 'API error',
  };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.api
      .getWeather()
      .pipe(
        finalize(() => this.loading.set(false)),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (report) => this.report.set(report),
        error: (err: HttpErrorResponse) => this.error.set(this.describeError(err)),
      });
  }

  formatValue(value: number | null, unit: string | null, digits = 1): string {
    return value === null ? '—' : `${value.toFixed(digits)} ${unit ?? ''}`.trim();
  }

  private describeError(err: HttpErrorResponse): string {
    // The API returns ProblemDetails ({ title, detail }) for known server-side failures.
    const problem = err.error as { title?: string; detail?: string } | null;
    if (problem?.detail) {
      return problem.detail;
    }
    if (err.status === 0 || err.status >= 500) {
      return 'The weather service is unavailable. Make sure the API is running and try again.';
    }
    return `Unexpected response from the weather service (HTTP ${err.status}).`;
  }
}