//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace StarbucksScraper
{
    using System;
    using System.Collections.Generic;
    
    public partial class Feature
    {
        public int Id { get; set; }
        public int StoreID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    
        public virtual Store Store { get; set; }
    }
}
