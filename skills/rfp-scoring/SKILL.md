---
name: rfp-scoring
description: Weighted scoring method for Contoso RFP bids - normalisation rules, tie-breakers and a worked example. Use when scoring, ranking or explaining bid scores for an RFP.
---

# RFP scoring method

Contoso scores every bid on five criteria. The `score_bid` tool implements this method exactly; use this skill to
explain and sanity-check its output, never to replace it.

## Criteria and default weights

| Criterion | Weight (RFP-2026-017) | Normalisation |
|---|---|---|
| Price (unit price in RFP currency) | 35 | `100 × cheapest price / this price` |
| Delivery lead time (weeks) | 20 | `100 × shortest lead time / this lead time` |
| Warranty (months) | 15 | `100 × this warranty / longest warranty` |
| Technical compliance (%) | 20 | taken as-is |
| Sustainability (ISO 14001) | 10 | 100 if the vendor holds ISO 14001, else 0 |

Weights are relative: the weighted total is `Σ(weight × criterion score) / Σ(weights)`, rounded to two decimals.
An analyst may override a weight (for example "weight sustainability at 20") - state the override in the memo.

## Rules

1. Convert every foreign-currency bid into the RFP currency with `convert_currency` before comparing prices. Cite the rate and its date.
2. Relative criteria are normalised against the **best bid among all bids of the RFP**, including bids that later fail compliance. Compliance never changes a score; it changes eligibility.
3. Missing data (no price, zero lead time, compliance outside 0-100) makes the bid unscorable: report it, do not guess.
4. **Tie-breakers**, in order: higher technical compliance, shorter lead time, longer warranty, lower bid id.
5. A sanctioned vendor is ineligible regardless of score. Exclude it from the ranking used for the recommendation but keep its score in the table for transparency.

See `references/scoring-rubric.md` for the worked example with the seeded RFP-2026-017 data.
