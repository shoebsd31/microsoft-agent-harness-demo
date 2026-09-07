using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Domain.Errors;

/// <summary>Catalogue of domain errors so codes are stable and reusable across layers.</summary>
public static class DomainErrors
{
    /// <summary>Identifier format errors.</summary>
    public static class Ids
    {
        /// <summary>RFP id does not match <c>^[A-Z]{3}-\d{4}-\d{3}$</c>.</summary>
        public static readonly Error InvalidRfpId = new("RfpId.InvalidFormat", "RFP id must look like RFP-2026-017.");

        /// <summary>Vendor id does not match <c>^VND-\d{4}$</c>.</summary>
        public static readonly Error InvalidVendorId = new("VendorId.InvalidFormat", "Vendor id must look like VND-0001.");

        /// <summary>Bid id does not match <c>^BID-\d{3}$</c>.</summary>
        public static readonly Error InvalidBidId = new("BidId.InvalidFormat", "Bid id must look like BID-001.");
    }

    /// <summary>Money and currency errors.</summary>
    public static class Money
    {
        /// <summary>Currency code is not a three-letter ISO-4217 code on the allowlist.</summary>
        public static readonly Error InvalidCurrency = new("Currency.Invalid", "Currency must be a supported ISO-4217 code (EUR, USD, GBP, JPY, CHF, SEK).");

        /// <summary>Amount is negative or not finite.</summary>
        public static readonly Error InvalidAmount = new("Money.InvalidAmount", "Amount must be zero or positive.");

        /// <summary>Two money values with different currencies were combined.</summary>
        public static readonly Error CurrencyMismatch = new("Money.CurrencyMismatch", "Cannot combine amounts in different currencies.");

        /// <summary>No FX rate exists for the requested pair.</summary>
        public static readonly Error RateNotFound = new("Fx.RateNotFound", "No exchange rate is seeded for the requested currency pair.");
    }

    /// <summary>Lookup errors.</summary>
    public static class NotFound
    {
        /// <summary>The RFP does not exist.</summary>
        public static readonly Error Rfp = new("Rfp.NotFound", "The requested RFP was not found.");

        /// <summary>The vendor does not exist.</summary>
        public static readonly Error Vendor = new("Vendor.NotFound", "The requested vendor was not found.");

        /// <summary>The bid does not exist.</summary>
        public static readonly Error Bid = new("Bid.NotFound", "The requested bid was not found.");
    }

    /// <summary>Scoring errors.</summary>
    public static class Scoring
    {
        /// <summary>The bid does not belong to the RFP being scored.</summary>
        public static readonly Error BidNotForRfp = new("Scoring.BidNotForRfp", "The bid was submitted for a different RFP.");

        /// <summary>No bids are available to compute relative scores.</summary>
        public static readonly Error NoBids = new("Scoring.NoBids", "The RFP has no bids to score.");

        /// <summary>The criteria weights do not sum to a positive number.</summary>
        public static readonly Error InvalidWeights = new("Scoring.InvalidWeights", "Criteria weights must be non-negative and sum to more than zero.");

        /// <summary>A required numeric input was missing or out of range.</summary>
        public static readonly Error MissingData = new("Scoring.MissingData", "A bid is missing data required for scoring.");
    }

    /// <summary>Award and workflow errors.</summary>
    public static class Award
    {
        /// <summary>The vendor is on the sanctions list and cannot be awarded.</summary>
        public static readonly Error VendorSanctioned = new("Award.VendorSanctioned", "A sanctioned vendor cannot receive an award recommendation.");

        /// <summary>The rationale is empty or too long.</summary>
        public static readonly Error InvalidRationale = new("Award.InvalidRationale", "A rationale between 1 and 4000 characters is required.");
    }
}
