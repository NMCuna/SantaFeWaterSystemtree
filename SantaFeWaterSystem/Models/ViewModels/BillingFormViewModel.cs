using SantaFeWaterSystem.Models;
using System.Collections.Generic;

namespace SantaFeWaterSystem.ViewModels
{
    public class BillingFormViewModel
    {
        public Billing Billing { get; set; }

        // Use this if you want full Consumer objects (with all properties)
        public List<Consumer> Consumers { get; set; } = new List<Consumer>();

       
        
    }
}
