using Npgsql;

var connString = "Host=192.168.50.199;Port=5432;Username=postgres;Password=123456;Database=moai_wiki";
await using var conn = new NpgsqlConnection(connString);
await conn.OpenAsync();
await using var cmd = new NpgsqlCommand("SELECT id, team_id, name, creator_user_id, expire_time, last_used_time FROM team_api_key ORDER BY create_time DESC LIMIT 6", conn);
await using var reader = await cmd.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    Console.WriteLine($"id={reader.GetGuid(0).ToString()[..8]} team={reader.GetInt32(1)} name={reader.GetString(2)} creator={reader.GetInt64(3)} expire={reader[4]} lastUsed={reader[5]}");
}
