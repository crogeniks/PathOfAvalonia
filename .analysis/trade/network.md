# Trade — Network

## HTTP Layer

- `launch:DownloadPage(url, headers, body, callback)` — async HTTP from the host runtime.
- Queue model: requests are enqueued → wait for rate budget → executed → callback.

## Endpoints

- `POST /api/trade/search/<league>` — submit query. Body = JSON:
  ```json
  {
    "query": {
      "status": { "option": "online" },
      "stats":  [ { "type": "weight", "filters": [...], "value": { "weight": 100 } } ],
      "filters": { "type_filters": { ... } }
    },
    "sort": { "statgroup.0": "desc" }
  }
  ```
- `GET  /api/trade/fetch/<id1>,<id2>,...?query=<queryId>` — fetch item details.
- PoE Ninja currency endpoint for chaos equivalence.

## Rate Limiting

`TradeQueryRateLimiter` parses PoE's `X-Rate-Limit-*` headers. Typical windows:

| Scope | Limits |
|-------|--------|
| IP (search)  | 8 / 10s · 15 / 60s · 60 / 300s |
| Account      | 3 / 5s |

- 429 response → exponential backoff `2^attempts`, capped at 60 s.
- Separate search and fetch policies; each has its own queue and independent budgets.

## Authentication

- `POESESSID` cookie stored in build config → passes HTTP request as `Cookie`.
- Session validated by observing the rate-limit header quotas (account-scoped limits appear only when auth'd).

## Parsing

Response JSON is parsed into:

```lua
{
  id, listing = { indexed, account = { name, online }, price = { amount, currency } },
  item = { raw text or structured mods },
}
```

Raw-text items are fed back into `Item:ParseRaw()` for consistent calc integration.
