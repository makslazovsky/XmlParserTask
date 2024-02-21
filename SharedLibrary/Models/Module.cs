using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Models
{
    public class Module
    {
        [Key]
        public string ModuleCategoryID { get; set; }
        public string ModuleState { get; set; }
        
    }
}
