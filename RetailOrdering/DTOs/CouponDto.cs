namespace RetailOrdering.DTOs;
    public class CouponDto
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public int DiscountPercentage { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; }
        public decimal MinimumOrderAmount { get; set; }
    }

    public class CreateCouponDto
    {
        public string Code { get; set; } = string.Empty;
        public int DiscountPercentage { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal MinimumOrderAmount { get; set; }
    }

    public class ApplyCouponDto
    {
        public string Code { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
    }

    public class CouponValidationResultDto
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public int DiscountPercentage { get; set; }
        public decimal DiscountAmount { get; set; }
    }

