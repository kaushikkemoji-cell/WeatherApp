export type WeatherStatus = 'Ok' | 'InvalidDate' | 'NoData' | 'ApiError';

export interface WeatherEntry {
  input: string;
  date: string | null;
  minTemperature: number | null;
  maxTemperature: number | null;
  precipitation: number | null;
  temperatureUnit: string | null;
  precipitationUnit: string | null;
  status: WeatherStatus;
  fromCache: boolean;
  error: string | null;
}

export interface WeatherReport {
  location: string;
  entries: WeatherEntry[];
}