# Trade — UI

## Panel Layout

Opened from Items tab → modal / pane within `ItemsTab`.

- **Slot selector** — which slot to buy for (auto-set to current).
- **Base / Rarity** — base item type filter.
- **Stat weight editor** (`TradeStatWeightMultiplierListControl`) — list of mod-ID × weight pairs derived from current item; user can add/remove/tweak weights and multipliers.
- **Results list** — paginated; each row: item thumbnail, price (chaos-eq), seller, DPS/EHP delta vs equipped, "Open in Trade" button.
- **Compare panel** — side-by-side delta.

## Controls

- Search / refresh button.
- Sort by price / weight.
- Pagination (10 results per fetch block).
- Rate-limit indicator showing remaining budget in current window.
