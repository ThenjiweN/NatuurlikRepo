﻿using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NatuurlikBase.Models
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }

        [Display(Name = "Company Name")]
        [Required]
        [MaxLength(50)]
        public string CompanyName { get; set; }

        [Display(Name = "Email Address")]
        [Required]
        [EmailAddress]
        public string EmailAddress { get; set; }

        [Required]
        [Display(Name = "Phone Number")]
        [MaxLength(9)]
        [RegularExpression(@"^(\d{9})$", ErrorMessage = "Please enter a valid Phone Number.")]
        public string PhoneNumber { get; set; }

        [Display(Name = "Street Address")]
        [Required]
        [MaxLength(50)]
        public string StreetAddress { get; set; }

        public int CountryId { get; set; }
        [ValidateNever]
        [ForeignKey("CountryId")]
        public Country Country { get; set; }

        public int ProvinceId { get; set; }
        [ValidateNever]
        [ForeignKey("ProvinceId")]
        public Province Province { get; set; }

        public int CityId { get; set; }
        [ForeignKey("CityId")]
        [ValidateNever]
        public City City { get; set; }

        public int SuburbId { get; set; }
        [ValidateNever]
        [ForeignKey("SuburbId")]
        public Suburb Suburb { get; set; }

        public List<SupplierInventory>? SupplierInventory { get; set; }


    }
}
