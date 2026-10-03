using System.Collections.Generic;
using System.Reflection;
namespace Tes.Models
{
    public class Profile
    {

    //    first name dan last name, company name, job title, email dan upload identity(format pdf, jpg), mobile phone
    
        public int id { get; set; }

        public string firstName { get; set; }

        public string lastName { get; set; }
        public string companyName { get; set; }

        public string phoneNumber { get; set; }

        public string email { get; set; }

        public string jobTitle { get; set; }

        public string identityImage { get; set; }
    }
}
