using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class VwGlTrialBalance
{
    public string AccountCode { get; set; } = null!;

    public string AccountName { get; set; } = null!;

    public decimal? TotalDebit { get; set; }

    public decimal? TotalCredit { get; set; }

    public decimal? Balance { get; set; }
}
