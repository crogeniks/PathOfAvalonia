# Trade — Logic

## Query Flow

1. User selects an equipped item / empty slot.
2. `TradeQueryGenerator` converts the item's mods into a weighted `trade.pathofexile.com` stat filter.
3. Query POSTed to `/api/trade/search/<league>` → returns an ID + up to 10 000 result IDs.
4. If result set >10k: binary-search the weight threshold to narrow the list.
5. `TradeQueryRequests` fetches result blocks of 10 via `/api/trade/fetch/<ids>`.
6. Results rendered in the results list with price chaos-equivalent (from PoE Ninja).

## Core Classes

- **`TradeQuery`** — pane coordinator; owns league, realm, currency conversion state.
- **`TradeQueryGenerator`** — mod→stat filter mapping, weight derivation.
- **`TradeQueryRequests`** — HTTP queue manager; respects rate limits.
- **`TradeQueryRateLimiter`** — parses `X-Rate-Limit` headers, tracks windows.
- **`CompareBuySimilar`** — builds "buy similar" URLs for compared items.
- **`CompareTradeHelpers`** — mod ID matching, category mapping.

## Buy-Similar

Given an item in the Compare tab, `CompareBuySimilar` constructs a URL pre-populated with the item's key stats so the user can shop for similar upgrades.

## Currency Conversion

Uses PoE Ninja's currency endpoint. Prices normalised to chaos equivalent; `TradeQuery` caches the exchange table.
