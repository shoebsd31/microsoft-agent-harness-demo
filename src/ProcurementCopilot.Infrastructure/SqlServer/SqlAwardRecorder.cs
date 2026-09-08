using System.Globalization;
using Dapper;
using Microsoft.Data.SqlClient;
using ProcurementCopilot.Application.Abstractions;
using ProcurementCopilot.Domain.Common;

namespace ProcurementCopilot.Infrastructure.SqlServer;

/// <summary>
/// Records the award through <c>copilot.usp_RecordAwardRecommendation</c>, which inserts a PENDING purchase order
/// (header + one line) in AdventureWorks and an <c>Procurement.AwardRecommendation</c> row in one transaction.
/// Also keeps the JSON document in the workspace so the memo can cite a file.
/// </summary>
public sealed class SqlAwardRecorder : IAwardRecorder
{
    private readonly SqlConnectionFactory _factory;
    private readonly IAwardRecorder _fileRecorder;

    /// <summary>Initializes the recorder.</summary>
    public SqlAwardRecorder(SqlConnectionFactory factory, IAwardRecorder fileRecorder)
    {
        _factory = factory;
        _fileRecorder = fileRecorder;
    }

    /// <inheritdoc />
    public async Task<Result<AwardRecordReference>> RecordAsync(AwardRecordRequest request, CancellationToken cancellationToken = default)
    {
        await using SqlConnection connection = _factory.Create();
        var parameters = new
        {
            RfpId = request.RfpId.Value,
            VendorId = request.VendorId.Value,
            request.Rationale,
            request.WinningScore,
            UnitPriceUsd = request.UnitPriceInRfpCurrency,
            request.LeadTimeWeeks,
            EmployeeID = _factory.Options.PurchaseOrderEmployeeId,
            ShipMethodID = _factory.Options.ShipMethodId,
            RecordedBy = "procurement-copilot",
        };

        AwardProcedureResult result;
        try
        {
            result = await connection.QuerySingleAsync<AwardProcedureResult>(new CommandDefinition(
                "copilot.usp_RecordAwardRecommendation", parameters, commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: _factory.CommandTimeout, cancellationToken: cancellationToken)).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number is >= 51000 and < 52000)
        {
            return ToError(ex.Message);
        }

        Result<AwardRecordReference> file = await _fileRecorder.RecordAsync(request, cancellationToken).ConfigureAwait(false);
        string reference = string.Create(CultureInfo.InvariantCulture,
            $"AdventureWorks purchase order {result.PurchaseOrderID} (status Pending, total due {result.TotalDue:N2}); Procurement.AwardRecommendation #{result.AwardRecommendationID}");
        return new AwardRecordReference(file.IsSuccess ? reference + "; " + file.Value.Reference : reference, result.PurchaseOrderID);
    }

    /// <summary>Converts a <c>THROW</c> message of the form <c>Code.Name: text</c> into a domain error.</summary>
    private static Error ToError(string message)
    {
        int colon = message.IndexOf(':', StringComparison.Ordinal);
        return colon > 0 && !message.AsSpan(0, colon).Contains(' ')
            ? new Error(message[..colon], message[(colon + 1)..].Trim())
            : new Error("Award.DatabaseError", message);
    }

    /// <summary>Result set returned by the stored procedure.</summary>
    public sealed class AwardProcedureResult
    {
        /// <summary>New purchase order id.</summary>
        public int PurchaseOrderID { get; set; }

        /// <summary>New award recommendation id.</summary>
        public int AwardRecommendationID { get; set; }

        /// <summary>Purchase order total.</summary>
        public decimal TotalDue { get; set; }
    }
}
