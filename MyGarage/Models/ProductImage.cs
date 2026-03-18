using System;
using System.Collections.Generic;

namespace MyGarage.Models;

public partial class ProductImage
{
    public long Id { get; set; }

    public long ProductId { get; set; }

    public string ImageUrl { get; set; } = null!;

    public bool? IsMain { get; set; }

    public int? SortOrder { get; set; }

    public string? Description { get; set; }

    public virtual Product Product { get; set; } = null!;
}
