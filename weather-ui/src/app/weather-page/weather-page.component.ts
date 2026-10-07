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
import { WeatherEntry, WeatherReport, WeatherStatus } from '../weather.models';

type SortKey = 'date' | 'minTemperature' | 'maxTemperature' | 'precipitation';
type SortDirection = 'asc' | 'desc';

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

  // ---- Data state ----
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly report = signal<WeatherReport | null>(null);
  readonly entries = computed(() => this.report()?.entries ?? []);

  // ---- Interaction state ----
  readonly sortKey = signal<SortKey>('date');
  readonly sortDirection = signal<SortDirection>('asc');
  readonly minTempFilter = signal<number | null>(null);
  readonly selectedEntry = signal<WeatherEntry | null>(null);

  /** What the table renders: filtered, then sorted. Recomputes only when its inputs change. */
  readonly visibleEntries = computed(() => {
    const minTemp = this.minTempFilter();
    const key = this.sortKey();
    const direction = this.sortDirection() === 'asc' ? 1 : -1;

    // filter() returns a new array, so sort() never mutates the source data.
    return this.entries()
      .filter((e) => minTemp === null || (e.minTemperature !== null && e.minTemperature >= minTemp))
      .sort((a, b) => compareNullsLast(a[key], b[key], direction));
  });

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
    this.selectedEntry.set(null);

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

  // ---- Sorting ----
  sortBy(key: SortKey): void {
    if (this.sortKey() === key) {
      this.sortDirection.update((d) => (d === 'asc' ? 'desc' : 'asc'));
    } else {
      this.sortKey.set(key);
      this.sortDirection.set('asc');
    }
  }

  sortIndicator(key: SortKey): string {
    if (this.sortKey() !== key) {
      return '';
    }
    return this.sortDirection() === 'asc' ? '▲' : '▼';
  }

  ariaSort(key: SortKey): 'ascending' | 'descending' | 'none' {
    if (this.sortKey() !== key) {
      return 'none';
    }
    return this.sortDirection() === 'asc' ? 'ascending' : 'descending';
  }

  // ---- Filtering ----
  onMinTempInput(event: Event): void {
    const raw = (event.target as HTMLInputElement).value.trim();
    const value = raw === '' ? null : Number(raw);
    this.minTempFilter.set(value !== null && Number.isFinite(value) ? value : null);
  }

  clearFilter(): void {
    this.minTempFilter.set(null);
  }

  // ---- Row details ----
  toggleDetails(entry: WeatherEntry): void {
    this.selectedEntry.update((current) => (current === entry ? null : entry));
  }

  temperatureRange(entry: WeatherEntry): string {
    if (entry.minTemperature === null || entry.maxTemperature === null) {
      return '—';
    }
    return this.formatValue(entry.maxTemperature - entry.minTemperature, entry.temperatureUnit);
  }

  // ---- Formatting ----
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

/**
 * Sorts numbers numerically and strings alphabetically. ISO dates (yyyy-MM-dd) sort
 * correctly as strings. Missing values always go last, whatever the direction.
 */
function compareNullsLast(
  a: string | number | null,
  b: string | number | null,
  direction: number,
): number {
  if (a === null && b === null) return 0;
  if (a === null) return 1;
  if (b === null) return -1;

  const result =
    typeof a === 'number' && typeof b === 'number'
      ? a - b
      : String(a).localeCompare(String(b));

  return result * direction;
}