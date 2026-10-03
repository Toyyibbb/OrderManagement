using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;
using WebApplication3.Models;

namespace WebApplication3.Services;

public class ProductService
{
    private readonly IConfiguration _configuration;

    public ProductService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

public List<Product> GetProduk()
{
    var list = new List<Product>();

    var connectionString = _configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
        throw new Exception("DefaultConnection tidak ditemukan di appsettings.json.");

    using var conn = new SqlConnection(connectionString);
    using var cmd = new SqlCommand("sp_GetProduk", conn);

    cmd.CommandType = CommandType.StoredProcedure;

    conn.Open();

    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        list.Add(new Product
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Alamat = reader.GetString(reader.GetOrdinal("alamat"))
        });
    }

    return list;
}

public void InsertProduk(Product produk)
    {
        using var conn = new SqlConnection(
            _configuration.GetConnectionString("DefaultConnection"));

        using var cmd = new SqlCommand("sp_InsertProduk", conn);
        cmd.CommandType = CommandType.StoredProcedure;

        cmd.Parameters.AddWithValue("@name", produk.Name);
        cmd.Parameters.AddWithValue("@alamat", produk.Alamat);

        conn.Open();
        cmd.ExecuteNonQuery();
    }
}