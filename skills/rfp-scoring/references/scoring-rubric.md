# Worked example: RFP-2026-017 (40 CNC vertical machining centres, EUR)

Weights: price 35, lead time 20, warranty 15, technical 20, sustainability 10 (total 100).

| Bid | Vendor | Unit price (EUR) | Lead (wk) | Warranty (mo) | Technical | ISO 14001 |
|---|---|---|---|---|---|---|
| BID-001 | VND-0001 Müller | 185,000.00 | 16 | 24 | 96 | yes |
| BID-002 | VND-0002 Nordic CNC | 168,500.00 | 20 | 18 | 91 | no |
| BID-003 | VND-0003 Apex | 183,080.00 (USD 199,000 × 0.92) | 14 | 36 | 94 | yes |
| BID-004 | VND-0004 Veldora | 149,900.00 | 12 | 12 | 88 | yes |
| BID-005 | VND-0005 Kyoto Precision | 176,000.00 | 18 | 30 | 98 | yes |

Best values: cheapest 149,900.00 (BID-004), shortest lead 12 weeks (BID-004), longest warranty 36 months (BID-003).

Criterion scores for BID-001: price 100 × 149,900 / 185,000 = 81.03; lead 100 × 12 / 16 = 75.00; warranty 100 × 24 / 36 = 66.67;
technical 96.00; sustainability 100.
Weighted total = (81.03×35 + 75.00×20 + 66.67×15 + 96×20 + 100×10) / 100 = **82.56**.

Repeat for every bid; the `score_bid` tool returns the same breakdown and the rank. Sanctioned VND-0004 keeps its score in the
table but is excluded from the recommendation.
