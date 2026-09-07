---
name: award-memo
description: Template and rules for the RFP award recommendation memo written to the workspace output folder. Use when the evaluation is complete and the memo must be written or checked.
---

# Award memo

When every bid is scored and every compliance hit has a disposition, write the memo with the file access tools to
`output/award-memo-<RFP-ID>.md`, then record the recommendation with `record_award_recommendation` (requires approval).

## Rules

1. Use the template in `templates/award-memo.md` verbatim: keep every heading, fill every placeholder.
2. All money in EUR with two decimals; cite the tool behind every number (`score_bid`, `convert_currency`, `check_vendor_compliance`).
3. Include the background research results (`market-research`, `risk-analyst`) and say which background agent produced them.
4. Sanctioned vendors appear in the table with their score but are marked *ineligible* and never recommended.
5. Open clarifications are listed under *Risks and open items* with the outbox draft path.
6. The memo is a **recommendation**; the analyst awards. Do not claim the contract is awarded.
