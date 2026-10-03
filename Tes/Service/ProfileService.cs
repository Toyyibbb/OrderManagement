using Microsoft.Data.SqlClient;
using System.Data;
using Tes.Models;

namespace Tes.Service
{
    public class ProfileService
    {
        private readonly IConfiguration _configuration;

        public ProfileService (IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public List<Profile> GetProfile()
        {
            var list = new List<Profile>();

            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("DefaultConnection tidak ditemukan di appsettings.json.");

            using var conn = new SqlConnection(connectionString);
            using var cmd = new SqlCommand("sp_getProfile", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            conn.Open();

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                list.Add(new Profile
                {

                    id = reader.GetInt32(reader.GetOrdinal("id")),
                    firstName = reader.GetString(reader.GetOrdinal("firstName")),
                    lastName = reader.GetString(reader.GetOrdinal("lastName")),
                    companyName = reader.GetString(reader.GetOrdinal("companyName")),
                    //phoneNumber = reader.GetString(reader.GetOrdinal("phoneNumber")),
                    email = reader.GetString(reader.GetOrdinal("email")),
                    jobTitle = reader.GetString(reader.GetOrdinal("jobTitle")),
                    identityImage = reader.GetString(reader.GetOrdinal("identityImage"))
                });
            }

            return list;
        }

        public void InsertProfile(Profile profile)
        {
            using var conn = new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));

            using var cmd = new SqlCommand("sp_insertProfile", conn);
            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.AddWithValue("@firstName", profile.firstName);
            cmd.Parameters.AddWithValue("@lastName", profile.lastName);
            cmd.Parameters.AddWithValue("@companyName", profile.companyName);
            cmd.Parameters.AddWithValue("@phoneNumber", profile.phoneNumber);
            cmd.Parameters.AddWithValue("@email", profile.email);
            cmd.Parameters.AddWithValue("@jobTitle", profile.jobTitle);
            cmd.Parameters.AddWithValue("@identityImage", profile.identityImage);


            conn.Open();
            cmd.ExecuteNonQuery();
        }
    }
}
