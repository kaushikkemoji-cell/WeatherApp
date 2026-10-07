# Historical Weather – .NET 8 + Angular

Reads dates from `dates.txt`, fetches historical daily weather for Dallas, TX from the
[Open-Meteo Historical Weather API](https://open-meteo.com/en/docs/historical-weather-api),
caches each result as JSON, and displays it in an Angular UI.

## Tech stack

| Layer | Tech |
|---|---|
| Backend | C# / .NET 8, ASP.NET Core Web API (controllers), typed `HttpClient` |
| Frontend | Angular 19 (standalone components, signals, new control flow) |
| Tests | xUnit (32 tests: parser, API client, cache, service) |

## Project structure

```
├── WeatherApp.Api/            ASP.NET Core Web API
│   ├── Configuration/         Strongly typed options (OpenMeteo, Storage)
│   ├── Controllers/           WeatherController -> GET /api/weather
│   ├── Models/                API response, Open-Meteo DTOs, internal results
│   ├── Services/              DateParser, OpenMeteoClient, FileWeatherCache,
│   │                          FileDatesSource, WeatherService (orchestration)
│   ├── dates.txt              Input dates
│   └── appsettings.json       Location, units, timeouts, file paths
├── WeatherApp.Tests/          xUnit tests
└── weather-ui/                Angular app
    ├── proxy.conf.json        Forwards /api to the backend in development
    └── src/app/weather-page/  Table, loading/error states, sort, filter, details
```

## Prerequisites

- .NET 8 SDK
- Node.js 20+ and npm
- A trusted .NET development certificate: `dotnet dev-certs https --trust`

## Run the backend

**Visual Studio:** open `WeatherApp.Api.sln`, select the `https` profile, press F5.

**CLI:**
```bash
dotnet run --project WeatherApp.Api --launch-profile https
```

The API listens on `https://localhost:7299`. Swagger UI: `https://localhost:7299/swagger`.

## Run the UI

With the backend running, in a second terminal:
```bash
cd weather-ui
npm install
npm start
```

Open `http://localhost:4200`. The dev server proxies `/api/*` to `https://localhost:7299`
(see `weather-ui/proxy.conf.json`), so no CORS configuration is needed locally.

## Run the tests

```bash
dotnet test
```

## API

### `GET /api/weather`

Returns one entry per line in `dates.txt`, in file order, each with its own status.

```json
{
  "location": "Dallas, TX",
  "entries": [
    {
      "input": "02/27/2021",
      "date": "2021-02-27",
      "minTemperature": 49.5,
      "maxTemperature": 72.7,
      "precipitation": 1.189,
      "temperatureUnit": "°F",
      "precipitationUnit": "inch",
      "status": "Ok",
      "fromCache": true,
      "error": null
    },
    {
      "input": "April 31, 2022",
      "date": null,
      "minTemperature": null,
      "maxTemperature": null,
      "precipitation": null,
      "temperatureUnit": null,
      "precipitationUnit": null,
      "status": "InvalidDate",
      "fromCache": false,
      "error": "'April 31, 2022' is not a valid calendar date in a supported format."
    }
  ]
}
```

| Status | Meaning |
|---|---|
| `Ok` | Weather data returned (from cache or from Open-Meteo) |
| `InvalidDate` | The line could not be parsed as a real calendar date |
| `NoData` | Open-Meteo returned no observations for that date |
| `ApiError` | Network failure, timeout, HTTP error, or malformed response |

If `dates.txt` itself is missing, the endpoint returns HTTP 500 with a `ProblemDetails` body.

## How it works

1. **Parse.** `DateParser` uses `DateOnly.TryParseExact` with an explicit list of formats and
   `InvariantCulture`. Impossible dates (April 31, Feb 29 in a non-leap year) are rejected by the
   calendar check. The parser never throws; it returns a result with a date or an error.
2. **Cache lookup.** `FileWeatherCache` checks `weather-data/yyyy-MM-dd.json`. A hit skips the API call.
   A corrupt file is treated as a miss and refetched.
3. **Fetch.** `OpenMeteoClient` (typed `HttpClient` via `IHttpClientFactory`) calls `/v1/archive`.
   Every outcome maps to `Success`, `NoData`, or `Failed`. Network errors, timeouts, HTTP 4xx/5xx,
   bad JSON, and null values are all handled without exceptions escaping.
4. **Store.** Only successful results are cached, written atomically (temp file, then rename).
5. **Respond.** `WeatherService` builds one entry per input line, so one bad date never fails the request.

## Configuration

All settings live in `WeatherApp.Api/appsettings.json` and are validated at startup:

```json
"OpenMeteo": {
  "BaseUrl": "https://archive-api.open-meteo.com/",
  "LocationName": "Dallas, TX",
  "Latitude": 32.78,
  "Longitude": -96.8,
  "Timezone": "America/Chicago",
  "TemperatureUnit": "fahrenheit",
  "PrecipitationUnit": "inch",
  "TimeoutSeconds": 15
},
"Storage": {
  "DatesFile": "dates.txt",
  "CacheDirectory": "weather-data"
}
```

No secrets are required. Relative paths resolve against the API project's content root.

## Assumptions

- **Invalid dates are included in the response** with `status: "InvalidDate"` and an error message,
  rather than silently dropped, so the UI can show what went wrong.
- **Supported input formats:** `M/d/yyyy`, `MMMM d, yyyy`, `MMM-d-yyyy`, and `yyyy-MM-dd`
  (US month-first, English month names). Day-first dates like `27/02/2021` are treated as invalid
  to avoid ambiguity. `Sept` is not recognized (InvariantCulture uses `Sep`).
- **Units:** Fahrenheit and inches, as the location is in the US. Configurable in `appsettings.json`.
- **Caching:** historical data does not change, so cached files never expire. `NoData` and errors
  are not cached, so a later request can succeed.
- **`weather-data/`** is generated at runtime and excluded from source control.
- **Blank lines** in `dates.txt` are ignored.
- Requests run **sequentially**. The input is small, and this keeps load on a free public API low.

## What I would improve for production

- **Resilience:** retries with exponential backoff and a circuit breaker (Polly /
  `Microsoft.Extensions.Http.Resilience`).
- **Throughput:** batch dates into a single date-range request, or run requests in parallel with a
  concurrency limit.
- **Caching:** replace the file cache with Redis or Blob Storage behind the existing `IWeatherCache` interface.
- **Observability:** Application Insights, structured logs with correlation IDs, and health checks.
- **Frontend:** component tests, sort/filter state in the URL, and a date-range filter.
- **API:** accept dates or a location as query parameters, add pagination, and version the API.
- **Deployment:** serve the Angular build from the API (or the same origin), with CI running tests on every PR.